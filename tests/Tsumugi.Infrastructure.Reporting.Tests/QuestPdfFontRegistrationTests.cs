using System.Reflection;
using FluentAssertions;
using QuestPDF.Infrastructure;
using Xunit;

namespace Tsumugi.Infrastructure.Reporting.Tests;

public sealed class QuestPdfFontRegistrationTests
{
    private static readonly Assembly ReportingAssembly = typeof(QuestPdfLicenseConfigurator).Assembly;

    [Theory]
    [InlineData("Tsumugi.Infrastructure.Reporting.assets.fonts.NotoSansJP-Regular.otf")]
    [InlineData("Tsumugi.Infrastructure.Reporting.assets.fonts.NotoSansJP-Bold.otf")]
    [InlineData("Tsumugi.Infrastructure.Reporting.assets.fonts.NotoSansJP.LICENSE.txt")]
    public void Embedded_resource_is_present(string resourceName)
    {
        using var stream = ReportingAssembly.GetManifestResourceStream(resourceName);
        stream.Should().NotBeNull(because: $"{resourceName} は Reporting アセンブリの EmbeddedResource として登録済みのはず");
        stream!.Length.Should().BeGreaterThan(0, because: "リソースは空でないこと");
    }

    [Fact]
    public void Initialize_sets_community_license()
    {
        QuestPdfLicenseConfigurator.Initialize();
        QuestPDF.Settings.License.Should().Be(LicenseType.Community);
    }

    [Fact]
    public void Initialize_disables_environment_fonts()
    {
        QuestPdfLicenseConfigurator.Initialize();
        QuestPDF.Settings.UseEnvironmentFonts.Should().BeFalse(
            because: "CI と本番で挙動一致させるため環境フォントを抑止");
    }

    [Fact]
    public void Initialize_is_idempotent()
    {
        var action = () =>
        {
            QuestPdfLicenseConfigurator.Initialize();
            QuestPdfLicenseConfigurator.Initialize();
            QuestPdfLicenseConfigurator.Initialize();
        };
        action.Should().NotThrow(because: "冪等に呼べること");
    }

    [Fact]
    public void Family_name_constant_is_defined()
    {
        var name = typeof(QuestPdfLicenseConfigurator)
            .GetField("NotoSansJpFamilyName",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .GetValue(null) as string;
        name.Should().Be("Noto Sans JP", because: "登録した .otf の Family 名を定数化していること");
    }
}
