using Avalonia.Controls.ApplicationLifetimes;
using AvaloniaApplication = Avalonia.Application;

namespace Tsumugi.App.Services;

/// <summary>Avalonia のクラシックデスクトップライフタイムを介する <see cref="IApplicationShutdown"/> 実装。</summary>
public sealed class AvaloniaApplicationShutdown : IApplicationShutdown
{
    public void RequestShutdown()
    {
        // デザイン時・テスト時はライフタイムが取れないため何もしない。
        if (AvaloniaApplication.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
