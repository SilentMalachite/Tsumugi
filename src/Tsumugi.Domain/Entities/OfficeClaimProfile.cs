using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>指定権者台帳に基づく事業所の請求区分登録を保持する追記型スナップショット。</summary>
public sealed record OfficeClaimProfile : Entity
{
    public required Guid OfficeId { get; init; }
    public required DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public required Guid RootId { get; init; }
    public required int Revision { get; init; }
    public required RecordKind Kind { get; init; }
    public Guid? ExpectedHeadId { get; init; }
    public ClaimMasterVersion? MasterVersion { get; init; }
    public R8ReformStatus? ReformStatus { get; init; }
    public AverageWageBandOption? AverageWageBandOption { get; init; }
    public DateOnly? DesignationDate { get; init; }
    public DateOnly? SupportStartDate { get; init; }
    public VersionedAverageWageBandOption? EarlierRegisteredBandOption { get; init; }
    public ServiceMonth? EarlierRegistrationMonth { get; init; }
    public VersionedAverageWageBandOption? LaterRegisteredBandOption { get; init; }
    public ServiceMonth? LaterRegistrationMonth { get; init; }
    public string? ReformComparisonEvidenceDocumentId { get; init; }
    public DateRange? FiledTransitionPeriod { get; init; }
    public string? FiledTransitionEvidenceDocumentId { get; init; }
    public string? EvidenceDocumentId { get; init; }
    public DateTimeOffset? ConfirmedAt { get; init; }
    public string? ConfirmedBy { get; init; }
    public string? ConfirmationReason { get; init; }

    /// <summary>
    /// 利用定員（実頭数）。ADR 0021が定める基本報酬選択の構造化入力（定員条件）。
    /// 未入力はnull（請求算定は未解決としてフェイルクローズ、値が入れば1以上を要求する）。
    /// </summary>
    public int? CapacityHeadcount { get; init; }

    /// <summary>
    /// 人員配置区分token。ADR 0021が定める基本報酬選択の構造化入力。値はマスタの
    /// staffing条件語彙（<c>ClaimConditionKind.Staffing</c>）に対応する文字列で、
    /// 語彙の閉集合検証はここでは行わない（不整合はcalculation/readiness側でフェイルクローズ）。
    /// </summary>
    public string? StaffingKey { get; init; }

    /// <summary>
    /// 地域区分token。ADR 0021が定める基本報酬選択の構造化入力。値はregion-unit-price
    /// masterの<c>RegionKey</c>語彙に対応する文字列。未入力時は
    /// <c>OfficeClaimBillingTokenProvider</c>が<c>Office.RegionGrade</c>由来の名義的既定へ
    /// フォールバックする（既存事業所の後方互換）。
    /// </summary>
    public string? RegionKey { get; init; }
}
