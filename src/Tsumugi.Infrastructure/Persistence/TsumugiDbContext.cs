using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class TsumugiDbContext(DbContextOptions<TsumugiDbContext> options) : DbContext(options)
{
    public DbSet<Office> Offices => Set<Office>();
    public DbSet<Recipient> Recipients => Set<Recipient>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<OfficeCapability> OfficeCapabilities => Set<OfficeCapability>();
    public DbSet<DailyRecord> DailyRecords => Set<DailyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TsumugiDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RotateConcurrencyTokens();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        RotateConcurrencyTokens();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void RotateConcurrencyTokens()
    {
        // 楽観ロック: 追跡中の変更エンティティのトークンを保存時に更新する。
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Property(nameof(Entity.ConcurrencyToken)).CurrentValue = Guid.NewGuid();
            }
        }
    }
}
