using FluentAssertions;
using Tsumugi.Domain.Enums;
using Tsumugi.Infrastructure.Csv.Specifications;

namespace Tsumugi.Infrastructure.Csv.Tests.Specifications;

/// <summary>
/// 地域区分コード（<c>provider:J121:01:010</c>）は共通編のコード一覧から解決する。
/// コードは級地番号のゼロ詰めではない（一級地=11 … 七級地=17 / その他=23）。
/// </summary>
public sealed class RegionClassificationCodeCatalogTests
{
    // NOTE(teeth): 級地番号のゼロ詰め（01..07）へ戻すとここが RED になる。"06" は公式コードに存在しない。
    [Theory]
    [InlineData(RegionGrade.Grade1, "11")]
    [InlineData(RegionGrade.Grade2, "12")]
    [InlineData(RegionGrade.Grade3, "13")]
    [InlineData(RegionGrade.Grade4, "14")]
    [InlineData(RegionGrade.Grade5, "15")]
    [InlineData(RegionGrade.Grade6, "16")]
    [InlineData(RegionGrade.Grade7, "17")]
    [InlineData(RegionGrade.Other, "23")]
    public void Each_region_grade_resolves_to_its_official_code(RegionGrade grade, string expected)
    {
        RegionClassificationCodeCatalog.Instance.TryResolve(grade, out var code).Should().BeTrue();
        code.Should().Be(expected);
    }

    [Fact]
    public void An_unset_region_grade_has_no_code()
    {
        RegionClassificationCodeCatalog.Instance.TryResolve(RegionGrade.None, out _).Should().BeFalse();
    }

    [Fact]
    public void The_zero_padded_grade_numbers_are_not_official_codes()
    {
        var codes = RegionClassificationCodeCatalog.Instance.AllCodes;

        // 06 / 07 は公式コード表に存在しない（06 は誤って出力していた値）。
        codes.Should().NotContain("06").And.NotContain("07");
        codes.Should().Contain("16").And.Contain("17").And.Contain("23");
    }

    [Fact]
    public void Every_code_is_two_ascii_digits_and_unique()
    {
        var codes = RegionClassificationCodeCatalog.Instance.AllCodes;

        codes.Should().OnlyHaveUniqueItems();
        codes.Should().OnlyContain(code => code.Length == 2 && code.All(char.IsAsciiDigit));
        codes.Should().HaveCount(18);
    }
}
