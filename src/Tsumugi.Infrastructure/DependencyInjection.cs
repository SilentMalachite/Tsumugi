using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tsumugi.Application.Abstractions;
using Tsumugi.Infrastructure.Persistence;

namespace Tsumugi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTsumugiInfrastructure(
        this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<TsumugiDbContext>(o => o.UseSqlite(connectionString));
        services.AddScoped<IOfficeRepository, OfficeRepository>();
        services.AddScoped<IRecipientRepository, RecipientRepository>();
        services.AddScoped<ICertificateRepository, CertificateRepository>();
        services.AddScoped<IContractedProviderRepository, ContractedProviderRepository>();
        services.AddScoped<IDisabilityCertificateRepository, DisabilityCertificateRepository>();
        services.AddScoped<IFaceSheetRepository, FaceSheetRepository>();
        services.AddScoped<IContractRepository, ContractRepository>();
        services.AddScoped<IOfficeCapabilityRepository, OfficeCapabilityRepository>();
        services.AddScoped<IDailyRecordRepository, DailyRecordRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IBackupService, SqliteBackupService>();
        return services;
    }
}
