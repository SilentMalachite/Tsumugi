using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Audit;
using Tsumugi.Application.Claim;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim;

namespace Tsumugi.Infrastructure.Persistence;

/// <summary>請求履歴の検証、採番、snapshotと監査の追記を単一の非deferred transactionで所有する。</summary>
public sealed class ClaimFinalizationStore(
    IDbContextFactory<TsumugiDbContext> contextFactory,
    IClaimFinalizationOperationRegistry operationRegistry,
    IClaimSnapshotValidationCodecRegistry codecRegistry,
    IClaimAuditEntryFactory auditEntryFactory,
    TimeProvider clock) : IClaimFinalizationStore
{
    public async Task<ClaimFinalizationResult> CommitAsync(
        ClaimFinalizationDraft draft,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(draft);

        try
        {
            await using var db = await contextFactory.CreateDbContextAsync(ct);
            await db.Database.OpenConnectionAsync(ct);
            var connection = (SqliteConnection)db.Database.GetDbConnection();
            await using var transaction = connection.BeginTransaction(deferred: false);
            await db.Database.UseTransactionAsync(transaction, ct);

            var replay = await FindAggregateByOperationIdAsync(
                db,
                draft.FinalizationOperationId,
                ct);
            if (replay is not null)
            {
                var result = ValidateReplay(replay, draft);
                await transaction.RollbackAsync(ct);
                return result;
            }

            if (!codecRegistry.HasWriteSupport)
                throw Error(ClaimErrorCode.UnsupportedSnapshotCodec);

            var history = await ListHistoryAggregatesAsync(
                db,
                draft.OfficeId,
                draft.ServiceMonth,
                ct);
            ValidateHistory(history);

            var effectiveDraft = NormalizeAndValidateIncoming(draft, history);
            var writeEntry = operationRegistry.GetWriteEntry(
                ClaimBatch.CurrentOperationPayloadSchemaVersion)
                ?? throw Error(ClaimErrorCode.UnsupportedOperationSchema);
            ValidateIncomingEnvelopes(effectiveDraft, requireWrite: true);
            var operationPayload = writeEntry.Operation.Canonicalize(effectiveDraft);
            var operationHash = ValidateOperationPayloadHash(
                operationPayload,
                ClaimErrorCode.InvalidOperationPayload);

            var revision = ClaimBatchPolicy.NextRevision(
                history.Select(item => item.Header).ToArray());
            var now = clock.GetUtcNow();
            var batch = CreateBatch(effectiveDraft, revision, operationHash, now);
            var details = CreateDetails(effectiveDraft, batch.Id, now);
            var candidate = new ClaimBatchAggregate(batch, details);

            ClaimBatchPolicy.ValidateHistory(
                history.Select(item => item.Header).Append(batch).ToArray());
            ValidateAggregate(candidate, requireOperationHash: true);
            ValidateHistory([.. history, candidate]);

            var audit = CreateAuditEntry(batch, effectiveDraft.CreatedBy, now);

            db.ClaimBatches.Add(batch);
            db.ClaimDetails.AddRange(details);
            db.AuditEntries.Add(audit);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new ClaimFinalizationResult(batch.Id, batch.Revision, IsReplay: false);
        }
        catch (ClaimFinalizationException)
        {
            throw;
        }
        catch (SqliteException)
        {
            throw Error(ClaimErrorCode.PersistenceFailure);
        }
        catch (DbUpdateException)
        {
            throw Error(ClaimErrorCode.PersistenceFailure);
        }
        catch (InvalidOperationException)
        {
            throw Error(ClaimErrorCode.InvalidHistory);
        }
        catch (ArgumentException)
        {
            throw Error(ClaimErrorCode.InvalidOperationPayload);
        }
    }

    private ClaimFinalizationResult ValidateReplay(
        ClaimBatchAggregate aggregate,
        ClaimFinalizationDraft incoming)
    {
        ValidateAggregate(aggregate, requireOperationHash: true);
        var readEntry = operationRegistry.GetReadEntry(
            aggregate.Header.OperationPayloadSchemaVersion)
            ?? throw Error(ClaimErrorCode.UnsupportedOperationSchema);
        var persistedDetails = RestoreDetailDrafts(aggregate);
        var rebuilt = readEntry.Operation.Rebuild(aggregate, persistedDetails);
        var rebuiltHash = ValidateOperationPayloadHash(rebuilt, ClaimErrorCode.InvalidHistory);
        if (!string.Equals(
                rebuiltHash,
                aggregate.Header.OperationPayloadSha256,
                StringComparison.Ordinal))
            throw Error(ClaimErrorCode.InvalidHistory);

        if (aggregate.Header.Kind == RecordKind.Cancel)
        {
            ValidateIncomingEnvelopes(incoming, requireWrite: false);
            if (incoming.Details.Count != 0
                || incoming.TotalUnits != 0
                || incoming.TotalCostYen != 0
                || incoming.TotalBenefitYen != 0
                || incoming.TotalBurdenYen != 0)
                throw Error(ClaimErrorCode.InvalidOperationPayload);
        }

        var normalizedIncoming = aggregate.Header.Kind == RecordKind.Cancel
            ? incoming with
            {
                ClaimMasterVersion = aggregate.Header.ClaimMasterVersion,
                CsvSpecificationVersion = aggregate.Header.CsvSpecificationVersion,
                ReportSpecificationVersion = aggregate.Header.ReportSpecificationVersion,
                SnapshotApplicationVersion = aggregate.Header.SnapshotApplicationVersion,
            }
            : incoming;
        if (aggregate.Header.Kind != RecordKind.Cancel)
            ValidateIncomingEnvelopes(normalizedIncoming, requireWrite: false);
        var requested = readEntry.Operation.Canonicalize(normalizedIncoming);
        var requestedHash = ValidateOperationPayloadHash(
            requested,
            ClaimErrorCode.InvalidOperationPayload);
        if (!string.Equals(requestedHash, rebuiltHash, StringComparison.Ordinal)
            || incoming.OfficeId != aggregate.Header.OfficeId
            || incoming.ServiceMonth != aggregate.Header.ServiceMonth
            || incoming.Kind != aggregate.Header.Kind
            || incoming.FinalizationOperationId != aggregate.Header.FinalizationOperationId)
            throw Error(ClaimErrorCode.OperationIdCollision);

        return new ClaimFinalizationResult(
            aggregate.Header.Id,
            aggregate.Header.Revision,
            IsReplay: true);
    }

    private AuditEntry CreateAuditEntry(
        ClaimBatch batch,
        string actor,
        DateTimeOffset occurredAt)
    {
        try
        {
            return auditEntryFactory.Create(
                Guid.NewGuid(),
                actor,
                new ClaimAuditPayload(
                    ClaimAuditPayload.FinalizedEventCode,
                    batch.Id,
                    batch.FinalizationOperationId,
                    batch.OfficeId,
                    batch.ServiceMonth,
                    batch.Kind,
                    batch.Revision,
                    batch.OriginId,
                    batch.OperationPayloadSha256),
                occurredAt);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            throw Error(ClaimErrorCode.PersistenceFailure);
        }
    }

    private static ClaimFinalizationDraft NormalizeAndValidateIncoming(
        ClaimFinalizationDraft draft,
        IReadOnlyList<ClaimBatchAggregate> history)
    {
        if (draft.FinalizationOperationId == Guid.Empty
            || draft.OfficeId == Guid.Empty
            || draft.Kind is not RecordKind.New and not RecordKind.Correct and not RecordKind.Cancel
            || draft.Details is null)
            throw Error(ClaimErrorCode.InvalidOperationPayload);
        ValidateDraftMetadata(draft);

        var headers = history.Select(item => item.Header).ToArray();
        var head = ClaimBatchPolicy.Head(headers);
        if (draft.Kind == RecordKind.New)
        {
            if (head is not null || draft.RootBatchId is not null || draft.ExpectedHead is not null)
                throw Error(ClaimErrorCode.ExpectedHeadMismatch);
            ValidateNonCancellation(draft);
            return draft;
        }

        if (head is null
            || head.Kind == RecordKind.Cancel
            || draft.ExpectedHead is null
            || draft.ExpectedHead.BatchId != head.Id
            || draft.ExpectedHead.Revision != head.Revision
            || draft.RootBatchId != headers[0].Id)
            throw Error(ClaimErrorCode.ExpectedHeadMismatch);

        if (draft.Kind == RecordKind.Cancel)
        {
            if (draft.Details.Count != 0
                || draft.TotalUnits != 0
                || draft.TotalCostYen != 0
                || draft.TotalBenefitYen != 0
                || draft.TotalBurdenYen != 0)
                throw Error(ClaimErrorCode.InvalidOperationPayload);

            return draft with
            {
                ClaimMasterVersion = head.ClaimMasterVersion,
                CsvSpecificationVersion = head.CsvSpecificationVersion,
                ReportSpecificationVersion = head.ReportSpecificationVersion,
                SnapshotApplicationVersion = head.SnapshotApplicationVersion,
            };
        }

        ValidateNonCancellation(draft);
        return draft;
    }

    private static void ValidateNonCancellation(ClaimFinalizationDraft draft)
    {
        if (draft.TotalUnits < 0
            || draft.TotalCostYen < 0
            || draft.TotalBenefitYen < 0
            || draft.TotalBurdenYen < 0
            || draft.Details.Select(detail => detail.RecipientId).Distinct().Count() != draft.Details.Count
            || draft.Details.Any(detail =>
                detail.RecipientId == Guid.Empty
                || detail.SnapshotSchemaVersion != detail.InputSnapshotEnvelope.SchemaVersion
                || detail.SnapshotSchemaVersion != detail.CalculationSnapshotEnvelope.SchemaVersion
                || detail.ClaimMasterVersion != draft.ClaimMasterVersion
                || detail.CsvSpecificationVersion != draft.CsvSpecificationVersion
                || detail.ReportSpecificationVersion != draft.ReportSpecificationVersion
                || detail.SnapshotApplicationVersion != draft.SnapshotApplicationVersion
                || detail.TotalUnits < 0
                || detail.TotalCostYen < 0
                || detail.BenefitYen < 0
                || detail.BurdenYen < 0)
            || draft.Details.Sum(detail => (long)detail.TotalUnits) != draft.TotalUnits
            || draft.Details.Sum(detail => (long)detail.TotalCostYen) != draft.TotalCostYen
            || draft.Details.Sum(detail => (long)detail.BenefitYen) != draft.TotalBenefitYen
            || draft.Details.Sum(detail => (long)detail.BurdenYen) != draft.TotalBurdenYen)
            throw Error(ClaimErrorCode.InvalidOperationPayload);
    }

    private void ValidateIncomingEnvelopes(ClaimFinalizationDraft draft, bool requireWrite)
    {
        foreach (var envelope in draft.Details.SelectMany(detail => new[]
                 {
                     detail.InputSnapshotEnvelope,
                     detail.CalculationSnapshotEnvelope,
                 }))
        {
            if (!AsciiBounded(envelope.SchemaVersion)
                || !AsciiBounded(envelope.ValidationCodecId)
                || !LowerSha256(envelope.PayloadSha256))
                throw Error(ClaimErrorCode.InvalidSnapshotEnvelope);
            var codec = codecRegistry.Find(envelope.SchemaVersion, envelope.ValidationCodecId)
                ?? throw Error(ClaimErrorCode.UnsupportedSnapshotCodec);
            if (requireWrite && !codec.CanWrite)
                throw Error(ClaimErrorCode.UnsupportedSnapshotCodec);
            InvokeCodec(() => codec.Validate(envelope));
            var restored = InvokeCodec(
                () => codec.ReadValidated(envelope.GetCanonicalUtf8Bytes()));
            if (restored.SchemaVersion != envelope.SchemaVersion
                || restored.ValidationCodecId != envelope.ValidationCodecId
                || restored.PayloadSha256 != envelope.PayloadSha256
                || !restored.GetCanonicalUtf8Bytes().AsSpan()
                    .SequenceEqual(envelope.GetCanonicalUtf8Bytes()))
                throw Error(ClaimErrorCode.InvalidSnapshotEnvelope);
        }
    }

    private void ValidateHistory(IReadOnlyList<ClaimBatchAggregate> history)
    {
        ClaimBatchPolicy.ValidateHistory(history.Select(item => item.Header).ToArray());
        if (history.Select(item => item.Header.FinalizationOperationId).Distinct().Count() != history.Count)
            throw Error(ClaimErrorCode.InvalidHistory);
        foreach (var aggregate in history) ValidateAggregate(aggregate, requireOperationHash: true);
    }

    private void ValidateAggregate(ClaimBatchAggregate aggregate, bool requireOperationHash)
    {
        var batch = aggregate.Header;
        var details = aggregate.Details;
        if (batch.Id == Guid.Empty
            || batch.FinalizationOperationId == Guid.Empty
            || !Bounded(batch.CreatedBy)
            || !AsciiBounded(batch.ClaimMasterVersion)
            || !AsciiBounded(batch.CsvSpecificationVersion)
            || !AsciiBounded(batch.ReportSpecificationVersion)
            || !AsciiBounded(batch.SnapshotApplicationVersion)
            || !AsciiBounded(batch.OperationApplicationVersion)
            || !AsciiBounded(batch.OperationPayloadSchemaVersion)
            || !LowerSha256(batch.OperationPayloadSha256)
            || details.Any(detail =>
                detail.Id == Guid.Empty
                || detail.ClaimBatchId != batch.Id
                || detail.RecipientId == Guid.Empty
                || !Bounded(detail.CreatedBy)
                || detail.CreatedBy != batch.CreatedBy
                || !AsciiBounded(detail.SnapshotSchemaVersion)
                || !AsciiBounded(detail.ClaimMasterVersion)
                || !AsciiBounded(detail.CsvSpecificationVersion)
                || !AsciiBounded(detail.ReportSpecificationVersion)
                || !AsciiBounded(detail.SnapshotApplicationVersion)
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
        var readEntry = operationRegistry.GetReadEntry(batch.OperationPayloadSchemaVersion)
            ?? throw Error(ClaimErrorCode.UnsupportedOperationSchema);
        var payload = readEntry.Operation.Rebuild(aggregate, restored);
        var payloadHash = ValidateOperationPayloadHash(payload, ClaimErrorCode.InvalidHistory);
        if (!string.Equals(payloadHash, batch.OperationPayloadSha256, StringComparison.Ordinal))
            throw Error(ClaimErrorCode.InvalidHistory);
    }

    private ClaimFinalizationDetailDraft[] RestoreDetailDrafts(
        ClaimBatchAggregate aggregate) => aggregate.Details
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
            if (!AsciiBounded(schema) || !AsciiBounded(codecId))
                throw Error(ClaimErrorCode.InvalidSnapshotEnvelope);
            var codec = codecRegistry.Find(schema, codecId)
                ?? throw Error(ClaimErrorCode.UnsupportedSnapshotCodec);
            var envelope = InvokeCodec(() => codec.ReadValidated(bytes));
            if (envelope.SchemaVersion != schema
                || envelope.ValidationCodecId != codecId
                || !LowerSha256(envelope.PayloadSha256))
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

    private static ClaimBatch CreateBatch(
        ClaimFinalizationDraft draft,
        int revision,
        string operationHash,
        DateTimeOffset now) => draft.Kind switch
        {
            RecordKind.New => ClaimBatch.NewRecord(
                Guid.NewGuid(), draft.OfficeId, draft.ServiceMonth,
                draft.TotalUnits, draft.TotalCostYen, draft.TotalBenefitYen, draft.TotalBurdenYen,
                draft.ClaimMasterVersion, draft.CsvSpecificationVersion,
                draft.ReportSpecificationVersion, draft.SnapshotApplicationVersion,
                draft.OperationApplicationVersion, draft.FinalizationOperationId,
                ClaimBatch.CurrentOperationPayloadSchemaVersion, operationHash, draft.CreatedBy, now),
            RecordKind.Correct => ClaimBatch.Correction(
                Guid.NewGuid(), draft.OfficeId, draft.ServiceMonth, revision,
                draft.RootBatchId!.Value, draft.ExpectedHead!.BatchId, draft.ExpectedHead.Revision,
                draft.TotalUnits, draft.TotalCostYen, draft.TotalBenefitYen, draft.TotalBurdenYen,
                draft.ClaimMasterVersion, draft.CsvSpecificationVersion,
                draft.ReportSpecificationVersion, draft.SnapshotApplicationVersion,
                draft.OperationApplicationVersion, draft.FinalizationOperationId,
                ClaimBatch.CurrentOperationPayloadSchemaVersion, operationHash, draft.CreatedBy, now),
            RecordKind.Cancel => ClaimBatch.Cancellation(
                Guid.NewGuid(), draft.OfficeId, draft.ServiceMonth, revision,
                draft.RootBatchId!.Value, draft.ExpectedHead!.BatchId, draft.ExpectedHead.Revision,
                draft.ClaimMasterVersion, draft.CsvSpecificationVersion,
                draft.ReportSpecificationVersion, draft.SnapshotApplicationVersion,
                draft.OperationApplicationVersion, draft.FinalizationOperationId,
                ClaimBatch.CurrentOperationPayloadSchemaVersion, operationHash, draft.CreatedBy, now),
            _ => throw Error(ClaimErrorCode.InvalidOperationPayload),
        };

    private static ClaimDetail[] CreateDetails(
        ClaimFinalizationDraft draft,
        Guid batchId,
        DateTimeOffset now) => draft.Details
        .Select(detail => ClaimDetail.Create(
            Guid.NewGuid(), batchId, detail.RecipientId, detail.SnapshotSchemaVersion,
            detail.ClaimMasterVersion, detail.CsvSpecificationVersion,
            detail.ReportSpecificationVersion, detail.SnapshotApplicationVersion,
            Encoding.UTF8.GetString(detail.InputSnapshotEnvelope.GetCanonicalUtf8Bytes()),
            Encoding.UTF8.GetString(detail.CalculationSnapshotEnvelope.GetCanonicalUtf8Bytes()),
            detail.TotalUnits, detail.TotalCostYen, detail.BenefitYen, detail.BurdenYen,
            draft.CreatedBy, now))
        .ToArray();

    private static async Task<ClaimBatchAggregate?> FindAggregateByOperationIdAsync(
        TsumugiDbContext db,
        Guid operationId,
        CancellationToken ct)
    {
        var header = await db.ClaimBatches.AsNoTracking()
            .SingleOrDefaultAsync(batch => batch.FinalizationOperationId == operationId, ct);
        if (header is null) return null;
        var details = await db.ClaimDetails.AsNoTracking()
            .Where(detail => detail.ClaimBatchId == header.Id)
            .OrderBy(detail => detail.RecipientId)
            .ToArrayAsync(ct);
        return new ClaimBatchAggregate(header, details);
    }

    private static async Task<IReadOnlyList<ClaimBatchAggregate>> ListHistoryAggregatesAsync(
        TsumugiDbContext db,
        Guid officeId,
        Tsumugi.Domain.ValueObjects.ServiceMonth serviceMonth,
        CancellationToken ct)
    {
        var headers = await db.ClaimBatches.AsNoTracking()
            .Where(batch => batch.OfficeId == officeId && batch.ServiceMonth == serviceMonth)
            .OrderBy(batch => batch.Revision)
            .ToArrayAsync(ct);
        if (headers.Length == 0) return [];
        var ids = headers.Select(batch => batch.Id).ToArray();
        var details = await db.ClaimDetails.AsNoTracking()
            .Where(detail => ids.Contains(detail.ClaimBatchId))
            .OrderBy(detail => detail.RecipientId)
            .ToArrayAsync(ct);
        var lookup = details.ToLookup(detail => detail.ClaimBatchId);
        return headers.Select(header => new ClaimBatchAggregate(header, lookup[header.Id])).ToArray();
    }

    private static ClaimFinalizationException Error(ClaimErrorCode code) => new(code);

    private static string ValidateOperationPayloadHash(
        ClaimFinalizationOperationPayload payload,
        ClaimErrorCode errorCode)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(payload.GetCanonicalUtf8Bytes()));
        if (!string.Equals(hash, payload.Sha256, StringComparison.Ordinal))
            throw Error(errorCode);
        return hash;
    }

    private static void InvokeCodec(Action action)
        => InvokeCodec(() =>
        {
            action();
            return true;
        });

    private static T InvokeCodec<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            throw Error(ClaimErrorCode.InvalidSnapshotEnvelope);
        }
    }

    private static void ValidateDraftMetadata(ClaimFinalizationDraft draft)
    {
        if (!Bounded(draft.CreatedBy)
            || !AsciiBounded(draft.OperationApplicationVersion)
            || !AsciiBounded(draft.ClaimMasterVersion)
            || !AsciiBounded(draft.CsvSpecificationVersion)
            || !AsciiBounded(draft.ReportSpecificationVersion)
            || !AsciiBounded(draft.SnapshotApplicationVersion)
            || draft.Details.Any(detail =>
                !AsciiBounded(detail.SnapshotSchemaVersion)
                || !AsciiBounded(detail.ClaimMasterVersion)
                || !AsciiBounded(detail.CsvSpecificationVersion)
                || !AsciiBounded(detail.ReportSpecificationVersion)
                || !AsciiBounded(detail.SnapshotApplicationVersion)
                || !AsciiBounded(detail.InputSnapshotEnvelope.SchemaVersion)
                || !AsciiBounded(detail.InputSnapshotEnvelope.ValidationCodecId)
                || !LowerSha256(detail.InputSnapshotEnvelope.PayloadSha256)
                || !AsciiBounded(detail.CalculationSnapshotEnvelope.SchemaVersion)
                || !AsciiBounded(detail.CalculationSnapshotEnvelope.ValidationCodecId)
                || !LowerSha256(detail.CalculationSnapshotEnvelope.PayloadSha256)))
            throw Error(ClaimErrorCode.InvalidOperationPayload);
    }

    private static bool Bounded(string value)
        => !string.IsNullOrWhiteSpace(value) && value.Length <= 64;

    private static bool AsciiBounded(string value)
        => Bounded(value) && value.All(character => character <= 0x7f);

    private static bool LowerSha256(string value)
        => value is { Length: 64 }
            && value.All(character =>
                character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}
