using QuestPDF.Infrastructure;

namespace Tsumugi.Infrastructure.Reporting;

/// <summary>
/// QuestPDF のライセンス設定。
/// ADR 0013 により Community License を採用する（事業所年商が QuestPDF Community 閾値未満である前提）。
/// 閾値超過時は Reporting 層を Avalonia 印刷経路（PrintDialog → ビジュアル Print）に差し替える設計。
/// </summary>
public static class QuestPdfLicenseConfigurator
{
    /// <summary>アプリ起動時に 1 度だけ呼ぶ。コードでハード設定し、appsettings や環境変数を経由しない（CLAUDE.md オフライン制約）。</summary>
    public static void ApplyCommunityLicense() => QuestPDF.Settings.License = LicenseType.Community;
}
