using System.IO;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class AssemblyMetadataScannerTests
{
    [Theory]
    [InlineData("Https://example.com")]
    [InlineData("HTTPS://EXAMPLE.COM")]
    [InlineData("https://example.com")]
    [InlineData("HtTpS://mixed")]
    public void ContainsRawUtf16SubstringIgnoreCase_finds_ascii_case_insensitively(string haystackText)
    {
        // ヘイは UTF-16 LE で書き出した合成バイト列。実際の DLL でも UserString はこの形式で格納される。
        var path = Path.Combine(Path.GetTempPath(), $"meta-scan-{System.Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, Encoding.Unicode.GetBytes($"prefix-{haystackText}-suffix"));
        try
        {
            AssemblyMetadataScanner.ContainsRawUtf16SubstringIgnoreCase(path, "https://")
                .Should().BeTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("nothing here")]
    [InlineData("htp://typo")]
    [InlineData("file:///local")]
    public void ContainsRawUtf16SubstringIgnoreCase_returns_false_when_absent(string haystackText)
    {
        var path = Path.Combine(Path.GetTempPath(), $"meta-scan-{System.Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, Encoding.Unicode.GetBytes(haystackText));
        try
        {
            AssemblyMetadataScanner.ContainsRawUtf16SubstringIgnoreCase(path, "https://")
                .Should().BeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }
}
