using Microsoft.EntityFrameworkCore;
using Sharenima.Server.Models;
using Sharenima.Shared;
using Sharenima.Shared.Queue;

namespace Sharenima.Server.Data;

public class GeneralDbContext : DbContext {
    public DbSet<Instance> Instances { get; set; }
    public DbSet<Queue> Queues { get; set; }
    public DbSet<Settings> Settings { get; set; }
    public DbSet<InstancePermission> InstancePermissions { get; set; }
    public DbSet<QueueSubtitles> QueueSubtitles { get; set; }

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
        modelBuilder.Entity<QueueSubtitles>(
            typeBuilder => {
                typeBuilder.HasOne(queueSubtitle => queueSubtitle.Queue)
                    .WithMany(queue => queue.Subtitles)
                    .HasForeignKey(queueSubtitles => queueSubtitles.QueueId)
                    .IsRequired();
            });
        modelBuilder.Entity<QueueSubtitles>()
            .HasKey(queueSubtitles => queueSubtitles.Id);
    }
}