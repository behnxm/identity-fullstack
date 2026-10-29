using System.Collections.Concurrent;
using System.Security.Claims;
using Identity.Api.Common.Constants;
using Identity.Api.Common.Options;
using Identity.Api.Email;
using Identity.Api.Persistence;
using Identity.Api.Persistence.Entities;
using Identity.Api.Security.Keys;
using Identity.Api.Security.Sessions;
using Identity.Api.Security.Tokens;
using Identity.Api.Seeding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace Identity.IntegrationTests.Infrastructure;

public sealed class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentBag<EmailMessage> _sentEmails = new();

    public IReadOnlyList<EmailMessage> SentEmails => _sentEmails.ToList();

    public Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _sentEmails.Add(message);
        return Task.CompletedTask;
    }

    public void Clear() => _sentEmails.Clear();

    public EmailMessage? GetLastEmailTo(string email) =>
        _sentEmails.LastOrDefault(m => string.Equals(m.ToEmail, email, StringComparison.OrdinalIgnoreCase));
}

public sealed class CapturingLogProvider : ILoggerProvider, ILogger
{
    private readonly ConcurrentBag<string> _logs = new();

    public IReadOnlyList<string> Logs => _logs.ToList();

    public ILogger CreateLogger(string categoryName) => this;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var msg = formatter(state, exception);
        _logs.Add(msg);
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public void Dispose() { }
}

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;
    private string _connectionString = "Host=127.0.0.1;Port=5432;Database=identity_test;Username=postgres;Password=postgres;Ssl Mode=Disable;";

    public FakeTimeProvider TimeProvider { get; } = new(DateTimeOffset.UtcNow);
    public CapturingEmailSender EmailSender { get; } = new();
    public CapturingLogProvider LogProvider { get; } = new();

    public async ValueTask InitializeAsync()
    {
        // Check if Testcontainers can be used (if Docker daemon is reachable)
        var useTestcontainers = Environment.GetEnvironmentVariable("USE_TESTCONTAINERS") == "true";
        if (useTestcontainers)
        {
            try
            {
                _postgresContainer = new PostgreSqlBuilder("postgres:17.11-alpine")
                    .WithDatabase("identity_test")
                    .WithUsername("postgres")
                    .WithPassword("postgres")
                    .Build();

                await _postgresContainer.StartAsync();
                _connectionString = _postgresContainer.GetConnectionString();
            }
            catch
            {
                // Fall back to local PostgreSQL if Docker is not active
                _connectionString = "Host=127.0.0.1;Port=5432;Database=identity_test;Username=postgres;Password=postgres;Ssl Mode=Disable;";
            }
        }

        // Apply migrations and seed fresh test database
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        await ResetDatabaseAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = _connectionString,
                ["Jwt:Issuer"] = "https://identity.local",
                ["Jwt:Audience"] = "https://identity.local/api",
                ["Jwt:KeyId"] = "test-key-1",
                ["Jwt:AccessTokenLifetime"] = "00:10:00",
                ["Jwt:ClockSkew"] = "00:00:05",
                ["Seed:Enabled"] = "true",
                ["Seed:DemoUsers"] = "true",
                ["Seed:AllowDemoInProduction"] = "false",
                ["Forwarded:TrustedNetworks"] = "127.0.0.1,::1,172.16.0.0/12",
                ["RateLimiting:LoginPermitLimit"] = "1000",
                ["RateLimiting:SensitivePermitLimit"] = "1000",
                ["RateLimiting:RefreshPermitLimit"] = "1000",
                ["RateLimiting:GlobalPermitLimit"] = "5000"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Configure AppDbContext to use test database
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(_connectionString);
            });

            // Replace time provider with FakeTimeProvider
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(TimeProvider);

            // Replace email sender with capturing sink
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);

            // Add capturing logger provider
            services.AddSingleton<ILoggerProvider>(LogProvider);
        });
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ DECLARE
                r RECORD;
            BEGIN
                FOR r IN (SELECT tablename FROM pg_tables WHERE schemaname = current_schema() AND tablename <> '__EFMigrationsHistory') LOOP
                    EXECUTE 'TRUNCATE TABLE ' || quote_ident(r.tablename) || ' CASCADE';
                END LOOP;
            END $$;
            """);

        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();

        EmailSender.Clear();
    }

    public async Task<string> IssueAccessTokenAsync(Guid userId, string email, string role, bool hasMfa)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
        var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var user = await userManager.FindByEmailAsync(email)
            ?? await userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            user = new AppUser
            {
                Id = userId != Guid.Empty ? userId : Guid.CreateVersion7(),
                UserName = email,
                Email = email,
                DisplayName = email.Split('@')[0],
                EmailConfirmed = true,
                TwoFactorEnabled = hasMfa
            };
            var createRes = await userManager.CreateAsync(user, "Password!123456");
            if (!createRes.Succeeded)
            {
                throw new InvalidOperationException($"Failed to create user: {string.Join(", ", createRes.Errors.Select(e => e.Description))}");
            }
            await userManager.AddToRoleAsync(user, role);
        }

        var (session, _) = await sessionService.CreateSessionAsync(user.Id, rememberMe: true, "127.0.0.1", "test-agent", hasMfa, default);

        var roles = await userManager.GetRolesAsync(user);
        var permissions = new List<string>();
        foreach (var r in roles)
        {
            var roleObj = await roleManager.FindByNameAsync(r);
            if (roleObj != null)
            {
                var claims = await roleManager.GetClaimsAsync(roleObj);
                permissions.AddRange(claims.Where(c => c.Type == "permission").Select(c => c.Value));
            }
        }

        return tokenService.GenerateAccessToken(user, session, roles.ToList(), permissions, TimeProvider.GetUtcNow());
    }

    public new async ValueTask DisposeAsync()
    {
        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
