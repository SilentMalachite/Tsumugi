using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Claim;

/// <summary>構築結果。<see cref="Request"/>が<c>null</c>のとき<see cref="Issues"/>は必ず非空。</summary>
public sealed record ClaimCalculationRequestBuildResult(
    ClaimCalculationRequest? Request,
    IReadOnlyList<ClaimPreparationIssue> Issues);

/// <summary>
/// snapshot＋解決済みトークンから<see cref="ClaimCalculationRequest"/>への純粋写像。
/// 写像できない入力は<see cref="ClaimPreparationIssue"/>として返しフェイルクローズする（推測しない）。
/// </summary>
public static class ClaimCalculationRequestBuilder
{
    /// <summary>
    /// 法定給付率（費用額の9割給付・利用者負担1割相当）。ADR 0025の決定利用者負担ステップは
    /// 「1割相当額＝総費用額×10/100の円未満切捨て」であり、<c>ClaimCalculator</c>は
    /// 本値の残余(100-90)/100からこの10/100を導出する。制度数値をApplicationへ置くのは
    /// この給付率1定数のみ（他の制度値はすべてマスタseed由来）。
    /// </summary>
    public const int StatutoryBenefitRatePercent = 90;

    /// <summary>
    /// 再エンコード後、基本報酬の解決はaverage-wage-band整数条件（体制届の公式option code）が担い、
    /// PaymentBand tokenは解決に使わない（ADR 0023: 体制届option番号と告示の区分番号は別体系で、
    /// 名称・数字だけで相互変換しない）。<c>ClaimBillingConditionContext</c>契約上必須のため
    /// 空文字を渡す。参加評価型（band-participation token条件）はこれに一致せず、
    /// 対応が一次資料で確定するまでフェイルクローズで除外される（open-questions起票済み）。
    /// </summary>
    public const string PaymentBandNotUsedForResolution = "";

    internal const string AverageWageBandOptionField =
        nameof(OfficeClaimProfile) + "." + nameof(OfficeClaimProfile.AverageWageBandOption);
    internal const string ReformStatusField =
        nameof(OfficeClaimProfile) + "." + nameof(OfficeClaimProfile.ReformStatus);
    internal const string CapacityHeadcountField = nameof(OfficeClaimProfile) + ".CapacityHeadcount";
    internal const string StaffingClassField = nameof(OfficeClaimProfile) + ".StaffingClass";
    internal const string RegionGradeField = nameof(Office) + "." + nameof(Office.RegionGrade);
    internal const string RegionKeyConflictField =
        nameof(OfficeClaimProfile) + "." + nameof(OfficeClaimProfile.RegionKey);
    internal const string ServiceCategoryField = nameof(Office) + "." + nameof(Office.ServiceCategory);

    public static ClaimCalculationRequestBuildResult Build(
        ClaimCalculationSnapshot snapshot,
        ServiceMonth serviceMonth,
        ClaimBillingConditionTokens? tokens)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _ = serviceMonth.ToInt();

        var issues = new List<ClaimPreparationIssue>();
        var bandOption = ResolveNumericBandOption(snapshot.Profile, issues);
        var reformStatus = ResolveReformStatus(snapshot.Profile, issues);
        ValidateTokens(tokens, issues);
        var sources = BuildSources(snapshot, issues);

        if (issues.Count > 0)
        {
            return new ClaimCalculationRequestBuildResult(null, issues);
        }

        var request = new ClaimCalculationRequest(
            serviceMonth,
            new ClaimBillingConditionContext(
                tokens!.RewardSystem!,
                PaymentBandNotUsedForResolution,
                tokens.CapacityHeadcount!.Value,
                tokens.StaffingKey!,
                bandOption!.Value,
                reformStatus!.Value),
            tokens.RegionKey!,
            tokens.RegionUnitPriceServiceKind!,
            sources);
        return new ClaimCalculationRequestBuildResult(request, issues);
    }

    private static AverageWageBandOption? ResolveNumericBandOption(
        OfficeClaimProfile? profile, List<ClaimPreparationIssue> issues)
    {
        if (profile is null)
        {
            issues.Add(new ClaimPreparationIssue(
                ClaimPreparationIssueCode.MissingRequiredEvidence,
                RecipientId: null,
                ClaimPreparationReadiness.OfficeClaimProfileField,
                ClaimInputDestination.ClaimInput));
            return null;
        }

        // ADR 0023: FiledTransition（公式option 8）は新規指定要件の検証後にのみ数値bandへ解決でき、
        // resolverへ直接渡してはならない。ProductionActivitySupport（option 10）は平均工賃bandの
        // 対象外。本スライスはNumericのみを扱い、それ以外は算定不能として可視化する。
        if (profile.AverageWageBandOption is not { } option
            || option.Kind != AverageWageBandOptionKind.Numeric)
        {
            issues.Add(new ClaimPreparationIssue(
                ClaimPreparationIssueCode.MissingRequiredField,
                RecipientId: null,
                AverageWageBandOptionField,
                ClaimInputDestination.ClaimInput));
            return null;
        }

        return option;
    }

    private static R8ReformStatus? ResolveReformStatus(
        OfficeClaimProfile? profile, List<ClaimPreparationIssue> issues)
    {
        if (profile is null) return null;
        if (profile.ReformStatus is not { } status || status == R8ReformStatus.Unknown)
        {
            issues.Add(new ClaimPreparationIssue(
                ClaimPreparationIssueCode.MissingRequiredField,
                RecipientId: null,
                ReformStatusField,
                ClaimInputDestination.ClaimInput));
            return null;
        }

        return status;
    }

    private static void ValidateTokens(
        ClaimBillingConditionTokens? tokens, List<ClaimPreparationIssue> issues)
    {
        void Require(bool present, string fieldCode, ClaimInputDestination destination)
        {
            if (!present)
            {
                issues.Add(new ClaimPreparationIssue(
                    ClaimPreparationIssueCode.MissingRequiredField,
                    RecipientId: null,
                    fieldCode,
                    destination));
            }
        }

        Require(
            !string.IsNullOrWhiteSpace(tokens?.RewardSystem),
            ServiceCategoryField,
            ClaimInputDestination.Office);

        if (tokens?.RegionKeyConflict == true)
        {
            // 両ソース（OfficeClaimProfile.RegionKey / Office.RegionGrade）が揃って不一致：
            // フェイルクローズ専用issue。汎用の「地域区分未解決」issueとは区別し、
            // どちらの値が誤りかを事業所側で確認させる（controller decision 2026-07-19）。
            issues.Add(new ClaimPreparationIssue(
                ClaimPreparationIssueCode.RegionKeySourceConflict,
                RecipientId: null,
                RegionKeyConflictField,
                ClaimInputDestination.ClaimInput));
        }
        else
        {
            Require(
                !string.IsNullOrWhiteSpace(tokens?.RegionKey)
                    && !string.IsNullOrWhiteSpace(tokens?.RegionUnitPriceServiceKind),
                RegionGradeField,
                ClaimInputDestination.Office);
        }

        Require(tokens?.CapacityHeadcount is > 0, CapacityHeadcountField, ClaimInputDestination.ClaimInput);
        Require(
            !string.IsNullOrWhiteSpace(tokens?.StaffingKey),
            StaffingClassField,
            ClaimInputDestination.ClaimInput);
    }

    private static List<RecipientClaimSource> BuildSources(
        ClaimCalculationSnapshot snapshot, List<ClaimPreparationIssue> issues)
    {
        var evidenceByRecipient = snapshot.EffectiveCertificateEvidenceByRecipient;
        var sources = new List<RecipientClaimSource>();
        foreach (var recipientId in snapshot.RecipientIds)
        {
            // 実効なPresent実績日数0の利用者は当月請求対象外（明細を作らない）。
            // 入力不備の可視化はcontext builder＋readiness側が担う。
            var billedDays = snapshot.BilledDaysByRecipient.GetValueOrDefault(recipientId);
            if (billedDays <= 0) continue;

            var certificateCount = snapshot.EffectiveCertificateCountByRecipient
                .GetValueOrDefault(recipientId);
            if (certificateCount != 1)
            {
                issues.Add(new ClaimPreparationIssue(
                    certificateCount == 0
                        ? ClaimPreparationIssueCode.MissingRequiredField
                        : ClaimPreparationIssueCode.MultipleEffectiveCertificates,
                    recipientId,
                    ClaimPreparationReadiness.EffectiveCertificateField,
                    ClaimInputDestination.Certificate));
                continue;
            }

            // RecipientClaimSourceの契約（doc-comment）: 未確認の証上限をsource構築前に弾く。
            var evidence = evidenceByRecipient.GetValueOrDefault(recipientId);
            if (evidence is null
                || !evidence.MonthlyCostCap.IsEntered
                || evidence.MonthlyCostCap.ValueYen is not { } capYen)
            {
                issues.Add(new ClaimPreparationIssue(
                    ClaimPreparationIssueCode.MissingRequiredEvidence,
                    recipientId,
                    ClaimPreparationReadiness.CertificateEvidenceField,
                    ClaimInputDestination.ClaimInput));
                continue;
            }

            if (string.IsNullOrWhiteSpace(evidence.OriginalDocumentReference)
                || evidence.ConfirmedAt is null
                || string.IsNullOrWhiteSpace(evidence.ConfirmedBy))
            {
                issues.Add(new ClaimPreparationIssue(
                    ClaimPreparationIssueCode.OriginalEvidenceUnconfirmed,
                    recipientId,
                    ClaimPreparationReadiness.OriginalEvidenceField,
                    ClaimInputDestination.ClaimInput));
                continue;
            }

            sources.Add(new RecipientClaimSource(
                recipientId, billedDays, StatutoryBenefitRatePercent, capYen));
        }

        return sources;
    }
}
