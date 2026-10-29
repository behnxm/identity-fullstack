using System.Security.Claims;
using Identity.Api.Common.Constants;
using Identity.Api.Common.Options;
using Identity.Api.Persistence;
using Identity.Api.Persistence.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Api.Seeding;

/// <summary>
/// Idempotently seeds roles, role permission claims, and optional demo users.
/// </summary>
public sealed class DatabaseSeeder(
    RoleManager<AppRole> roleManager,
    UserManager<AppUser> userManager,
    IOptions<SeedOptions> seedOptions,
    IWebHostEnvironment environment,
    ILogger<DatabaseSeeder> logger)
{
    private readonly SeedOptions _options = seedOptions.Value;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Database seeding is disabled in configuration.");
            return;
        }

        logger.LogInformation("Beginning idempotent database seeding...");

        // 1. Seed Roles and Permission Claims
        foreach (var (roleName, permissions) in Roles.RolePermissions)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role == null)
            {
                role = new AppRole(roleName, $"Default system role for {roleName}");
                var createRoleResult = await roleManager.CreateAsync(role);
                if (!createRoleResult.Succeeded)
                {
                    logger.LogError("Failed to create role {RoleName}: {Errors}",
                        roleName, string.Join(", ", createRoleResult.Errors.Select(e => e.Description)));
                    continue;
                }
                logger.LogInformation("Created role: {RoleName}", roleName);
            }

            var existingClaims = await roleManager.GetClaimsAsync(role);
            var existingPermissions = existingClaims
                .Where(c => c.Type == "permission")
                .Select(c => c.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var permission in permissions)
            {
                if (!existingPermissions.Contains(permission))
                {
                    await roleManager.AddClaimAsync(role, new Claim("permission", permission));
                    logger.LogInformation("Added permission claim '{Permission}' to role '{RoleName}'", permission, roleName);
                }
            }
        }

        // 2. Demo Users (Guarded by environment and configuration)
        if (!_options.DemoUsers)
        {
            logger.LogInformation("Demo user seeding is disabled.");
            return;
        }

        if (environment.IsProduction() && !_options.AllowDemoInProduction)
        {
            logger.LogWarning("Refusing to seed demo users in Production environment.");
            return;
        }

        var demoPassword = Environment.GetEnvironmentVariable("SEED_DEMO_PASSWORD");
        if (string.IsNullOrWhiteSpace(demoPassword))
        {
            demoPassword = "Password!12345678"; // Safe fallback for local automated test runs
        }

        var demoUsers = new (string Email, string Name, string Role)[]
        {
            ("user@demo.test", "Demo User", Roles.User),
            ("support@demo.test", "Demo Support", Roles.Support),
            ("admin@demo.test", "Demo Admin", Roles.Admin),
            ("user@identity.local", "Local User", Roles.User),
            ("support@identity.local", "Local Support", Roles.Support),
            ("admin@identity.local", "Local Admin", Roles.Admin)
        };

        foreach (var (email, name, role) in demoUsers)
        {
            var existingUser = await userManager.FindByEmailAsync(email);
            if (existingUser == null)
            {
                var user = new AppUser
                {
                    UserName = email,
                    Email = email,
                    DisplayName = name,
                    EmailConfirmed = true, // Demo accounts have confirmed emails
                    TwoFactorEnabled = false // No 2FA pre-enrolled to demonstrate forced enrollment flow
                };

                var createResult = await userManager.CreateAsync(user, demoPassword);
                if (!createResult.Succeeded)
                {
                    logger.LogError("Failed to create demo user {Email}: {Errors}",
                        email, string.Join(", ", createResult.Errors.Select(e => e.Description)));
                    continue;
                }

                await userManager.AddToRoleAsync(user, role);
                logger.LogInformation("Seeded demo user {Email} in role {Role}", email, role);
            }
            else
            {
                if (!await userManager.IsInRoleAsync(existingUser, role))
                {
                    await userManager.AddToRoleAsync(existingUser, role);
                }
            }
        }

        logger.LogInformation("Database seeding completed successfully.");
    }
}
