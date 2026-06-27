using FluentAssertions;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class NetworkArtifactRulesTests
{
    [Theory]
    [InlineData("ws2_32.dll")]
    [InlineData("WS2_32.DLL")]
    [InlineData("wininet")]
    [InlineData("winhttp.dll")]
    [InlineData("libcurl.4.dylib")]
    [InlineData("iphlpapi.dll")]
    public void Forbidden_native_libraries_are_detected(string dll)
    {
        NetworkArtifactRules.IsForbiddenPInvoke(dll).Should().BeTrue();
    }

    [Theory]
    [InlineData("kernel32.dll")]
    [InlineData("libsqlite3.dylib")]
    [InlineData("libSystem.dylib")]   // 紛らわしいが前方一致では libsystem_network のみ拒否
    public void Allowed_native_libraries_are_not_flagged(string dll)
    {
        NetworkArtifactRules.IsForbiddenPInvoke(dll).Should().BeFalse();
    }

    [Theory]
    [InlineData("https://example.com/api")]
    [InlineData("HTTP://example.com")]
    [InlineData("connect to ftp://files.example")]
    [InlineData("smtp://mail")]
    [InlineData("wss://socket")]
    public void Forbidden_url_schemes_are_detected_in_literals(string s)
    {
        NetworkArtifactRules.ContainsForbiddenUrl(s).Should().BeTrue();
    }

    [Theory]
    [InlineData("file:///Users/x/foo")]
    [InlineData("data:image/png;base64,...")]
    [InlineData("javascript:void(0)")]
    [InlineData("no URL here")]
    public void Local_or_inert_literals_are_not_flagged(string s)
    {
        NetworkArtifactRules.ContainsForbiddenUrl(s).Should().BeFalse();
    }
}
