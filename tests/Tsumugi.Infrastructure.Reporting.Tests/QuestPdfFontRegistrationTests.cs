using System.Reflection;
using FluentAssertions;
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
}
