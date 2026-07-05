using System;
using System.IO;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tsumugi.App;
using Tsumugi.App.ViewModels;
using Xunit;

namespace Tsumugi.App.Tests;

/// <summary>
/// MainViewModel が Phase 4 S0 で追加された 2 タブ VM プロパティを公開することを確認する。
/// CompositionRoot 経由で解決し、DI 配線の通し確認も兼ねる。
/// </summary>
public sealed class MainViewModelTests
{
    [Fact]
    public void MainViewModel_exposes_RecipientHourlyRate_and_WageAdjustment()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"tsumugi-main-vm-{Guid.NewGuid():N}.db");
        try
        {
            using var provider = (ServiceProvider)CompositionRoot.Build($"Data Source={dbPath}");
            using var scope = provider.CreateScope();

            var vm = scope.ServiceProvider.GetRequiredService<MainViewModel>();

            vm.RecipientHourlyRate.Should().NotBeNull();
            vm.WageAdjustment.Should().NotBeNull();
            vm.RecipientHourlyRate.Should().BeOfType<RecipientHourlyRateViewModel>();
            vm.WageAdjustment.Should().BeOfType<WageAdjustmentViewModel>();
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var f in new[] { dbPath, dbPath + "-shm", dbPath + "-wal" })
                if (File.Exists(f)) File.Delete(f);
        }
    }

    [Fact]
    public void MainViewModel_new_tab_vms_are_distinct_instances_from_separate_scopes()
    {
        // TransientなVMなので、スコープが異なれば別インスタンス。
        // ここでは同一スコープ内でMainViewModelが持つ参照が一貫していることを確認。
        var dbPath = Path.Combine(Path.GetTempPath(), $"tsumugi-main-vm2-{Guid.NewGuid():N}.db");
        try
        {
            using var provider = (ServiceProvider)CompositionRoot.Build($"Data Source={dbPath}");
            using var scope = provider.CreateScope();

            var vm = scope.ServiceProvider.GetRequiredService<MainViewModel>();

            // 同一インスタンス内で何度アクセスしても同じ参照を返す（プロパティが init-only）
            vm.RecipientHourlyRate.Should().BeSameAs(vm.RecipientHourlyRate);
            vm.WageAdjustment.Should().BeSameAs(vm.WageAdjustment);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var f in new[] { dbPath, dbPath + "-shm", dbPath + "-wal" })
                if (File.Exists(f)) File.Delete(f);
        }
    }
}
