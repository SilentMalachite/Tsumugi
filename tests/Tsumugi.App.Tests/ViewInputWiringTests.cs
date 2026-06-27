using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Tsumugi.App.Tests;

/// <summary>
/// Phase 1 の View が、対応する ViewModel/DTO のプロパティを
/// 必ず画面入力として晒していることを XAML テキストスキャンで担保する。
/// VM や DTO に値があるのに画面で触れない、という Round 2/3 で頻発した抜けを再発防止するトリップワイヤ。
/// </summary>
public sealed class ViewInputWiringTests
{
    [Fact]
    public void OfficeView_exposes_ServiceCategory_and_RegionGrade_inputs()
    {
        var xml = ReadView("OfficeView.axaml");
        // OfficeViewModel の Category / Region に対する Binding が必須。
        xml.Should().Contain("{Binding Category}",
            because: "サービス種別の入力フィールドが画面に無いと AC1-1 を満たさない");
        xml.Should().Contain("{Binding Region}",
            because: "地域区分の入力フィールドが画面に無いと AC1-1 を満たさず、報酬算定の前提が画面で確定できない");
    }

    [Fact]
    public void ContractView_exposes_PeriodEnd_input()
    {
        var xml = ReadView("ContractView.axaml");
        // PeriodEnd は VM/UseCase で保存に使うが、画面に入力欄が無いと永久に null のまま保存される。
        xml.Should().Contain("{Binding PeriodEnd",
            because: "契約終了日の入力フィールドが画面に無いと AC1-1 を満たさない");
    }

    private static string ReadView(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.EnumerateFiles("Tsumugi.sln").Any())
            {
                return File.ReadAllText(
                    Path.Combine(dir.FullName, "src", "Tsumugi.App", "Views", fileName));
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Tsumugi.sln が祖先方向に見つからない");
    }
}
