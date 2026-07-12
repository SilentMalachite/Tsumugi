using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Audit;
using Tsumugi.Application.Claim;
using Tsumugi.Infrastructure.ClaimMasters;
using Tsumugi.Infrastructure.Persistence;

namespace Tsumugi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTsumugiInfrastructure(
        this IServiceCollection services, string connectionString)
    {
        var claimMasterProvider = JsonClaimMasterProvider.LoadEmbedded();
        services.AddSingleton<IClaimMasterProvider>(claimMasterProvider);
        services.TryAddSingleton(TimeProvider.System);
        services.AddDbContextFactory<TsumugiDbContext>(o => o.UseSqlite(connectionString));
        services.AddScoped<IOfficeRepository, OfficeRepository>();
        services.AddScoped<IRecipientRepository, RecipientRepository>();
        services.AddScoped<ICertificateRepository, CertificateRepository>();
        services.AddScoped<IContractedProviderRepository, ContractedProviderRepository>();
        services.AddScoped<IDisabilityCertificateRepository, DisabilityCertificateRepository>();
        services.AddScoped<IFaceSheetRepository, FaceSheetRepository>();
        services.AddScoped<IContractRepository, ContractRepository>();
        services.AddScoped<IOfficeCapabilityRepository, OfficeCapabilityRepository>();
        services.AddScoped<IDailyRecordRepository, DailyRecordRepository>();
        services.AddScoped<IWorkRecordRepository, WorkRecordRepository>();
        services.AddScoped<IWageFundRepository, WageFundRepository>();
        services.AddScoped<IWageSettingsRepository, WageSettingsRepository>();
        services.AddScoped<IWageStatementRepository, WageStatementRepository>();
        services.AddScoped<IAuditEntryRepository, AuditEntryRepository>();
        services.AddScoped<IClaimBatchRepository, ClaimBatchRepository>();
        services.AddScoped<IClaimInputRepository, ClaimInputRepository>();
        services.AddScoped<IIntensiveSupportEpisodeRepository, IntensiveSupportEpisodeRepository>();
        services.AddScoped<IAverageWageAnnualEvidenceRepository, AverageWageAnnualEvidenceRepository>();
        services.AddScoped<IOfficeClaimProfileRepository, OfficeClaimProfileRepository>();
        services.AddScoped<ICertificateClaimEvidenceRepository, CertificateClaimEvidenceRepository>();
        services.AddScoped<IUpperLimitManagementStatementRepository,
            UpperLimitManagementStatementRepository>();
        services.AddScoped<IWageAdjustmentRepository, WageAdjustmentRepository>();
        services.AddScoped<IRecipientHourlyRateRepository, RecipientHourlyRateRepository>();
        services.AddScoped<IAuditTrail, AuditTrail>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IBackupService, SqliteBackupService>();
        services.AddSingleton<IClaimFinalizationOperationRegistry, ClaimFinalizationOperationRegistry>();
        services.AddSingleton<IClaimAuditEntryFactory, ClaimAuditEntryFactory>();
        services.AddSingleton<IClaimSnapshotValidationCodecRegistry,
            UnavailableClaimSnapshotValidationCodecRegistry>();
        services.AddSingleton<IClaimFinalizationStore, ClaimFinalizationStore>();
        return services;
    }
}
