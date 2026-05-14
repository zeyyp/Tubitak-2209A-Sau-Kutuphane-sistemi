using Microsoft.EntityFrameworkCore;
using TurnstileService.Models;

namespace TurnstileService.Data;

public class TurnstileDbContext : DbContext
{
    public TurnstileDbContext(DbContextOptions<TurnstileDbContext> options) : base(options) { }

    public DbSet<TurnstileEntryLogEntity> EntryLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TurnstileEntryLogEntity>()
            .HasIndex(e => e.StudentNumber);

        modelBuilder.Entity<TurnstileEntryLogEntity>()
            .HasIndex(e => e.TimestampUtc);
    }
}
