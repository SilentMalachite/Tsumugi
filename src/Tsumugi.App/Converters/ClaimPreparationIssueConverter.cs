using System.Globalization;
using Avalonia.Data.Converters;
using Tsumugi.Application.Claim;

namespace Tsumugi.App.Converters;

/// <summary>
/// <see cref="ClaimPreparationIssue"/>を、氏名・受給者証番号を含まない読みやすい日本語1行へ変換する。
/// 個人情報はRecipientId(Guid)としてのみ保持しており文言化しない（ハード制約4）。
/// </summary>
public sealed class ClaimPreparationIssueConverter : IValueConverter
{
    public static readonly ClaimPreparationIssueConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ClaimPreparationIssue issue) return string.Empty;

        var reason = issue.Code switch
        {
            ClaimPreparationIssueCode.MissingRequiredField => "必須項目が未入力です",
            ClaimPreparationIssueCode.MultipleEffectiveCertificates => "有効な受給者証が複数あります",
            ClaimPreparationIssueCode.InvalidEffectiveHistory => "履歴が不正です",
            ClaimPreparationIssueCode.MasterVersionUnavailable => "対象月の請求制度マスターが未整備です",
            ClaimPreparationIssueCode.OriginalEvidenceUnconfirmed => "原本確認が未了です",
            ClaimPreparationIssueCode.UnresolvedRequirementCondition => "適用条件を判定できません",
            ClaimPreparationIssueCode.MissingRequiredEvidence => "必須の請求根拠が未登録です",
            ClaimPreparationIssueCode.EvidenceSourceMismatch => "請求根拠の参照元が一致しません",
            ClaimPreparationIssueCode.UnresolvedEvidence => "請求根拠を解決できません",
            ClaimPreparationIssueCode.RegionKeySourceConflict => "地域区分の設定が事業所間で一致しません",
            _ => "確認が必要な項目があります",
        };
        var destination = issue.Destination switch
        {
            ClaimInputDestination.Certificate => "受給者証",
            ClaimInputDestination.ClaimInput => "請求入力",
            ClaimInputDestination.ClaimPreparation => "請求確定",
            ClaimInputDestination.DailyRecord => "日次記録",
            ClaimInputDestination.Office => "事業所",
            _ => null,
        };

        var location = destination is null ? string.Empty : $"（{destination}画面で確認）";
        var scope = issue.RecipientId is null ? "事業所全体" : "対象利用者あり";
        return $"{reason}{location}: {issue.FieldCode} [{scope}]";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
