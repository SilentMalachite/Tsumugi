using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tsumugi.App.Settings;
using Tsumugi.App.ViewModels;
using Tsumugi.Infrastructure.Persistence;
using Tsumugi.Infrastructure.Reporting;
using AvaloniaApplication = Avalonia.Application;

namespace Tsumugi.App;

public partial class App : AvaloniaApplication
{
    private IServiceProvider? _services;
    private IServiceScope? _appScope;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        // CLAUDE.md §ハード制約 5: アクセシビリティ既定（テーマ・低アニメーション・フォント拡大追従）。
        AccessibilityDefaults.Apply(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // ADR 0013: QuestPDF Community ライセンスをコードで固定する（オフライン保証）。
        QuestPdfLicenseConfigurator.Initialize();

        var appDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Tsumugi");

        var location = new SqliteLocationService(appDataRoot);
        location.EnsureSecuredStorage();

        _services = CompositionRoot.Build(location.ConnectionString);

        // アプリ全体で一つのスコープを維持する（ScopedサービスをTransient VMが参照できるよう）
        _appScope = _services.CreateScope();

        // 起動時に EF Core マイグレーションを適用（未適用分のみ実行）
        _appScope.ServiceProvider.GetRequiredService<TsumugiDbContext>().Database.Migrate();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = _appScope.ServiceProvider.GetRequiredService<MainViewModel>();
            desktop.MainWindow = new MainWindow(mainVm);
            desktop.ShutdownRequested += (_, _) => _appScope?.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
