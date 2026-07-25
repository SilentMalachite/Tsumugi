using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.App.Tests;

/// <summary>
/// ViewModel テストが <see cref="VerifiedClaimBatchProvider"/> を組むための最小スキャフォールド。
/// </summary>
/// <remarks>
/// App.Tests の関心は UI 配線（コマンドの可否・保存呼び出し・表示文言）であり、履歴の完全性は
/// Application.Tests（改竄検知）と Infrastructure.Tests（実DB経路）が担当する。そのため手書きの
/// Fake 履歴に本物の確定操作 payload ハッシュを付け直してから検証器へ渡す。
/// </remarks>
internal static class ClaimVerifiedBatchTestFactory
{
    /// <summary>codec v2 が受け付ける最小の canonical envelope（"{}" は検証を通らない）。</summary>
    internal const string MinimalEnvelopeJson =
        """{"schemaVersion":"claim-snapshot-v2","validationCodecId":"claim-snapshot-codec-v2"}""";

    internal static ClaimHistoryVerifier Verifier() => new(
        new ClaimFinalizationOperationRegistry(),
        new ProductionClaimSnapshotValidationCodecRegistry());

    internal static VerifiedClaimBatchProvider Provider(IClaimBatchRepository repository) =>
        new(new SigningRepository(repository, Verifier()), Verifier());

    private sealed class SigningRepository(IClaimBatchRepository inner, ClaimHistoryVerifier verifier)
        : IClaimBatchRepository
    {
        public async Task<IReadOnlyList<ClaimBatchAggregate>> ListHistoryAggregatesAsync(
            Guid officeId, ServiceMonth serviceMonth, CancellationToken ct)
            => [.. (await inner.ListHistoryAggregatesAsync(officeId, serviceMonth, ct)).Select(Sign)];

        public async Task<ClaimBatchAggregate?> FindByOperationIdAsync(
            Guid finalizationOperationId, CancellationToken ct)
            => await inner.FindByOperationIdAsync(finalizationOperationId, ct) is { } aggregate
                ? Sign(aggregate)
                : null;

        private ClaimBatchAggregate Sign(ClaimBatchAggregate aggregate) => new(
            aggregate.Header with
            {
                OperationPayloadSha256 = verifier.ComputeOperationPayloadSha256(aggregate),
            },
            aggregate.Details);
    }
}
