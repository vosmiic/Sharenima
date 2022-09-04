using Microsoft.EntityFrameworkCore;
using Sharenima.Server.Models;
using Sharenima.Shared;

namespace Sharenima.Server.Data;

public class GeneralDbContext : DbContext {
    public DbSet<Instance> Instances { get; set; }
    public DbSet<Queue> Queues { get; set; }
    public DbSet<Settings> Settings { get; set; }
    public DbSet<AdvancedRole> Roles { get; set; }

    public GeneralDbContext(
        DbContextOptions<GeneralDbContext> options) : base(options) {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.Entity<Instance>()
            .HasKey(i => i.Id);
        modelBuilder.Entity<Queue>()
            .HasKey(q => q.Id);
        modelBuilder.Entity<Settings>()
            .HasKey(q => q.Id);
    }
}