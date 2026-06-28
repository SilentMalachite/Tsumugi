using Microsoft.Extensions.DependencyInjection;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases;
using Tsumugi.Application.UseCases.Certificate;
using Tsumugi.Application.UseCases.Contract;
using Tsumugi.Application.UseCases.DailyRecord;
using Tsumugi.Application.UseCases.Office;
using Tsumugi.Application.UseCases.OfficeCapability;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Application.UseCases.Wage;
using Tsumugi.Application.UseCases.WorkRecord;
using Tsumugi.Domain.Logic.Wage;
using Tsumugi.Infrastructure;
using Tsumugi.Infrastructure.Reporting;

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
        services.AddScoped<ArchiveRecipientUseCase>();
        services.AddScoped<RestoreRecipientUseCase>();
        services.AddScoped<RegisterDisabilityCertificateUseCase>();
        services.AddScoped<ListDisabilityCertificatesUseCase>();
        services.AddScoped<SaveFaceSheetUseCase>();
        services.AddScoped<GetLatestFaceSheetUseCase>();

        // Phase 1: 受給者証
        services.AddScoped<RegisterCertificateUseCase>();
        services.AddScoped<ListExpiringCertificatesUseCase>();
        services.AddScoped<ListCertificatesByRecipientUseCase>();
        services.AddScoped<RegisterContractedProviderUseCase>();
        services.AddScoped<ListContractedProvidersUseCase>();

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

        // Phase 2: 工賃計算戦略（4 方式並存; D3 CalculateWagesUseCase が IReadOnlyList<IWageMethodStrategy> を要求）
        services.AddSingleton<IReadOnlyList<IWageMethodStrategy>>(_ => new IWageMethodStrategy[]
        {
            new PieceWageStrategy(),
            new HourlyWageStrategy(),
            new FixedWageStrategy(),
            new EqualWageStrategy(),
        });

        // Phase 2: 作業実績
        services.AddScoped<RecordWorkUseCase>();
        services.AddScoped<CorrectWorkUseCase>();
        services.AddScoped<CancelWorkUseCase>();
        services.AddScoped<QueryMonthWorkUseCase>();

        // Phase 2: 工賃原資・設定・計算・確定
        services.AddScoped<SetWageFundUseCase>();
        services.AddScoped<ConfigureWageSettingsUseCase>();
        services.AddScoped<CalculateWagesUseCase>();
        services.AddScoped<CloseWagesUseCase>();
        services.AddScoped<QueryWageStatementUseCase>();

        // Phase 2: 帳票（E2/E3）
        services.AddScoped<IWageReportGenerator, WageStatementPdfGenerator>();

        // ViewModels
        services.AddTransient<RecipientListViewModel>();
        services.AddTransient<RecipientEditViewModel>();
        services.AddTransient<DisabilityCertificateViewModel>();
        services.AddTransient<FaceSheetViewModel>();
        services.AddTransient<CertificateViewModel>();
        services.AddTransient<ContractViewModel>();
        services.AddTransient<OfficeViewModel>();
        services.AddTransient<OfficeCapabilityViewModel>();
        services.AddTransient<DailyRecordViewModel>();
        // Phase 2 ViewModels
        services.AddTransient<WorkRecordViewModel>();
        services.AddTransient<WageFundSettingsViewModel>();
        services.AddTransient<WageCalculationViewModel>();
        services.AddTransient<WageStatementViewModel>();
        services.AddTransient<MainViewModel>();

        return services;
    }
}
