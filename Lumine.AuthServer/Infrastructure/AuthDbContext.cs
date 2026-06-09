using Lumine.AuthServer.Domain.Entities;
using Lumine.Services.Core.Infrastructure.DatabaseMigrations.Entities;
using Lumine.Services.Core.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Lumine.AuthServer.Infrastructure
{
    public class AuthDbContext : EFContext
    {
        public AuthDbContext(DbContextOptions<AuthDbContext> options, IMediator mediator)
            : base(options, mediator) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<UserRole> UserRoles => Set<UserRole>();
        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
        public DbSet<OidcClient> OidcClients => Set<OidcClient>();
        public DbSet<OidcClientRedirectUri> OidcClientRedirectUris => Set<OidcClientRedirectUri>();
        public DbSet<AuthorizationCode> AuthorizationCodes => Set<AuthorizationCode>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<DbVersion> DbVersions => Set<DbVersion>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User configuration
            modelBuilder.Entity<User>(b =>
            {
                b.ToTable("Users");
                b.HasKey(u => u.Id);
                b.Property(u => u.UserName).IsRequired().HasMaxLength(128);
                b.Property(u => u.Email).HasMaxLength(256);
                b.Property(u => u.NickName).HasMaxLength(128);
                b.Property(u => u.PhoneNumber).HasMaxLength(20);
                b.Property(u => u.CreatedAtUtc).IsRequired();
                b.Property(u => u.LastLoginAtUtc);

                // One-to-many with UserRole
                b.HasMany(u => u.UserRoles)
                    .WithOne(ur => ur.User)
                    .HasForeignKey(ur => ur.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Role configuration
            modelBuilder.Entity<Role>(b =>
            {
                b.ToTable("Roles");
                b.HasKey(r => r.Id);
                b.Property(r => r.Name).IsRequired().HasMaxLength(128);

                // One-to-many with UserRole
                b.HasMany(r => r.UserRoles)
                    .WithOne(ur => ur.Role)
                    .HasForeignKey(ur => ur.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);

                // One-to-many with RolePermission
                b.HasMany(r => r.RolePermissions)
                    .WithOne(rp => rp.Role)
                    .HasForeignKey(rp => rp.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Permission configuration
            modelBuilder.Entity<Permission>(b =>
            {
                b.ToTable("Permissions");
                b.HasKey(p => p.Id);
                b.Property(p => p.PermissionName).IsRequired().HasMaxLength(256);
                b.Property(p => p.DisplayName).HasMaxLength(256);

                // One-to-many with RolePermission
                b.HasMany<RolePermission>()
                    .WithOne(rp => rp.Permission)
                    .HasForeignKey(rp => rp.PermissionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // UserRole configuration (junction table)
            modelBuilder.Entity<UserRole>(b =>
            {
                b.ToTable("UserRoles");
                b.HasKey(ur => new { ur.UserId, ur.RoleId });
                
                b.HasOne(ur => ur.User)
                    .WithMany(u => u.UserRoles)
                    .HasForeignKey(ur => ur.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(ur => ur.Role)
                    .WithMany(r => r.UserRoles)
                    .HasForeignKey(ur => ur.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Add unique index
                b.HasIndex(ur => new { ur.UserId, ur.RoleId }).IsUnique();
            });

            // RolePermission configuration (junction table)
            modelBuilder.Entity<RolePermission>(b =>
            {
                b.ToTable("RolePermissions");
                b.HasKey(rp => new { rp.RoleId, rp.PermissionId });

                b.HasOne(rp => rp.Role)
                    .WithMany(r => r.RolePermissions)
                    .HasForeignKey(rp => rp.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(rp => rp.Permission)
                    .WithMany()
                    .HasForeignKey(rp => rp.PermissionId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Add unique index
                b.HasIndex(rp => new { rp.RoleId, rp.PermissionId }).IsUnique();
            });

            modelBuilder.Entity<OidcClient>(b =>
            {
                b.ToTable("OidcClients");
                b.HasKey(item => item.Id);
                b.Property(item => item.ClientId).IsRequired().HasMaxLength(128);
                b.Property(item => item.ClientName).IsRequired().HasMaxLength(256);
                b.Property(item => item.ClientType).IsRequired().HasMaxLength(32);
                b.Property(item => item.AllowedScopes).HasMaxLength(512);
                b.Property(item => item.Description).HasMaxLength(512);
                b.HasIndex(item => item.ClientId).IsUnique();
                b.HasMany(item => item.RedirectUris)
                    .WithOne(item => item.Client)
                    .HasForeignKey(item => item.OidcClientId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<OidcClientRedirectUri>(b =>
            {
                b.ToTable("OidcClientRedirectUris");
                b.HasKey(item => new { item.OidcClientId, item.RedirectUri });
                b.Property(item => item.RedirectUri).HasMaxLength(512);
            });

            modelBuilder.Entity<AuthorizationCode>(b =>
            {
                b.ToTable("AuthorizationCodes");
                b.HasKey(item => item.Id);
                b.Property(item => item.Code).IsRequired().HasMaxLength(128);
                b.Property(item => item.RedirectUri).IsRequired().HasMaxLength(512);
                b.Property(item => item.Scopes).HasMaxLength(512);
                b.Property(item => item.Nonce).HasMaxLength(256);
                b.Property(item => item.CodeChallenge).HasMaxLength(256);
                b.Property(item => item.CodeChallengeMethod).IsRequired().HasMaxLength(32);
                b.HasIndex(item => item.Code).IsUnique();
                b.HasIndex(item => item.ExpiresAtUtc);
                b.HasOne(item => item.Client)
                    .WithMany()
                    .HasForeignKey(item => item.OidcClientId)
                    .OnDelete(DeleteBehavior.Cascade);
                b.HasOne(item => item.User)
                    .WithMany()
                    .HasForeignKey(item => item.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<RefreshToken>(b =>
            {
                b.ToTable("RefreshTokens");
                b.HasKey(item => item.Id);
                b.Property(item => item.Token).IsRequired().HasMaxLength(128);
                b.Property(item => item.Scopes).HasMaxLength(512);
                b.HasIndex(item => item.Token).IsUnique();
                b.HasIndex(item => item.ExpiresAtUtc);
                b.HasOne(item => item.Client)
                    .WithMany()
                    .HasForeignKey(item => item.OidcClientId)
                    .OnDelete(DeleteBehavior.Cascade);
                b.HasOne(item => item.User)
                    .WithMany()
                    .HasForeignKey(item => item.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<DbVersion>(b =>
            {
                b.ToTable("DbVersions");
                b.Property<Guid>("Id");
                b.HasKey("Id");
                b.Property(item => item.Version).IsRequired().HasMaxLength(32);
            });
        }
    }
}
