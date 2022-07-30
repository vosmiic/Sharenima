using Microsoft.EntityFrameworkCore;
using Sharenima.Server.Models;

namespace Sharenima.Server.Data;

public class GeneralDbContext : DbContext {
    public DbSet<Instance> Instances { get; set; }
    public GeneralDbContext(
        DbContextOptions<GeneralDbContext> options) : base(options) {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.Entity<Instance>()
            .HasKey(i => i.Id);
    }
}