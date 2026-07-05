using System.Reflection;
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;

namespace Tsumugi.Infrastructure.Reporting;

/// <summary>
/// QuestPDF のライセンス設定と日本語フォント登録。
/// ADR 0013 により Community License を採用し、Noto Sans JP (SIL OFL 1.1) を EmbeddedResource として同梱する。
/// </summary>
public static class QuestPdfLicenseConfigurator
{
    internal const string NotoSansJpFamilyName = "Noto Sans JP";

    private static readonly object _sync = new();
    private static bool _initialized;

    /// <summary>アプリ起動時・テスト static ctor で 1 度だけ呼ぶ。ライセンス + フォント登録 + 環境フォント抑止を一気に適用する。</summary>
    public static void Initialize()
    {
        lock (_sync)
        {
            if (_initialized) return;

            QuestPDF.Settings.License = LicenseType.Community;
            QuestPDF.Settings.UseEnvironmentFonts = false;
            RegisterFontsFromEmbeddedResources();

            _initialized = true;
        }
    }

    private static void RegisterFontsFromEmbeddedResources()
    {
        var asm = typeof(QuestPdfLicenseConfigurator).Assembly;
        RegisterOne(asm, "Tsumugi.Infrastructure.Reporting.assets.fonts.NotoSansJP-Regular.otf");
        RegisterOne(asm, "Tsumugi.Infrastructure.Reporting.assets.fonts.NotoSansJP-Bold.otf");
    }

    private static void RegisterOne(Assembly asm, string resourceName)
    {
        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"埋込フォントが見つかりません: {resourceName}");
        // QuestPDF 2025.4.0 に RegisterFontFromStream は存在しない（設計時点の想定 API から改名/削除）。
        // FontManager.RegisterFont(Stream) は .otf 内部メタデータから Family/Weight/Style を自動検出する。
        // Task 1 で fc-scan 済み: Regular/Bold とも Family は定数 NotoSansJpFamilyName と一致することを確認済み。
        FontManager.RegisterFont(stream);
    }
}
