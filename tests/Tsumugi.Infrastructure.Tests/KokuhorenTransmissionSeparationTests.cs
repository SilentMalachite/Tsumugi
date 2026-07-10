using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

/// <summary>
/// CLAUDE.md §ハード制約 2「国保連は生成のみ」を機械判定する。
/// 伝送・電子証明書・送信系のシンボル/語彙が Tsumugi.* 本番アセンブリに混入していないことを検査する。
/// 違反したらスコープ逸脱として扱い、別アプリ/別ライブラリに切り出すこと。
/// </summary>
public sealed class KokuhorenTransmissionSeparationTests
{
    // 伝送・電子証明書・電子署名・メール送信を直接想起させる型。
    // CLR の TypeRef レベルで現れたら違反候補（推移閉包は対象外、直接参照のみ）。
    private static readonly string[] ForbiddenTransmissionTypes =
    {
        "System.Security.Cryptography.X509Certificates.X509Certificate",
        "System.Security.Cryptography.X509Certificates.X509Certificate2",
        "System.Security.Cryptography.X509Certificates.X509Store",
        "System.Security.Cryptography.Xml.SignedXml",
        "System.Security.Cryptography.Pkcs.SignedCms",
        "System.Security.Cryptography.Pkcs.EnvelopedCms",
        "System.Net.Mail.SmtpClient",
        "System.Net.Mail.MailMessage",
    };

    // 公式伝送系を強く示唆する固有語彙（汎用語「伝送」「証明書」は誤検出が多いため採用しない）。
    private static readonly string[] ForbiddenTransmissionKeywords =
    {
        "電子請求受付システム",
        "取込・送信システム",
    };

    // やむを得ない参照は (Symbol, Reason) を埋めること。Reason 空は CI 違反。
    private static readonly (string Symbol, string Reason)[] Allowlist =
        Array.Empty<(string, string)>();

    [Fact]
    public void Transmission_type_check_covers_all_production_assemblies()
    {
        InlineDataCoverage.AssertMethodCovers(
            typeof(KokuhorenTransmissionSeparationTests),
            nameof(Tsumugi_assemblies_do_not_reference_transmission_types),
            InlineDataCoverage.AllProductionAssemblies);
    }

    [Theory]
    [InlineData("Tsumugi.App")]
    [InlineData("Tsumugi.Domain")]
    [InlineData("Tsumugi.Application")]
    [InlineData("Tsumugi.Infrastructure")]
    [InlineData("Tsumugi.Infrastructure.Reporting")]
    [InlineData("Tsumugi.Infrastructure.Csv")]
    public void Tsumugi_assemblies_do_not_reference_transmission_types(string assemblyName)
    {
        var dll = TsumugiAssemblyLocator.LocateProductionDll(assemblyName);
        var refs = AssemblyMetadataScanner.ScanReferencedTypeFullNames(dll);

        AssertAllowlistHasReasons();
        var allowed = Allowlist.Select(a => a.Symbol).ToHashSet(StringComparer.Ordinal);

        var hits = refs
            .Where(name => ForbiddenTransmissionTypes.Contains(name, StringComparer.Ordinal))
            .Where(name => !allowed.Contains(name))
            .ToArray();

        hits.Should().BeEmpty(
            because: $"{assemblyName} に国保連伝送系の型が混入している。" +
                     "請求は CSV 生成までが責務、伝送はアプリ範囲外（CLAUDE.md §ハード制約 2）。" +
                     Environment.NewLine +
                     "違反: " + string.Join(", ", hits));
    }

    [Fact]
    public void Transmission_keyword_check_covers_all_production_assemblies()
    {
        InlineDataCoverage.AssertMethodCovers(
            typeof(KokuhorenTransmissionSeparationTests),
            nameof(Tsumugi_assemblies_do_not_embed_transmission_keyword_literals),
            InlineDataCoverage.AllProductionAssemblies);
    }

    [Theory]
    [InlineData("Tsumugi.App")]
    [InlineData("Tsumugi.Domain")]
    [InlineData("Tsumugi.Application")]
    [InlineData("Tsumugi.Infrastructure")]
    [InlineData("Tsumugi.Infrastructure.Reporting")]
    [InlineData("Tsumugi.Infrastructure.Csv")]
    public void Tsumugi_assemblies_do_not_embed_transmission_keyword_literals(string assemblyName)
    {
        var dll = TsumugiAssemblyLocator.LocateProductionDll(assemblyName);
        AssertAllowlistHasReasons();
        var allowed = Allowlist.Select(a => a.Symbol).ToHashSet(StringComparer.Ordinal);

        var hits = new List<string>();
        foreach (var kw in ForbiddenTransmissionKeywords)
        {
            if (allowed.Contains(kw)) continue;
            if (AssemblyMetadataScanner.ContainsRawUtf16Substring(dll, kw))
                hits.Add(kw);
        }

        hits.Should().BeEmpty(
            because: $"{assemblyName} に伝送系の語彙リテラルが埋め込まれている。" +
                     "Tsumugi は『生成』が責務であり、伝送系（取込・送信）には触れない。" +
                     Environment.NewLine +
                     "検出: " + string.Join(", ", hits));
    }

    private static void AssertAllowlistHasReasons()
    {
        var emptyReason = Allowlist
            .Where(e => string.IsNullOrWhiteSpace(e.Reason))
            .Select(e => e.Symbol)
            .ToArray();
        emptyReason.Should().BeEmpty(
            because: "Allowlist の各エントリは Reason 必須（空文字禁止）。"
                     + Environment.NewLine
                     + "Reason 空: " + string.Join(", ", emptyReason));
    }
}
