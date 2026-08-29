using Microsoft.EntityFrameworkCore;
using TaskPriorityApi.Models;

namespace TaskPriorityApi.Data;

public class TaskContext(DbContextOptions<TaskContext> options) : DbContext(options)
{
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<ModelMetrics> Metrics => Set<ModelMetrics>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TaskItem>(e =>
        {
            e.Property(t => t.Title).IsRequired().HasMaxLength(500);
            e.Property(t => t.Tags).HasMaxLength(500);
            e.Property(t => t.Score).HasColumnType("REAL");
        });
    }
}
