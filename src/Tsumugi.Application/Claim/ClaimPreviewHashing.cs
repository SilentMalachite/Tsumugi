using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Claim;

/// <summary>
/// PreviewHash＝確定操作と同一のcanonical化（<see cref="ClaimFinalizationOperationV1.Canonicalize"/>）を
/// 固定placeholder付きプレビューdraftへ適用したSHA-256。履歴状態（New/Correct・expected head）に
/// 依存しない「内容の指紋」であり、同一入力→同一hashを保証する。Close側も同じ手順で再計算して
/// 照合するため、プレビュー取得後に入力が変わればhash不一致で確定を拒否できる。
/// </summary>
internal static class ClaimPreviewHashing
{
    /// <summary>
    /// Canonicalizeは空GuidのFinalizationOperationIdを拒否する（InvalidOperationPayload）ため、
    /// プレビュー用の固定非空sentinelを使う。値自体に意味はなく、決定論性のためだけに固定する。
    /// </summary>
    internal static readonly Guid PreviewFinalizationOperationId =
        new("7cf3b6a1-91d4-4f6e-8f14-5f0b9c2d7e10");

    internal const string PreviewCreatedBy = "preview";

    internal static string Compute(
        Guid officeId,
        ServiceMonth serviceMonth,
        string claimMasterVersion,
        ClaimCalculationResult result,
        IReadOnlyList<ClaimFinalizationDetailDraft> details)
        => new ClaimFinalizationOperationV1().Canonicalize(new ClaimFinalizationDraft(
            PreviewFinalizationOperationId,
            RecordKind.New,
            officeId,
            serviceMonth,
            RootBatchId: null,
            ExpectedHead: null,
            PreviewCreatedBy,
            ClaimFinalizationVersions.OperationApplicationVersion,
            claimMasterVersion,
            ClaimFinalizationVersions.CsvSpecificationVersion,
            ClaimFinalizationVersions.ReportSpecificationVersion,
            ClaimFinalizationVersions.SnapshotApplicationVersion,
            result.TotalUnits,
            result.TotalCostYen,
            result.TotalBenefitYen,
            result.TotalBurdenYen,
            details)).Sha256;
}
