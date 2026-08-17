using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class OfficeFormViewModelBaseTests
{
    [Theory]
    // 事業所フォームの8項目は OfficeViewModel と FirstRunWizardViewModel に一字一句同じ形で
    // 二重にあった。NullIfEmpty はさらに DailyRecordViewModel にも3つ目のコピーがあった。
    // どれも片方だけ直しても気付けない。
    [InlineData("_representativeTitleAndName = string.Empty;", "OfficeFormViewModelBase.cs")]
    [InlineData("_region = RegionGrade.None;", "OfficeFormViewModelBase.cs")]
    // 引数名に依存しないニードル（コピーは value / s と名前が割れていた）。
    [InlineData("string? NullIfEmpty(string?", "InputText.cs")]
    public void Shared_input_state_is_declared_in_exactly_one_place(
        string declaration, string expectedFile)
    {
        var viewModelDir = Path.Combine(RepositoryPaths.AppProject, "ViewModels");

        var definers = Directory
            .EnumerateFiles(viewModelDir, "*.cs", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file).Contains(declaration, StringComparison.Ordinal))
            .Select(file => Path.GetFileName(file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        definers.Should().Equal([expectedFile],
            because: $"'{declaration}' の宣言箇所は1つ。実際: " + string.Join(", ", definers));
    }

    [Fact]
    public void Blank_and_whitespace_only_optional_input_becomes_null()
    {
        var sut = new ProbeFormViewModel();

        sut.PostalCode = "   ";

        sut.OptionalPostalCode.Should().BeNull();
    }

    [Fact]
    public void Filled_optional_input_is_passed_through_unchanged()
    {
        var sut = new ProbeFormViewModel { PostalCode = "100-0001" };

        sut.OptionalPostalCode.Should().Be("100-0001");
    }

    [Fact]
    public void ResetForm_clears_every_input_back_to_its_initial_value()
    {
        var sut = new ProbeFormViewModel
        {
            OfficeNumber = "1234567890",
            Name = "つむぎ作業所",
            Region = RegionGrade.Grade4,
            PostalCode = "100-0001",
            Address = "東京都千代田区1-1",
            PhoneNumber = "03-1234-5678",
            RepresentativeTitleAndName = "管理者 山田太郎",
        };

        sut.Reset();

        sut.OfficeNumber.Should().BeEmpty();
        sut.Name.Should().BeEmpty();
        sut.Category.Should().Be(ServiceCategory.TypeB);
        sut.Region.Should().Be(RegionGrade.None);
        sut.PostalCode.Should().BeEmpty();
        sut.Address.Should().BeEmpty();
        sut.PhoneNumber.Should().BeEmpty();
        sut.RepresentativeTitleAndName.Should().BeEmpty();
    }

    // protected メンバを外から突くためだけの派生型。
    private sealed class ProbeFormViewModel : OfficeFormViewModelBase
    {
        public string? OptionalPostalCode => OptionalPostalCodeInput;

        public void Reset() => ResetForm();
    }
}
