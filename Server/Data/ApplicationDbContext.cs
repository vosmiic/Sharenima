using Microsoft.AspNetCore.ApiAuthorization.IdentityServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Duende.IdentityServer.EntityFramework.Options;
using Sharenima.Server.Models;

namespace Sharenima.Server.Data;

public class ApplicationDbContext : ApiAuthorizationDbContext<ApplicationUser> {
    public DbSet<AdvancedRole> AdvancedRoles { get; set; }

    public ApplicationDbContext(
        DbContextOptions options,
        IOptions<OperationalStoreOptions> operationalStoreOptions) : base(options, operationalStoreOptions) {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(
            typeBuilder => {
                typeBuilder.HasMany(host => host.Roles)
                    .WithOne(guest => guest.User)
                    .HasForeignKey(guest => guest.UserId)
                    .IsRequired();
            });

        modelBuilder.Entity<AdvancedRole>(
            typeBuilder => {
                typeBuilder.HasOne(guest => guest.User)
                    .WithMany(host => host.Roles)
                    .HasForeignKey(guest => guest.UserId)
                    .IsRequired();
            });
        modelBuilder.Entity<AdvancedRole>()
            .HasKey(i => i.Id);
    }
}