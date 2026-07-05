using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Tsumugi.App.Tests;

/// <summary>
/// 事業所選択や利用者選択を必要とする View が、画面ライフサイクル（Loaded）から
/// ViewModel.InitializeAsync を呼ぶ配線になっていることをコードビハインドのテキストスキャンで担保する。
/// 配線が消えると ComboBox が実画面で空のまま運用に出てしまう。
/// </summary>
public sealed class ViewLifecycleWiringTests
{
    private static readonly string[] ViewsRequiringInitialize =
    {
        "ContractView.axaml.cs",
        "DailyRecordView.axaml.cs",
        "WageFundSettingsView.axaml.cs",
        "WageCalculationView.axaml.cs",
        "WageStatementView.axaml.cs",
        // Phase 4 S0
        "RecipientHourlyRateView.axaml.cs",
        "WageAdjustmentView.axaml.cs",
    };

    [Theory]
    [InlineData("ContractView.axaml.cs")]
    [InlineData("DailyRecordView.axaml.cs")]
    [InlineData("WageFundSettingsView.axaml.cs")]
    [InlineData("WageCalculationView.axaml.cs")]
    [InlineData("WageStatementView.axaml.cs")]
    [InlineData("RecipientHourlyRateView.axaml.cs")]
    [InlineData("WageAdjustmentView.axaml.cs")]
    public void View_code_behind_wires_InitializeAsync_to_Loaded(string viewFileName)
    {
        var path = LocateView(viewFileName);
        var text = File.ReadAllText(path);

        text.Should().Contain("Loaded", because: $"{viewFileName} は Loaded を購読して VM を初期化する必要がある");
        text.Should().Contain("InitializeAsync", because: $"{viewFileName} は VM.InitializeAsync を呼ぶ必要がある");
    }

    [Fact]
    public void All_views_requiring_recipient_load_are_covered()
    {
        // 万一スキャン対象配列を縮めて検査をすり抜けないよう、明示的に件数も pin。
        ViewsRequiringInitialize.Should().HaveCount(7);
    }

    private static string LocateView(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.EnumerateFiles("Tsumugi.sln").Any())
            {
                return Path.Combine(dir.FullName, "src", "Tsumugi.App", "Views", fileName);
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Tsumugi.sln が祖先方向に見つからない");
    }
}
