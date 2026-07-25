using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim;

namespace Tsumugi.Application.Claim;

/// <summary>
/// 永続化された確定請求履歴が信用できるかを検証する。書込み経路（<c>ClaimFinalizationStore</c>）と
/// 読出し経路（<see cref="VerifiedClaimBatchProvider"/>）の<b>唯一の</b>検証実装。
/// </summary>
/// <remarks>
/// <para>検証する内容:</para>
/// <list type="number">
/// <item>履歴構造（<see cref="ClaimBatchPolicy.ValidateHistory"/>: Revision の連番・New は1件・
/// Origin/ExpectedHead の連鎖・Cancel 後の追記禁止・合計の符号と Cancel の 0 強制）</item>
/// <item>確定操作IDの一意性</item>
/// <item>header と detail の版一致（claim-master / CSV仕様 / 帳票仕様 / snapshot版）と作成者一致</item>
/// <item>合計の整合（Σdetail＝header、Cancel は detail 0 件）</item>
/// <item>snapshot envelope（codec による canonical JSON 検証と schemaVersion の一致）</item>
/// <item>確定操作 payload の SHA-256（永続化された値から再構築したハッシュとの一致）</item>
/// </list>
/// <para>
/// 一つでも崩れていれば <see cref="ClaimFinalizationException"/> で fail-close する。
/// 国保連へ渡す成果物（CSV・3帳票）は、この検証を通った aggregate からしか作らない
/// （<see cref="VerifiedClaimBatch"/>）。
/// </para>
/// </remarks>
public sealed class ClaimHistoryVerifier(
    IClaimFinalizationOperationRegistry operationRegistry,
    IClaimSnapshotValidationCodecRegistry codecRegistry)
{
    /// <summary>履歴全体を検証する。</summary>
    public void Verify(IReadOnlyList<ClaimBatchAggregate> history)
    {
        ArgumentNullException.ThrowIfNull(history);
        ClaimBatchPolicy.ValidateHistory(history.Select(item => item.Header).ToArray());
        if (history.Select(item => item.Header.FinalizationOperationId).Distinct().Count() != history.Count)
            throw Error(ClaimErrorCode.InvalidHistory);
        foreach (var aggregate in history) VerifyAggregate(aggregate, requireOperationHash: true);
    }

    /// <summary>1 revision 分の header＋details を検証する。</summary>
    public void VerifyAggregate(ClaimBatchAggregate aggregate, bool requireOperationHash)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        var batch = aggregate.Header;
        var details = aggregate.Details;
        if (batch.Id == Guid.Empty
            || batch.FinalizationOperationId == Guid.Empty
            || !ClaimFinalizationGuards.Bounded(batch.CreatedBy)
            || !ClaimFinalizationGuards.AsciiBounded(batch.ClaimMasterVersion)
            || !ClaimFinalizationGuards.AsciiBounded(batch.CsvSpecificationVersion)
            || !ClaimFinalizationGuards.AsciiBounded(batch.ReportSpecificationVersion)
            || !ClaimFinalizationGuards.AsciiBounded(batch.SnapshotApplicationVersion)
            || !ClaimFinalizationGuards.AsciiBounded(batch.OperationApplicationVersion)
            || !ClaimFinalizationGuards.AsciiBounded(batch.OperationPayloadSchemaVersion)
            || !ClaimFinalizationGuards.LowerSha256(batch.OperationPayloadSha256)
            || details.Any(detail =>
                detail.Id == Guid.Empty
                || detail.ClaimBatchId != batch.Id
                || detail.RecipientId == Guid.Empty
                || !ClaimFinalizationGuards.Bounded(detail.CreatedBy)
                || detail.CreatedBy != batch.CreatedBy
                || !ClaimFinalizationGuards.AsciiBounded(detail.SnapshotSchemaVersion)
                || !ClaimFinalizationGuards.AsciiBounded(detail.ClaimMasterVersion)
                || !ClaimFinalizationGuards.AsciiBounded(detail.CsvSpecificationVersion)
                || !ClaimFinalizationGuards.AsciiBounded(detail.ReportSpecificationVersion)
                || !ClaimFinalizationGuards.AsciiBounded(detail.SnapshotApplicationVersion)
                || detail.ClaimMasterVersion != batch.ClaimMasterVersion
                || detail.CsvSpecificationVersion != batch.CsvSpecificationVersion
                || detail.ReportSpecificationVersion != batch.ReportSpecificationVersion
                || detail.SnapshotApplicationVersion != batch.SnapshotApplicationVersion
                || detail.TotalUnits < 0
                || detail.TotalCostYen < 0
                || detail.BenefitYen < 0
                || detail.BurdenYen < 0)
            || details.Select(detail => detail.RecipientId).Distinct().Count() != details.Count
            || (batch.Kind == RecordKind.Cancel && details.Count != 0)
            || (batch.Kind != RecordKind.Cancel
                && (details.Sum(detail => (long)detail.TotalUnits) != batch.TotalUnits
                    || details.Sum(detail => (long)detail.TotalCostYen) != batch.TotalCostYen
                    || details.Sum(detail => (long)detail.BenefitYen) != batch.TotalBenefitYen
                    || details.Sum(detail => (long)detail.BurdenYen) != batch.TotalBurdenYen)))
            throw Error(ClaimErrorCode.InvalidHistory);

        var restored = RestoreDetailDrafts(aggregate);
        if (!requireOperationHash) return;
        var payload = RebuildOperationPayload(aggregate, restored);
        var payloadHash = RequireOperationPayloadHash(payload, ClaimErrorCode.InvalidHistory);
        if (!string.Equals(payloadHash, batch.OperationPayloadSha256, StringComparison.Ordinal))
            throw Error(ClaimErrorCode.InvalidHistory);
    }

    /// <summary>永続化された detail の JSON から検証済み envelope を復元する。</summary>
    public ClaimFinalizationDetailDraft[] RestoreDetailDrafts(ClaimBatchAggregate aggregate)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        return aggregate.Details
            .OrderBy(detail => detail.RecipientId)
            .Select(detail => new ClaimFinalizationDetailDraft(
                detail.RecipientId,
                detail.SnapshotSchemaVersion,
                detail.ClaimMasterVersion,
                detail.CsvSpecificationVersion,
                detail.ReportSpecificationVersion,
                detail.SnapshotApplicationVersion,
                RestoreEnvelope(detail.InputSnapshotJson, detail.SnapshotSchemaVersion),
                RestoreEnvelope(detail.CalculationSnapshotJson, detail.SnapshotSchemaVersion),
                detail.TotalUnits,
                detail.TotalCostYen,
                detail.BenefitYen,
                detail.BurdenYen))
            .ToArray();
    }

    /// <summary>
    /// 永続化済み aggregate が canonical 化される確定操作 payload の SHA-256 を求める。
    /// </summary>
    /// <remarks>
    /// ハッシュは秘密鍵を用いない完全性検査であり、この値を計算できること自体は権限を与えない
    /// （整合した aggregate を持っていることと同義）。テスト・診断が「整合した履歴」を組み立てる
    /// ために公開する。
    /// </remarks>
    public string ComputeOperationPayloadSha256(ClaimBatchAggregate aggregate)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        return RebuildOperationPayload(aggregate, RestoreDetailDrafts(aggregate)).Sha256;
    }

    /// <summary>payload の canonical bytes と自称ハッシュの一致を検証して返す。</summary>
    public static string RequireOperationPayloadHash(
        ClaimFinalizationOperationPayload payload,
        ClaimErrorCode errorCode)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var hash = Convert.ToHexStringLower(SHA256.HashData(payload.GetCanonicalUtf8Bytes()));
        if (!string.Equals(hash, payload.Sha256, StringComparison.Ordinal))
            throw Error(errorCode);
        return hash;
    }

    private ClaimFinalizationOperationPayload RebuildOperationPayload(
        ClaimBatchAggregate aggregate,
        IReadOnlyList<ClaimFinalizationDetailDraft> details)
    {
        var readEntry = operationRegistry.GetReadEntry(aggregate.Header.OperationPayloadSchemaVersion)
            ?? throw Error(ClaimErrorCode.UnsupportedOperationSchema);
        return readEntry.Operation.Rebuild(aggregate, details);
    }

    private ValidatedClaimSnapshotEnvelope RestoreEnvelope(string json, string expectedSchema)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            var reader = new Utf8JsonReader(bytes);
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            if (reader.BytesConsumed != bytes.Length
                || root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("schemaVersion", out var schemaProperty)
                || schemaProperty.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("validationCodecId", out var codecProperty)
                || codecProperty.ValueKind != JsonValueKind.String)
                throw Error(ClaimErrorCode.InvalidSnapshotEnvelope);
            var schema = schemaProperty.GetString();
            var codecId = codecProperty.GetString();
            if (schema != expectedSchema || schema is null || codecId is null)
                throw Error(ClaimErrorCode.InvalidSnapshotEnvelope);
            if (!ClaimFinalizationGuards.AsciiBounded(schema)
                || !ClaimFinalizationGuards.AsciiBounded(codecId))
                throw Error(ClaimErrorCode.InvalidSnapshotEnvelope);
            var codec = codecRegistry.Find(schema, codecId)
                ?? throw Error(ClaimErrorCode.UnsupportedSnapshotCodec);
            var envelope = ClaimFinalizationGuards.InvokeCodec(() => codec.ReadValidated(bytes));
            if (envelope.SchemaVersion != schema
                || envelope.ValidationCodecId != codecId
                || !ClaimFinalizationGuards.LowerSha256(envelope.PayloadSha256))
                throw Error(ClaimErrorCode.InvalidSnapshotEnvelope);
            return envelope;
        }
        catch (ClaimFinalizationException)
        {
            throw;
        }
        catch (JsonException)
        {
            throw Error(ClaimErrorCode.InvalidSnapshotEnvelope);
        }
        catch (Exception exception) when (
            exception is KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            throw Error(ClaimErrorCode.InvalidSnapshotEnvelope);
        }
    }

    private static ClaimFinalizationException Error(ClaimErrorCode code) => new(code);
}
