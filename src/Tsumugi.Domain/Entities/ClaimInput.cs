using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>事業所・利用者・サービス月単位の月次請求固有入力。</summary>
public sealed record ClaimInput : Entity
{
    public required Guid OfficeId { get; init; }
    public required Guid RecipientId { get; init; }
    public required ServiceMonth ServiceMonth { get; init; }
    public required Guid RootId { get; init; }
    public required int Revision { get; init; }
    public required RecordKind Kind { get; init; }
    public Guid? ExpectedHeadId { get; init; }
    public UpperLimitManagementResult? UpperLimitManagementResult { get; init; }
    public int? UpperLimitManagedAmountYen { get; init; }
    public int? MunicipalSubsidyAmountYen { get; init; }
    public ServiceMonth? ExceptionalUsageStartMonth { get; init; }
    public ServiceMonth? ExceptionalUsageEndMonth { get; init; }
    public int? ExceptionalUsageDays { get; init; }
    public int? StandardUsageDayTotal { get; init; }

    /// <summary>
    /// 訪問支援特別加算の<b>算定回数</b>（当月合計・単位は「回」）。
    /// 公式項目 <c>provider:J611:01:052</c>「訪問支援特別加算（回）（算定回数）」＝
    /// 「訪問支援特別加算の算定回数の合計を設定」。
    /// 日次実績（<see cref="DailyRecord.SpecialVisitSupportMinutes"/>＝実際にサービス提供した時間・分）からは
    /// 導出できないため個別入力で受ける。根拠は留意事項通知 2(6)⑨「所要時間については、実際に要した時間により
    /// 算定されるのではなく、計画に基づいて行われるべき指定サービス等に要する時間に基づき算定される」および
    /// 「1月に2回算定する場合は…再度5日間以上連続して利用がなかった場合にのみ対象」。
    /// </summary>
    public int? SpecialVisitSupportBilledCount { get; init; }

    /// <summary>
    /// 施設外支援の<b>累計日数</b>（単位は「日」）。
    /// 公式項目 <c>provider:J611:01:054</c>「施設外支援 累計（日／１８０日）」＝
    /// 「就労継続支援において、施設外支援の累計日数を設定」。
    /// 当月分を含むか否かは公式資料から一意に確定できないため、運用者が明細書の「累計」欄に設定する値を
    /// そのまま受ける（アプリ側で当月の日次記録から導出しない）。
    /// </summary>
    public int? OffsiteSupportCumulativeDays { get; init; }
}
