using System.Net;
using System.Security.Claims;
using Identity.Api.Admin;
using Identity.Api.Auditing;
using Identity.Api.Auth;
using Identity.Api.Common.Constants;
using Identity.Api.Common.Options;
using Identity.Api.Common.Results;
using Identity.Api.Email;
using Identity.Api.Me;
using Identity.Api.Middleware;
using Identity.Api.Notes;
using Identity.Api.Persistence;
using Identity.Api.Persistence.Entities;
using Identity.Api.Security.Authorization;
using Identity.Api.Security.Keys;
using Identity.Api.Security.Password;
using Identity.Api.Security.Sessions;
using Identity.Api.Security.Tokens;
using Identity.Api.Seeding;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Strongly-typed configuration options
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<UserSessionOptions>(builder.Configuration.GetSection(UserSessionOptions.SectionName));
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.SectionName));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<SecurityPolicyOptions>(builder.Configuration.GetSection(SecurityPolicyOptions.SectionName));

var securityOptions = builder.Configuration.GetSection(SecurityPolicyOptions.SectionName).Get<SecurityPolicyOptions>() ?? new();
var jwtConfig = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new();

// 2. Data persistence & PostgreSQL EF Core 10
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connectionString = config.GetConnectionString("Database")
        ?? "Host=127.0.0.1;Port=5432;Database=identity_dev;Username=postgres;Password=postgres";

    // Use Npgsql for Postgres, or InMemory if specified in testing configuration
    if (config["UseInMemoryDatabase"] == "true")
    {
        options.UseInMemoryDatabase("TestDb");
    }
    else
    {
        options.UseNpgsql(connectionString);
    }
});

// 3. ASP.NET Core Identity Core (No cookies, no MapIdentityApi, no UI package)
builder.Services.AddIdentityCore<AppUser>(options =>
{
    // Unique email is mandatory; email is the username
    options.User.RequireUniqueEmail = true;

    // NIST SP 800-63B password rules: min 12, max 128, NO composition rules
    options.Password.RequiredLength = 12;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;

    // Lockout settings: 5 failures, 15 minutes lockout
    options.Lockout.MaxFailedAccessAttempts = securityOptions.MaxFailedAccessAttempts;
    options.Lockout.DefaultLockoutTimeSpan = securityOptions.DefaultLockoutTimeSpan;
    options.Lockout.AllowedForNewUsers = true;

    // Token & Authenticator options
    options.Tokens.AuthenticatorIssuer = securityOptions.AuthenticatorIssuer;
})
.AddRoles<AppRole>()
.AddEntityFrameworkStores<AppDbContext>()
.AddSignInManager()
.AddDefaultTokenProviders()
.AddPasswordValidator<CustomPasswordValidator>();

// Configure PBKDF2 iteration count to at least OWASP recommendation (210,000 for SHA512)
builder.Services.Configure<PasswordHasherOptions>(options =>
{
    options.IterationCount = securityOptions.Pbkdf2Iterations;
});

// Configure email confirmation token lifespan to 1 hour
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(1);
});

// 4. Data Protection: persist encryption keys to PostgreSQL with fixed application name
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>()
    .SetApplicationName("IdentityFullstack");

// 5. Caching & TimeProvider
builder.Services.AddHybridCache();
builder.Services.AddSingleton(TimeProvider.System);

// 6. Security & Domain Services
builder.Services.AddSingleton<IKeyManagementService, KeyManagementService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<ISecurityAuditor, SecurityAuditor>();
builder.Services.AddSingleton<IEmailQueue, EmailQueue>();
builder.Services.AddTransient<IEmailSender, MailKitEmailSender>();
builder.Services.AddHostedService<EmailBackgroundService>();
builder.Services.AddSingleton<IBreachedPasswordChecker, StubBreachedPasswordChecker>();
builder.Services.AddScoped<DatabaseSeeder>();

// 7. JWT Bearer Authentication & Token Validation
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IKeyManagementService, TimeProvider>((options, keyManagementService, timeProvider) =>
    {
        // Disable automatic inbound claim mapping so claim names remain exact (e.g. sub, jti, role, permission)
        options.MapInboundClaims = false;
        options.SaveToken = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "name",
            RoleClaimType = "role",
            ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
            ValidTypes = ["at+jwt"],
            ValidateIssuer = true,
            ValidIssuer = jwtConfig.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtConfig.Audience,
            ValidateLifetime = true,
            ClockSkew = jwtConfig.ClockSkew,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            LifetimeValidator = (notBefore, expires, _, _) =>
            {
                var utcNow = timeProvider.GetUtcNow().UtcDateTime;
                if (notBefore.HasValue && notBefore.Value.Subtract(jwtConfig.ClockSkew) > utcNow)
                {
                    return false;
                }
                if (expires.HasValue && expires.Value.Add(jwtConfig.ClockSkew) < utcNow)
                {
                    return false;
                }
                return true;
            },
            IssuerSigningKeyResolver = (_, _, kid, _) =>
                keyManagementService.FindValidationKey(kid) is { } k ? [k] : []
        };

        // Real-time server-side session check with HybridCache
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var sidClaim = context.Principal?.FindFirstValue("sid");
                if (string.IsNullOrWhiteSpace(sidClaim) || !Guid.TryParse(sidClaim, out var sessionId))
                {
                    context.Fail("Invalid or missing session identifier (sid claim).");
                    return;
                }

                var sessionService = context.HttpContext.RequestServices.GetRequiredService<ISessionService>();
                var validation = await sessionService.ValidateSessionForTokenAsync(sessionId, context.HttpContext.RequestAborted);

                if (!validation.IsValid)
                {
                    context.Fail(validation.ErrorCode ?? AuthErrorCodes.Unauthenticated);
                }
            }
        };
    });

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// 8. Authorization Policies: Permission-based, resource-based, and MFA requirements
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, SecurityAuthorizationMiddlewareResultHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, MfaRequirementHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, AnyPermissionRequirementHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, NoteAccessAuthorizationHandler>();

builder.Services.AddAuthorizationBuilder()
    // Secure by default: Deny-by-default fallback policy requires authenticated user everywhere
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .SetDefaultPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())

    // Panel policies
    .AddPolicy(Policies.PanelUser, policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("email_verified", "true", "True")
              .RequireClaim("permission", Permissions.ProfileManage))

    .AddPolicy(Policies.PanelAdmin, policy =>
        policy.RequireAuthenticatedUser()
              .AddRequirements(
                  new MfaRequirement(),
                  new AnyPermissionRequirement(
                      Permissions.UsersRead,
                      Permissions.AuditRead,
                      Permissions.UsersManage,
                      Permissions.NotesModerate)))

    // Individual administrative policies
    .AddPolicy(Policies.UsersRead, policy =>
        policy.RequireAuthenticatedUser()
              .AddRequirements(new MfaRequirement())
              .RequireClaim("permission", Permissions.UsersRead))

    .AddPolicy(Policies.AuditRead, policy =>
        policy.RequireAuthenticatedUser()
              .AddRequirements(new MfaRequirement())
              .RequireClaim("permission", Permissions.AuditRead))

    .AddPolicy(Policies.UsersManage, policy =>
        policy.RequireAuthenticatedUser()
              .AddRequirements(new MfaRequirement())
              .RequireClaim("permission", Permissions.UsersManage))

    .AddPolicy(Policies.NotesModerate, policy =>
        policy.RequireAuthenticatedUser()
              .AddRequirements(new MfaRequirement())
              .RequireClaim("permission", Permissions.NotesModerate))

    // User note access policy
    .AddPolicy(Policies.NotesManageOwn, policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("permission", Permissions.NotesManageOwn));

// 9. Minimal API Validation (.NET 10 automatic validation) & Problem Details
builder.Services.AddValidation();
builder.Services.AddProblemDetails();

// 10. Rate Limiting
builder.Services.AddSecurityRateLimiting(builder.Configuration);

// 11. Built-in OpenAPI and Scalar documentation
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Identity Fullstack Reference API";
        document.Info.Version = "v1";
        document.Info.Description = "Production-grade reference implementation of authentication and authorization with ASP.NET Core 10 Identity, ES256 JWT, rotating refresh tokens, and server-evaluated policies.";

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Enter your ES256 access token."
        };

        return Task.CompletedTask;
    });
});

// Configure Kestrel limits
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.AddServerHeader = false;
    kestrel.Limits.MaxRequestBodySize = 100 * 1024; // 100 KB limit
});

var app = builder.Build();

// 12. Middleware Pipeline
// Forwarded headers from trusted BFF network only
var forwardedNetworks = builder.Configuration["Forwarded:TrustedNetworks"] ?? "172.16.0.0/12,127.0.0.1,::1";
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
    ForwardLimit = 1
};
forwardedOptions.KnownIPNetworks.Clear();
forwardedOptions.KnownProxies.Clear();

foreach (var net in forwardedNetworks.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
{
    if (net.Contains('/'))
    {
        var parts = net.Split('/');
        if (IPAddress.TryParse(parts[0], out var prefix) && int.TryParse(parts[1], out var mask))
        {
            forwardedOptions.KnownIPNetworks.Add(new System.Net.IPNetwork(prefix, mask));
        }
    }
    else if (IPAddress.TryParse(net, out var proxyIp))
    {
        forwardedOptions.KnownProxies.Add(proxyIp);
    }
}
app.UseForwardedHeaders(forwardedOptions);

app.UseMiddleware<SecurityHeadersMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseStatusCodePages();
app.UseExceptionHandler();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

// 13. Map Endpoint Feature Groups
app.MapAuthEndpoints();
app.MapMeEndpoints();
app.MapNotesEndpoints();
app.MapAdminEndpoints();

// 14. Optional Database Migration and Seeding at Startup
if (app.Configuration.GetValue<bool>("Database:Migrate") || app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (db.Database.IsRelational())
    {
        await db.Database.MigrateAsync();
    }
    else
    {
        await db.Database.EnsureCreatedAsync();
    }

    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedAsync();
}

app.Run();

// Export Program class for WebApplicationFactory in integration tests
public partial class Program { }
