using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class TsumugiDbContext(DbContextOptions<TsumugiDbContext> options) : DbContext(options)
{
    public DbSet<Office> Offices => Set<Office>();
    public DbSet<Recipient> Recipients => Set<Recipient>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<ContractedProvider> ContractedProviders => Set<ContractedProvider>();
    public DbSet<DisabilityCertificate> DisabilityCertificates => Set<DisabilityCertificate>();
    public DbSet<FaceSheet> FaceSheets => Set<FaceSheet>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<OfficeCapability> OfficeCapabilities => Set<OfficeCapability>();
    public DbSet<DailyRecord> DailyRecords => Set<DailyRecord>();

    // Phase 2 - Wage calculation
    public DbSet<WorkRecord> WorkRecords => Set<WorkRecord>();
    public DbSet<WageFund> WageFunds => Set<WageFund>();
    public DbSet<WageSettings> WageSettings => Set<WageSettings>();
    public DbSet<WageStatement> WageStatements => Set<WageStatement>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    // Phase 3 - Claim finalization snapshots
    public DbSet<ClaimBatch> ClaimBatches => Set<ClaimBatch>();
    public DbSet<ClaimDetail> ClaimDetails => Set<ClaimDetail>();

    // Phase 4 - KouchinModule
    public DbSet<WageAdjustment> WageAdjustments => Set<WageAdjustment>();
    public DbSet<RecipientHourlyRate> RecipientHourlyRates => Set<RecipientHourlyRate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TsumugiDbContext).Assembly);
    }

    // NOTE: bool overload を「正本」として override する。
    // EF Core の SaveChanges() / SaveChangesAsync(CancellationToken) は内部で bool overload を仮想呼び出しするため、
    // ここを覆えば全 SaveChanges 経路が AppendOnlyGuard と更新トークン回転を通る。
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AppendOnlyGuard.Inspect(ChangeTracker);
        RotateConcurrencyTokens();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AppendOnlyGuard.Inspect(ChangeTracker);
        RotateConcurrencyTokens();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
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
