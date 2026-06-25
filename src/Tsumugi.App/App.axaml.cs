using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Tsumugi.Infrastructure.Persistence;
using AvaloniaApplication = Avalonia.Application;

namespace Tsumugi.App;

public partial class App : AvaloniaApplication
{
    private IServiceProvider? _services;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var appDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Tsumugi");

        var location = new SqliteLocationService(appDataRoot);
        location.EnsureSecuredStorage();

        _services = CompositionRoot.Build(location.ConnectionString);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
