using Microsoft.Extensions.DependencyInjection;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.UseCases;
using Tsumugi.Application.UseCases.Certificate;
using Tsumugi.Application.UseCases.Contract;
using Tsumugi.Application.UseCases.DailyRecord;
using Tsumugi.Application.UseCases.Office;
using Tsumugi.Application.UseCases.OfficeCapability;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Infrastructure;

namespace Tsumugi.App;

/// <summary>アプリ全体のDI構成を一点に集約する合成ルート。テストからも同じ構成を再現できる。</summary>
public static class CompositionRoot
{
    public static IServiceProvider Build(string connectionString)
        => new ServiceCollection().AddTsumugiServices(connectionString).BuildServiceProvider();

    public static IServiceCollection AddTsumugiServices(
        this IServiceCollection services, string connectionString)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddTsumugiInfrastructure(connectionString);

        // Phase 0: 事業所・バックアップ
        services.AddScoped<RegisterOfficeUseCase>();
        services.AddScoped<BackupDatabaseUseCase>();

        // Phase 1: 事業所 (更新・一覧)
        services.AddScoped<UpdateOfficeUseCase>();
        services.AddScoped<ListOfficesUseCase>();

        // Phase 1: 受給者
        services.AddScoped<RegisterRecipientUseCase>();
        services.AddScoped<UpdateRecipientUseCase>();
        services.AddScoped<ListRecipientsUseCase>();

        // Phase 1: 受給者証
        services.AddScoped<RegisterCertificateUseCase>();
        services.AddScoped<ListExpiringCertificatesUseCase>();

        // Phase 1: 契約
        services.AddScoped<RegisterContractUseCase>();
        services.AddScoped<ListContractsByRecipientUseCase>();

        // Phase 1: 事業所機能
        services.AddScoped<RegisterOfficeCapabilityUseCase>();
        services.AddScoped<ListOfficeCapabilitiesUseCase>();

        // Phase 1: 日次記録
        services.AddScoped<RecordDailyRecordUseCase>();
        services.AddScoped<CorrectDailyRecordUseCase>();
        services.AddScoped<CancelDailyRecordUseCase>();
        services.AddScoped<QueryMonthDailyRecordsUseCase>();

        // ViewModels
        services.AddTransient<RecipientListViewModel>();
        services.AddTransient<RecipientEditViewModel>();
        services.AddTransient<CertificateViewModel>();
        services.AddTransient<ContractViewModel>();
        services.AddTransient<OfficeViewModel>();
        services.AddTransient<OfficeCapabilityViewModel>();
        services.AddTransient<DailyRecordViewModel>();
        services.AddTransient<MainViewModel>();

        return services;
    }
}
