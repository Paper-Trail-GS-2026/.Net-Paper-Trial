using Microsoft.EntityFrameworkCore;
using PaperTrail.Domain.Entities;

namespace PaperTrail.Infrastructure.Persistence;

public sealed class PaperTrailContext(DbContextOptions<PaperTrailContext> options) : DbContext(options)
{
    public DbSet<Preparation> Preparations => Set<Preparation>();
    public DbSet<PreparationItem> PreparationItems => Set<PreparationItem>();
    public DbSet<ServiceVersion> ServiceVersions => Set<ServiceVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaperTrailContext).Assembly);
    }
}
