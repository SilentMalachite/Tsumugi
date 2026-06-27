using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Tsumugi.App.Settings;
using Tsumugi.App.ViewModels;
using Tsumugi.Infrastructure.Persistence;
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
        var appDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Tsumugi");

        var location = new SqliteLocationService(appDataRoot);
        location.EnsureSecuredStorage();

        _services = CompositionRoot.Build(location.ConnectionString);

        // アプリ全体で一つのスコープを維持する（ScopedサービスをTransient VMが参照できるよう）
        _appScope = _services.CreateScope();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = _appScope.ServiceProvider.GetRequiredService<MainViewModel>();
            desktop.MainWindow = new MainWindow(mainVm);
            desktop.ShutdownRequested += (_, _) => _appScope?.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
