using Identity.Api.Persistence.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Persistence;

/// <summary>
/// Application database context with Identity stores, sessions, audit events, notes, and Data Protection keys.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<
        AppUser,
        AppRole,
        Guid,
        IdentityUserClaim<Guid>,
        IdentityUserRole<Guid>,
        IdentityUserLogin<Guid>,
        IdentityRoleClaim<Guid>,
        IdentityUserToken<Guid>>(options),
      IDataProtectionKeyContext
{
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<MfaChallenge> MfaChallenges => Set<MfaChallenge>();
    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Rename Identity tables to snake_case / clean table names
        builder.Entity<AppUser>(b =>
        {
            b.ToTable("users");
            b.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
            b.HasIndex(u => u.NormalizedEmail).IsUnique();
        });

        builder.Entity<AppRole>(b =>
        {
            b.ToTable("roles");
            b.Property(r => r.Description).HasMaxLength(256);
        });

        builder.Entity<IdentityUserRole<Guid>>(b => b.ToTable("user_roles"));
        builder.Entity<IdentityUserClaim<Guid>>(b => b.ToTable("user_claims"));
        builder.Entity<IdentityUserLogin<Guid>>(b => b.ToTable("user_logins"));
        builder.Entity<IdentityRoleClaim<Guid>>(b =>
        {
            b.ToTable("role_claims");
            b.HasIndex(rc => new { rc.RoleId, rc.ClaimType, rc.ClaimValue });
        });
        builder.Entity<IdentityUserToken<Guid>>(b => b.ToTable("user_tokens"));

        // User sessions
        builder.Entity<UserSession>(b =>
        {
            b.ToTable("user_sessions");
            b.HasKey(s => s.Id);
            b.Property(s => s.RevocationReason).HasMaxLength(100);
            b.Property(s => s.IpAddress).HasMaxLength(45); // IPv6 max length
            b.Property(s => s.UserAgent).HasMaxLength(500);

            b.HasIndex(s => s.UserId);
            b.HasIndex(s => s.ExpiresAt);
            b.HasIndex(s => new { s.UserId, s.RevokedAt });

            b.HasOne(s => s.User)
                .WithMany(u => u.Sessions)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Refresh tokens
        builder.Entity<RefreshToken>(b =>
        {
            b.ToTable("refresh_tokens");
            b.HasKey(r => r.Id);
            b.Property(r => r.TokenHash).HasMaxLength(64).IsFixedLength().IsRequired();
            b.Property(r => r.ReplacedByTokenHash).HasMaxLength(64).IsFixedLength();

            b.HasIndex(r => r.TokenHash).IsUnique();
            b.HasIndex(r => r.SessionId);

            b.HasOne(r => r.Session)
                .WithMany(s => s.RefreshTokens)
                .HasForeignKey(r => r.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // MFA challenges
        builder.Entity<MfaChallenge>(b =>
        {
            b.ToTable("mfa_challenges");
            b.HasKey(c => c.Id);
            b.Property(c => c.TokenHash).HasMaxLength(64).IsFixedLength().IsRequired();

            b.HasIndex(c => c.TokenHash).IsUnique();
            b.HasIndex(c => new { c.UserId, c.ExpiresAt, c.IsConsumed });

            b.HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Security audit events
        builder.Entity<SecurityEvent>(b =>
        {
            b.ToTable("security_events");
            b.HasKey(e => e.Id);
            b.Property(e => e.EventType).HasMaxLength(100).IsRequired();
            b.Property(e => e.IpAddress).HasMaxLength(45);
            b.Property(e => e.UserAgent).HasMaxLength(500);

            b.HasIndex(e => e.Timestamp);
            b.HasIndex(e => e.EventType);
            b.HasIndex(e => e.UserId);

            b.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Notes (domain entity for resource-based authorization)
        builder.Entity<Note>(b =>
        {
            b.ToTable("notes");
            b.HasKey(n => n.Id);
            b.Property(n => n.Title).HasMaxLength(200).IsRequired();
            b.Property(n => n.Content).HasMaxLength(10000).IsRequired();

            b.HasIndex(n => n.OwnerId);
            b.HasIndex(n => n.CreatedAt);

            b.HasOne(n => n.Owner)
                .WithMany(u => u.Notes)
                .HasForeignKey(n => n.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Data protection keys
        builder.Entity<DataProtectionKey>(b =>
        {
            b.ToTable("data_protection_keys");
        });
    }
}
