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

            var revision = ClaimBatchPolicy.NextRevision(
                history.Select(item => item.Header).ToArray());
            var now = clock.GetUtcNow();
            var batch = CreateBatch(effectiveDraft, revision, operationPayload.Sha256, now);
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
        catch (SqliteException exception)
        {
            throw Error(ClaimErrorCode.PersistenceFailure, exception);
        }
        catch (DbUpdateException exception)
        {
            throw Error(ClaimErrorCode.PersistenceFailure, exception);
        }
        catch (InvalidOperationException exception)
        {
            throw Error(ClaimErrorCode.InvalidHistory, exception);
        }
        catch (ArgumentException exception)
        {
            throw Error(ClaimErrorCode.InvalidOperationPayload, exception);
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
        var rebuilt = readEntry.Operation is ClaimFinalizationOperationV1 v1
            ? v1.Rebuild(aggregate, persistedDetails)
            : readEntry.Operation.Canonicalize(ToDraft(aggregate, persistedDetails));
        if (!string.Equals(
                rebuilt.Sha256,
                aggregate.Header.OperationPayloadSha256,
                StringComparison.Ordinal))
            throw Error(ClaimErrorCode.InvalidHistory);

        var normalizedIncoming = aggregate.Header.Kind == RecordKind.Cancel
            ? incoming with
            {
                ClaimMasterVersion = aggregate.Header.ClaimMasterVersion,
                CsvSpecificationVersion = aggregate.Header.CsvSpecificationVersion,
                ReportSpecificationVersion = aggregate.Header.ReportSpecificationVersion,
                SnapshotApplicationVersion = aggregate.Header.SnapshotApplicationVersion,
                TotalUnits = 0,
                TotalCostYen = 0,
                TotalBenefitYen = 0,
                TotalBurdenYen = 0,
                Details = [],
            }
            : incoming;
        ValidateIncomingEnvelopes(normalizedIncoming, requireWrite: false);
        var requested = readEntry.Operation.Canonicalize(normalizedIncoming);
        if (!string.Equals(requested.Sha256, rebuilt.Sha256, StringComparison.Ordinal)
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
            || draft.Details.Sum(detail => detail.TotalUnits) != draft.TotalUnits
            || draft.Details.Sum(detail => detail.TotalCostYen) != draft.TotalCostYen
            || draft.Details.Sum(detail => detail.BenefitYen) != draft.TotalBenefitYen
            || draft.Details.Sum(detail => detail.BurdenYen) != draft.TotalBurdenYen)
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
            var codec = codecRegistry.Find(envelope.SchemaVersion, envelope.ValidationCodecId)
                ?? throw Error(ClaimErrorCode.UnsupportedSnapshotCodec);
            if (requireWrite && !codec.CanWrite)
                throw Error(ClaimErrorCode.UnsupportedSnapshotCodec);
            codec.Validate(envelope);
            var restored = codec.ReadValidated(envelope.GetCanonicalUtf8Bytes());
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
        if (details.Any(detail =>
                detail.ClaimBatchId != batch.Id
                || detail.CreatedBy != batch.CreatedBy
                || detail.SnapshotSchemaVersion.Length == 0
                || detail.ClaimMasterVersion != batch.ClaimMasterVersion
                || detail.CsvSpecificationVersion != batch.CsvSpecificationVersion
                || detail.ReportSpecificationVersion != batch.ReportSpecificationVersion
                || detail.SnapshotApplicationVersion != batch.SnapshotApplicationVersion)
            || details.Select(detail => detail.RecipientId).Distinct().Count() != details.Count
            || (batch.Kind == RecordKind.Cancel && details.Count != 0)
            || (batch.Kind != RecordKind.Cancel
                && (details.Sum(detail => detail.TotalUnits) != batch.TotalUnits
                    || details.Sum(detail => detail.TotalCostYen) != batch.TotalCostYen
                    || details.Sum(detail => detail.BenefitYen) != batch.TotalBenefitYen
                    || details.Sum(detail => detail.BurdenYen) != batch.TotalBurdenYen)))
            throw Error(ClaimErrorCode.InvalidHistory);

        var restored = RestoreDetailDrafts(aggregate);
        if (!requireOperationHash) return;
        var readEntry = operationRegistry.GetReadEntry(batch.OperationPayloadSchemaVersion)
            ?? throw Error(ClaimErrorCode.UnsupportedOperationSchema);
        var payload = readEntry.Operation is ClaimFinalizationOperationV1 v1
            ? v1.Rebuild(aggregate, restored)
            : readEntry.Operation.Canonicalize(ToDraft(aggregate, restored));
        if (!string.Equals(payload.Sha256, batch.OperationPayloadSha256, StringComparison.Ordinal))
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
            var schema = root.GetProperty("schemaVersion").GetString();
            var codecId = root.GetProperty("validationCodecId").GetString();
            if (schema != expectedSchema || schema is null || codecId is null)
                throw Error(ClaimErrorCode.InvalidSnapshotEnvelope);
            var codec = codecRegistry.Find(schema, codecId)
                ?? throw Error(ClaimErrorCode.UnsupportedSnapshotCodec);
            var envelope = codec.ReadValidated(bytes);
            if (envelope.SchemaVersion != schema || envelope.ValidationCodecId != codecId)
                throw Error(ClaimErrorCode.InvalidSnapshotEnvelope);
            return envelope;
        }
        catch (ClaimFinalizationException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw Error(ClaimErrorCode.InvalidSnapshotEnvelope, exception);
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

    private static ClaimFinalizationDraft ToDraft(
        ClaimBatchAggregate aggregate,
        IReadOnlyList<ClaimFinalizationDetailDraft> details)
    {
        var batch = aggregate.Header;
        return new ClaimFinalizationDraft(
            batch.FinalizationOperationId,
            batch.Kind,
            batch.OfficeId,
            batch.ServiceMonth,
            batch.OriginId,
            batch.ExpectedHeadBatchId is null
                ? null
                : new ClaimExpectedHead(batch.ExpectedHeadBatchId.Value, batch.ExpectedHeadRevision!.Value),
            batch.CreatedBy,
            batch.OperationApplicationVersion,
            batch.ClaimMasterVersion,
            batch.CsvSpecificationVersion,
            batch.ReportSpecificationVersion,
            batch.SnapshotApplicationVersion,
            batch.TotalUnits,
            batch.TotalCostYen,
            batch.TotalBenefitYen,
            batch.TotalBurdenYen,
            details);
    }

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

    private static ClaimFinalizationException Error(
        ClaimErrorCode code,
        Exception? inner = null) => new(code, innerException: inner);
}
