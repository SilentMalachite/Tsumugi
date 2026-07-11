using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Audit;
using Tsumugi.Application.Claim;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Persistence;

namespace Tsumugi.Infrastructure.Tests.Persistence;

public sealed class ClaimFinalizationStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"tsumugi-finalization-{Guid.NewGuid():N}.db");
    private readonly RecordingFactory _factory;
    private readonly TestCodec _codec = new();

    public ClaimFinalizationStoreTests()
    {
        _factory = new RecordingFactory(_path);
        using var context = _factory.CreateDbContext();
        context.Database.Migrate();
        _factory.Contexts.Clear();
    }

    [Fact]
    public async Task Commit_New_appends_revision_one_audit_and_disposes_operation_context()
    {
        var store = CreateStore();
        var draft = Draft(Guid.NewGuid());

        var result = await store.CommitAsync(draft, default);

        result.Revision.Should().Be(1);
        result.IsReplay.Should().BeFalse();
        var operationContext = _factory.Contexts.Should().ContainSingle().Subject;
        FluentActions.Invoking(() => _ = operationContext.ChangeTracker.Entries().Count())
            .Should().Throw<ObjectDisposedException>();
        await using var verification = _factory.CreateDbContext();
        (await verification.ClaimBatches.CountAsync()).Should().Be(1);
        (await verification.ClaimDetails.CountAsync()).Should().Be(1);
        (await verification.AuditEntries.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Commit_same_operation_and_payload_is_replay_without_new_rows_or_audit()
    {
        var store = CreateStore();
        var draft = Draft(Guid.NewGuid());
        var first = await store.CommitAsync(draft, default);
        _factory.Contexts.Clear();

        var replay = await store.CommitAsync(draft, default);

        replay.Should().Be(new ClaimFinalizationResult(first.BatchId, 1, IsReplay: true));
        AssertDisposed(_factory.Contexts);
        await using var verification = _factory.CreateDbContext();
        (await verification.ClaimBatches.CountAsync()).Should().Be(1);
        (await verification.AuditEntries.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Commit_replay_rejects_forged_incoming_validation_marker()
    {
        var store = CreateStore();
        var draft = Draft(Guid.NewGuid());
        await store.CommitAsync(draft, default);
        var original = draft.Details[0].InputSnapshotEnvelope;
        var forged = ValidatedClaimSnapshotEnvelope.CreateValidated(
            original.SchemaVersion,
            original.ValidationCodecId,
            original.PayloadSha256,
            original.GetCanonicalUtf8Bytes(),
            new object());
        var collision = draft with
        {
            Details = [draft.Details[0] with { InputSnapshotEnvelope = forged }],
        };

        var action = () => store.CommitAsync(collision, default);

        await action.Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.InvalidSnapshotEnvelope);
    }

    [Fact]
    public async Task Commit_Correct_requires_matching_expected_head_and_appends_next_revision()
    {
        var store = CreateStore();
        var initial = Draft(Guid.NewGuid());
        var root = await store.CommitAsync(initial, default);
        var correction = initial with
        {
            FinalizationOperationId = Guid.NewGuid(),
            Kind = RecordKind.Correct,
            RootBatchId = root.BatchId,
            ExpectedHead = new ClaimExpectedHead(root.BatchId, root.Revision),
        };

        var result = await store.CommitAsync(correction, default);

        result.Revision.Should().Be(2);
    }

    [Fact]
    public async Task Commit_Cancel_replay_rejects_different_expected_head()
    {
        var store = CreateStore();
        var initial = Draft(Guid.NewGuid());
        var root = await store.CommitAsync(initial, default);
        var cancellation = initial with
        {
            FinalizationOperationId = Guid.NewGuid(),
            Kind = RecordKind.Cancel,
            RootBatchId = root.BatchId,
            ExpectedHead = new ClaimExpectedHead(root.BatchId, root.Revision),
            TotalUnits = 0,
            TotalCostYen = 0,
            TotalBenefitYen = 0,
            TotalBurdenYen = 0,
            Details = [],
        };
        await store.CommitAsync(cancellation, default);
        var collision = cancellation with
        {
            ExpectedHead = new ClaimExpectedHead(Guid.NewGuid(), root.Revision),
        };

        var action = () => store.CommitAsync(collision, default);

        await action.Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.OperationIdCollision);
    }

    [Fact]
    public async Task Commit_Cancel_replay_rejects_nonzero_totals_before_normalization()
    {
        var store = CreateStore();
        var (cancellation, _) = await CommitCancellationAsync(store);
        var collision = cancellation with { TotalUnits = 1 };

        var action = () => store.CommitAsync(collision, default);

        await action.Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.InvalidOperationPayload);
    }

    [Fact]
    public async Task Commit_Cancel_replay_rejects_details_before_normalization()
    {
        var store = CreateStore();
        var (cancellation, initial) = await CommitCancellationAsync(store);
        var collision = cancellation with { Details = initial.Details };

        var action = () => store.CommitAsync(collision, default);

        await action.Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.InvalidOperationPayload);
    }

    [Fact]
    public async Task Commit_Cancel_replay_rejects_forged_detail_marker_before_normalization()
    {
        var store = CreateStore();
        var (cancellation, initial) = await CommitCancellationAsync(store);
        var original = initial.Details[0].InputSnapshotEnvelope;
        var forged = ValidatedClaimSnapshotEnvelope.CreateValidated(
            original.SchemaVersion,
            original.ValidationCodecId,
            original.PayloadSha256,
            original.GetCanonicalUtf8Bytes(),
            new object());
        var collision = cancellation with
        {
            Details = [initial.Details[0] with { InputSnapshotEnvelope = forged }],
        };

        var action = () => store.CommitAsync(collision, default);

        await action.Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.InvalidSnapshotEnvelope);
    }

    [Fact]
    public async Task Commit_rejects_unavailable_codec_before_adding_entities()
    {
        var store = new ClaimFinalizationStore(
            _factory,
            new ClaimFinalizationOperationRegistry(),
            new EmptyCodecRegistry(),
            new ClaimAuditEntryFactory(),
            TimeProvider.System);

        var action = () => store.CommitAsync(Draft(Guid.NewGuid()), default);

        await action.Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.UnsupportedSnapshotCodec);
        AssertDisposed(_factory.Contexts);
        await using var verification = _factory.CreateDbContext();
        (await verification.ClaimBatches.CountAsync()).Should().Be(0);
        (await verification.AuditEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Commit_rejects_empty_New_when_registry_has_no_write_support()
    {
        var store = new ClaimFinalizationStore(
            _factory,
            new ClaimFinalizationOperationRegistry(),
            new EmptyCodecRegistry(),
            new ClaimAuditEntryFactory(),
            TimeProvider.System);
        var draft = Draft(Guid.NewGuid()) with
        {
            TotalUnits = 0,
            TotalCostYen = 0,
            TotalBenefitYen = 0,
            TotalBurdenYen = 0,
            Details = [],
        };

        var action = () => store.CommitAsync(draft, default);

        await action.Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.UnsupportedSnapshotCodec);
    }

    [Fact]
    public async Task Commit_audit_factory_failure_rolls_back_without_audit()
    {
        var store = new ClaimFinalizationStore(
            _factory,
            new ClaimFinalizationOperationRegistry(),
            new TestCodecRegistry(_codec),
            new ThrowingAuditFactory(),
            TimeProvider.System);

        var action = () => store.CommitAsync(Draft(Guid.NewGuid()), default);

        await action.Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.PersistenceFailure);
        AssertDisposed(_factory.Contexts);
        await using var verification = _factory.CreateDbContext();
        (await verification.ClaimBatches.CountAsync()).Should().Be(0);
        (await verification.ClaimDetails.CountAsync()).Should().Be(0);
        (await verification.AuditEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Replay_rejects_empty_persisted_batch_id_even_though_id_is_not_hashed()
    {
        var store = CreateStore();
        var draft = Draft(Guid.NewGuid()) with
        {
            TotalUnits = 0,
            TotalCostYen = 0,
            TotalBenefitYen = 0,
            TotalBurdenYen = 0,
            Details = [],
        };
        await store.CommitAsync(draft, default);
        await using (var tamper = _factory.CreateDbContext())
            await tamper.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE ClaimBatches SET Id = {Guid.Empty} WHERE FinalizationOperationId = {draft.FinalizationOperationId}");

        var action = () => store.CommitAsync(draft, default);

        await action.Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.InvalidHistory);
    }

    [Fact]
    public async Task Replay_rejects_empty_persisted_detail_id_even_though_id_is_not_hashed()
    {
        var store = CreateStore();
        var draft = Draft(Guid.NewGuid());
        await store.CommitAsync(draft, default);
        await using (var tamper = _factory.CreateDbContext())
            await tamper.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE ClaimDetails SET Id = {Guid.Empty}");

        var action = () => store.CommitAsync(draft, default);

        await action.Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.InvalidHistory);
    }

    [Fact]
    public async Task Replay_rejects_empty_persisted_recipient_id_with_matching_operation_hash()
    {
        var store = CreateStore();
        var draft = Draft(Guid.NewGuid());
        await store.CommitAsync(draft, default);
        var tamperedDraft = draft with
        {
            Details = [draft.Details[0] with { RecipientId = Guid.Empty }],
        };
        await using (var tamper = _factory.CreateDbContext())
        {
            await tamper.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE ClaimDetails SET RecipientId = {Guid.Empty}");
            var hash = ReplaceAndHash(
                new ClaimFinalizationOperationV1().Canonicalize(draft),
                $"\"recipientId\":\"{draft.Details[0].RecipientId:D}\"",
                $"\"recipientId\":\"{Guid.Empty:D}\"");
            await SetOperationHashAsync(tamper, draft.FinalizationOperationId, hash);
        }

        var action = () => store.CommitAsync(tamperedDraft, default);

        await action.Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.InvalidHistory);
    }

    [Fact]
    public async Task Replay_rejects_empty_persisted_created_by_with_matching_operation_hash()
    {
        var store = CreateStore();
        var draft = Draft(Guid.NewGuid());
        await store.CommitAsync(draft, default);
        var tamperedDraft = draft with { CreatedBy = string.Empty };
        await using (var tamper = _factory.CreateDbContext())
        {
            await tamper.Database.ExecuteSqlRawAsync(
                "UPDATE ClaimBatches SET CreatedBy = ''; UPDATE ClaimDetails SET CreatedBy = '';");
            var hash = ReplaceAndHash(
                new ClaimFinalizationOperationV1().Canonicalize(draft),
                "\"createdBy\":\"actor\"",
                "\"createdBy\":\"\"");
            await SetOperationHashAsync(tamper, draft.FinalizationOperationId, hash);
        }

        var action = () => store.CommitAsync(tamperedDraft, default);

        await action.Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.InvalidHistory);
    }

    [Fact]
    public async Task Replay_rejects_65_character_persisted_created_by_before_rebuild()
    {
        var store = CreateStore();
        var draft = Draft(Guid.NewGuid());
        await store.CommitAsync(draft, default);
        var value = new string('a', 65);
        var hash = ReplaceAndHash(
            new ClaimFinalizationOperationV1().Canonicalize(draft),
            "\"createdBy\":\"actor\"",
            $"\"createdBy\":\"{value}\"");
        await using (var tamper = _factory.CreateDbContext())
        {
            await tamper.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE ClaimBatches SET CreatedBy = {value}, OperationPayloadSha256 = {hash}");
            await tamper.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE ClaimDetails SET CreatedBy = {value}");
        }

        var action = () => store.CommitAsync(draft with { CreatedBy = value }, default);

        await action.Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.InvalidHistory);
    }

    [Fact]
    public async Task Replay_rejects_65_character_persisted_versions_before_rebuild()
    {
        var store = CreateStore();
        var draft = Draft(Guid.NewGuid());
        await store.CommitAsync(draft, default);
        var value = new string('a', 65);
        var hash = ReplaceAndHash(
            new ClaimFinalizationOperationV1().Canonicalize(draft),
            "\"claimMasterVersion\":\"master-v1\"",
            $"\"claimMasterVersion\":\"{value}\"");
        await using (var tamper = _factory.CreateDbContext())
        {
            await tamper.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE ClaimBatches SET ClaimMasterVersion = {value}, OperationPayloadSha256 = {hash}");
            await tamper.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE ClaimDetails SET ClaimMasterVersion = {value}");
        }
        var detail = draft.Details[0] with { ClaimMasterVersion = value };
        var incoming = draft with { ClaimMasterVersion = value, Details = [detail] };

        var action = () => store.CommitAsync(incoming, default);

        await action.Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.InvalidHistory);
    }

    [Fact]
    public async Task Replay_rejects_65_character_operation_schema_as_invalid_history()
    {
        var store = CreateStore();
        var draft = Draft(Guid.NewGuid());
        await store.CommitAsync(draft, default);
        var value = new string('a', 65);
        await using (var tamper = _factory.CreateDbContext())
            await tamper.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE ClaimBatches SET OperationPayloadSchemaVersion = {value}");

        var action = () => store.CommitAsync(draft, default);

        await action.Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.InvalidHistory);
    }

    [Fact]
    public async Task Replay_rejects_negative_persisted_detail_totals_even_when_they_offset_and_hash_matches()
    {
        var store = CreateStore();
        var draft = DraftWithTwoDetails(Guid.NewGuid());
        await store.CommitAsync(draft, default);
        var first = draft.Details[0] with
        {
            TotalUnits = -1,
            TotalCostYen = -10,
            BenefitYen = -8,
            BurdenYen = -2,
        };
        var second = draft.Details[1] with
        {
            TotalUnits = 2,
            TotalCostYen = 20,
            BenefitYen = 16,
            BurdenYen = 4,
        };
        var tamperedDraft = draft with { Details = [first, second] };
        await using (var tamper = _factory.CreateDbContext())
        {
            await tamper.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE ClaimDetails SET TotalUnits = -1, TotalCostYen = -10, BenefitYen = -8, BurdenYen = -2 WHERE RecipientId = {first.RecipientId}");
            await tamper.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE ClaimDetails SET TotalUnits = 2, TotalCostYen = 20, BenefitYen = 16, BurdenYen = 4 WHERE RecipientId = {second.RecipientId}");
            var hash = ReplaceDetailTotalsAndHash(
                new ClaimFinalizationOperationV1().Canonicalize(draft),
                (first.RecipientId, -1, -10, -8, -2),
                (second.RecipientId, 2, 20, 16, 4));
            await SetOperationHashAsync(tamper, draft.FinalizationOperationId, hash);
        }

        var action = () => store.CommitAsync(tamperedDraft, default);

        await action.Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.InvalidHistory);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":\"claim-snapshot-v1\"}")]
    [InlineData("{\"schemaVersion\":1,\"validationCodecId\":\"test-codec-v1\",\"payloadSha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"payload\":{}}")]
    [InlineData("{\"schemaVersion\":\"claim-snapshot-v1\",\"validationCodecId\":\"test-codec-v1\",\"payloadSha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"payload\":{}}{}")]
    public async Task Replay_maps_malformed_snapshot_json_to_typed_invalid_envelope(string malformedJson)
    {
        var store = CreateStore();
        var draft = Draft(Guid.NewGuid());
        await store.CommitAsync(draft, default);
        await using (var tamper = _factory.CreateDbContext())
            await tamper.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE ClaimDetails SET InputSnapshotJson = {malformedJson}");

        var action = () => store.CommitAsync(draft, default);

        await action.Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.InvalidSnapshotEnvelope);
    }

    [Fact]
    public async Task Replay_uses_v1_reader_after_v2_writer_is_registered_and_v1_write_is_disabled()
    {
        var draft = Draft(Guid.NewGuid());
        await CreateStore().CommitAsync(draft, default);
        var v1 = new ClaimFinalizationOperationV1();
        var registry = new ClaimFinalizationOperationRegistry([
            new ClaimFinalizationOperationEntry(v1, CanWrite: false),
            new ClaimFinalizationOperationEntry(new V2Operation(), CanWrite: true),
        ]);
        var replayStore = new ClaimFinalizationStore(
            _factory,
            registry,
            new TestCodecRegistry(_codec),
            new ClaimAuditEntryFactory(),
            TimeProvider.System);

        var result = await replayStore.CommitAsync(draft, default);

        result.IsReplay.Should().BeTrue();
        await using var verification = _factory.CreateDbContext();
        (await verification.ClaimBatches.CountAsync()).Should().Be(1);
        (await verification.AuditEntries.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Replay_rejects_unknown_operation_schema_without_v1_fallback()
    {
        var store = CreateStore();
        var draft = Draft(Guid.NewGuid());
        await store.CommitAsync(draft, default);
        await using (var tamper = _factory.CreateDbContext())
            await tamper.Database.ExecuteSqlRawAsync(
                "UPDATE ClaimBatches SET OperationPayloadSchemaVersion = 'unknown-operation-v9'");

        var action = () => store.CommitAsync(draft, default);

        await action.Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.UnsupportedOperationSchema);
    }

    [Fact]
    public async Task Concurrent_New_uses_separate_contexts_and_commits_only_revision_one()
    {
        var store = CreateStore();
        var first = Draft(Guid.NewGuid());
        var second = first with
        {
            FinalizationOperationId = Guid.NewGuid(),
            Details = [first.Details[0] with { RecipientId = Guid.NewGuid() }],
        };

        var outcomes = await Task.WhenAll(
            CaptureAsync(() => store.CommitAsync(first, default)),
            CaptureAsync(() => store.CommitAsync(second, default)));

        outcomes.Count(outcome => outcome.Result is not null).Should().Be(1);
        outcomes.Count(outcome => outcome.Error?.Code is ClaimErrorCode.ExpectedHeadMismatch).Should().Be(1);
        _factory.Contexts.Should().HaveCount(2);
        AssertDisposed(_factory.Contexts);
        await using var verification = _factory.CreateDbContext();
        var revisions = await verification.ClaimBatches.Select(batch => batch.Revision).ToArrayAsync();
        revisions.Should().Equal(1);
        (await verification.AuditEntries.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Concurrent_Correct_and_Cancel_from_same_head_commit_only_one_revision_two()
    {
        var store = CreateStore();
        var initial = Draft(Guid.NewGuid());
        var head = await store.CommitAsync(initial, default);
        var correction = initial with
        {
            FinalizationOperationId = Guid.NewGuid(),
            Kind = RecordKind.Correct,
            RootBatchId = head.BatchId,
            ExpectedHead = new ClaimExpectedHead(head.BatchId, head.Revision),
        };
        var cancellation = correction with
        {
            FinalizationOperationId = Guid.NewGuid(),
            Kind = RecordKind.Cancel,
            TotalUnits = 0,
            TotalCostYen = 0,
            TotalBenefitYen = 0,
            TotalBurdenYen = 0,
            Details = [],
        };
        _factory.Contexts.Clear();

        var outcomes = await Task.WhenAll(
            CaptureAsync(() => store.CommitAsync(correction, default)),
            CaptureAsync(() => store.CommitAsync(cancellation, default)));

        outcomes.Count(outcome => outcome.Result is not null).Should().Be(1);
        outcomes.Count(outcome => outcome.Error?.Code is ClaimErrorCode.ExpectedHeadMismatch).Should().Be(1);
        _factory.Contexts.Should().HaveCount(2);
        AssertDisposed(_factory.Contexts);
        await using var verification = _factory.CreateDbContext();
        var revisions = await verification.ClaimBatches
            .OrderBy(batch => batch.Revision)
            .Select(batch => batch.Revision)
            .ToArrayAsync();
        revisions.Should().Equal(1, 2);
        (await verification.AuditEntries.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Busy_write_lock_adds_no_rows_or_audit_and_disposes_local_context()
    {
        await using var blocker = _factory.CreateDbContext();
        await blocker.Database.OpenConnectionAsync();
        var connection = (SqliteConnection)blocker.Database.GetDbConnection();
        await using var transaction = connection.BeginTransaction(deferred: false);
        var busyFactory = new RecordingFactory(_path, defaultTimeoutSeconds: 1);
        var store = CreateStore(busyFactory, _codec);

        var action = () => store.CommitAsync(Draft(Guid.NewGuid()), default);

        await action.Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.PersistenceFailure);
        AssertDisposed(busyFactory.Contexts);
        await transaction.RollbackAsync();
        await using var verification = _factory.CreateDbContext();
        (await verification.ClaimBatches.CountAsync()).Should().Be(0);
        (await verification.ClaimDetails.CountAsync()).Should().Be(0);
        (await verification.AuditEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Audit_primary_key_constraint_rolls_back_batch_details_and_new_audit()
    {
        var duplicateAuditId = Guid.NewGuid();
        await using (var setup = _factory.CreateDbContext())
        {
            setup.AuditEntries.Add(Tsumugi.Domain.Entities.AuditEntry.Create(
                duplicateAuditId,
                "setup",
                AuditAction.Register,
                "Setup",
                Guid.NewGuid(),
                DateTimeOffset.UnixEpoch,
                null,
                DateTimeOffset.UnixEpoch,
                "setup"));
            await setup.SaveChangesAsync();
        }
        _factory.Contexts.Clear();
        var store = new ClaimFinalizationStore(
            _factory,
            new ClaimFinalizationOperationRegistry(),
            new TestCodecRegistry(_codec),
            new FixedAuditIdFactory(duplicateAuditId),
            TimeProvider.System);

        var action = () => store.CommitAsync(Draft(Guid.NewGuid()), default);

        await action.Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.PersistenceFailure);
        AssertDisposed(_factory.Contexts);
        await using var verification = _factory.CreateDbContext();
        (await verification.ClaimBatches.CountAsync()).Should().Be(0);
        (await verification.ClaimDetails.CountAsync()).Should().Be(0);
        (await verification.AuditEntries.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Forced_Save_failure_rolls_back_all_candidate_rows_and_disposes_context()
    {
        await using (var setup = _factory.CreateDbContext())
            await setup.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER FailClaimBatchInsert
                BEFORE INSERT ON ClaimBatches
                BEGIN
                    SELECT RAISE(ABORT, 'forced-save-failure');
                END;
                """);
        _factory.Contexts.Clear();

        var action = () => CreateStore().CommitAsync(Draft(Guid.NewGuid()), default);

        await action.Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.PersistenceFailure);
        AssertDisposed(_factory.Contexts);
        await using var verification = _factory.CreateDbContext();
        (await verification.ClaimBatches.CountAsync()).Should().Be(0);
        (await verification.ClaimDetails.CountAsync()).Should().Be(0);
        (await verification.AuditEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Replay_allows_read_enabled_write_disabled_old_codec()
    {
        var marker = new object();
        var writerCodec = new TestCodec(canWrite: true, marker);
        var writerStore = CreateStore(_factory, writerCodec);
        var draft = Draft(Guid.NewGuid(), writerCodec);
        await writerStore.CommitAsync(draft, default);
        var readerCodec = new TestCodec(canWrite: false, marker);
        var replayStore = CreateStore(_factory, readerCodec);

        var replay = await replayStore.CommitAsync(draft, default);

        replay.IsReplay.Should().BeTrue();
    }

    [Fact]
    public async Task Commit_does_not_touch_long_lived_scoped_context_tracker()
    {
        await using var longLived = _factory.CreateDbContext();
        longLived.ChangeTracker.Entries().Should().BeEmpty();
        _factory.Contexts.Clear();

        await CreateStore().CommitAsync(Draft(Guid.NewGuid()), default);

        longLived.ChangeTracker.Entries().Should().BeEmpty();
        AssertDisposed(_factory.Contexts);
    }

    private ClaimFinalizationStore CreateStore() => new(
        _factory,
        new ClaimFinalizationOperationRegistry(),
        new TestCodecRegistry(_codec),
        new ClaimAuditEntryFactory(),
        TimeProvider.System);

    private static ClaimFinalizationStore CreateStore(
        IDbContextFactory<TsumugiDbContext> factory,
        TestCodec codec) => new(
            factory,
            new ClaimFinalizationOperationRegistry(),
            new TestCodecRegistry(codec),
            new ClaimAuditEntryFactory(),
            TimeProvider.System);

    private async Task<(ClaimFinalizationDraft Cancellation, ClaimFinalizationDraft Initial)>
        CommitCancellationAsync(ClaimFinalizationStore store)
    {
        var initial = Draft(Guid.NewGuid());
        var root = await store.CommitAsync(initial, default);
        var cancellation = initial with
        {
            FinalizationOperationId = Guid.NewGuid(),
            Kind = RecordKind.Cancel,
            RootBatchId = root.BatchId,
            ExpectedHead = new ClaimExpectedHead(root.BatchId, root.Revision),
            TotalUnits = 0,
            TotalCostYen = 0,
            TotalBenefitYen = 0,
            TotalBurdenYen = 0,
            Details = [],
        };
        await store.CommitAsync(cancellation, default);
        return (cancellation, initial);
    }

    private ClaimFinalizationDraft Draft(Guid operationId, TestCodec? codec = null)
    {
        var envelope = (codec ?? _codec).Create("{\"value\":1}");
        return new ClaimFinalizationDraft(
            operationId, RecordKind.New, Guid.NewGuid(), new ServiceMonth(2026, 6),
            null, null, "actor", "operation-app-v1", "master-v1", "csv-v1", "report-v1",
            "snapshot-app-v1", 1, 10, 8, 2,
            [new ClaimFinalizationDetailDraft(
                Guid.NewGuid(), TestCodec.Schema, "master-v1", "csv-v1", "report-v1", "snapshot-app-v1",
                envelope, envelope, 1, 10, 8, 2)]);
    }

    private ClaimFinalizationDraft DraftWithTwoDetails(Guid operationId)
    {
        var draft = Draft(operationId);
        return draft with
        {
            Details =
            [
                draft.Details[0] with
                {
                    TotalUnits = 0,
                    TotalCostYen = 0,
                    BenefitYen = 0,
                    BurdenYen = 0,
                },
                draft.Details[0] with { RecipientId = Guid.NewGuid() },
            ],
        };
    }

    private static async Task SetOperationHashAsync(
        TsumugiDbContext context,
        Guid operationId,
        string hash)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE ClaimBatches SET OperationPayloadSha256 = {hash} WHERE FinalizationOperationId = {operationId}");
    }

    private static string ReplaceDetailTotalsAndHash(
        ClaimFinalizationOperationPayload payload,
        params (Guid RecipientId, int Units, int Cost, int Benefit, int Burden)[] replacements)
    {
        var json = Encoding.UTF8.GetString(payload.GetCanonicalUtf8Bytes());
        foreach (var replacement in replacements)
        {
            var detailStart = json.IndexOf(
                $"\"recipientId\":\"{replacement.RecipientId:D}\"",
                StringComparison.Ordinal);
            detailStart.Should().BeGreaterThanOrEqualTo(0);
            json = ReplaceNumber(json, detailStart, "\"totalUnits\":", replacement.Units);
            json = ReplaceNumber(json, detailStart, "\"totalCostYen\":", replacement.Cost);
            json = ReplaceNumber(json, detailStart, "\"benefitYen\":", replacement.Benefit);
            json = ReplaceNumber(json, detailStart, "\"burdenYen\":", replacement.Burden);
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    private static string ReplaceNumber(string json, int startIndex, string property, int value)
    {
        var valueStart = json.IndexOf(property, startIndex, StringComparison.Ordinal) + property.Length;
        valueStart.Should().BeGreaterThanOrEqualTo(property.Length);
        var valueEnd = json.IndexOfAny([',', '}'], valueStart);
        return string.Concat(
            json.AsSpan(0, valueStart),
            value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            json.AsSpan(valueEnd));
    }

    private static string ReplaceAndHash(
        ClaimFinalizationOperationPayload payload,
        string oldValue,
        string newValue)
    {
        var json = Encoding.UTF8.GetString(payload.GetCanonicalUtf8Bytes());
        var changed = Encoding.UTF8.GetBytes(json.Replace(oldValue, newValue, StringComparison.Ordinal));
        return Convert.ToHexStringLower(SHA256.HashData(changed));
    }

    private static async Task<CommitOutcome> CaptureAsync(
        Func<Task<ClaimFinalizationResult>> action)
    {
        try
        {
            return new CommitOutcome(await action(), null);
        }
        catch (ClaimFinalizationException exception)
        {
            return new CommitOutcome(null, exception);
        }
    }

    private static void AssertDisposed(IEnumerable<TsumugiDbContext> contexts)
    {
        foreach (var context in contexts)
            FluentActions.Invoking(() => _ = context.ChangeTracker.Entries().Count())
                .Should().Throw<ObjectDisposedException>();
    }

    private sealed record CommitOutcome(
        ClaimFinalizationResult? Result,
        ClaimFinalizationException? Error);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _path, _path + "-shm", _path + "-wal" })
            if (File.Exists(path)) File.Delete(path);
    }

    private sealed class RecordingFactory(
        string path,
        int defaultTimeoutSeconds = 30) : IDbContextFactory<TsumugiDbContext>
    {
        internal ConcurrentBag<TsumugiDbContext> Contexts { get; } = [];

        public TsumugiDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<TsumugiDbContext>()
                .UseSqlite($"Data Source={path};Default Timeout={defaultTimeoutSeconds}")
                .Options;
            var context = new TsumugiDbContext(options);
            Contexts.Add(context);
            return context;
        }

        public Task<TsumugiDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class TestCodecRegistry(TestCodec codec) : IClaimSnapshotValidationCodecRegistry
    {
        public bool HasWriteSupport => codec.CanWrite;

        public IClaimSnapshotValidationCodec? Find(string schemaVersion, string validationCodecId)
            => schemaVersion == TestCodec.Schema && validationCodecId == TestCodec.Id ? codec : null;
    }

    private sealed class EmptyCodecRegistry : IClaimSnapshotValidationCodecRegistry
    {
        public bool HasWriteSupport => false;

        public IClaimSnapshotValidationCodec? Find(string schemaVersion, string validationCodecId) => null;
    }

    private sealed class ThrowingAuditFactory : IClaimAuditEntryFactory
    {
        public Tsumugi.Domain.Entities.AuditEntry Create(
            Guid auditEntryId,
            string actor,
            ClaimAuditPayload payload,
            DateTimeOffset occurredAt) => throw new InvalidOperationException("audit failed");
    }

    private sealed class FixedAuditIdFactory(Guid fixedId) : IClaimAuditEntryFactory
    {
        public Tsumugi.Domain.Entities.AuditEntry Create(
            Guid auditEntryId,
            string actor,
            ClaimAuditPayload payload,
            DateTimeOffset occurredAt) => Tsumugi.Domain.Entities.AuditEntry.Create(
                fixedId,
                actor,
                AuditAction.Register,
                nameof(Tsumugi.Domain.Entities.ClaimBatch),
                payload.BatchId,
                occurredAt,
                "forced-duplicate",
                occurredAt,
                actor);
    }

    private sealed class V2Operation : IClaimFinalizationOperation
    {
        private readonly ClaimFinalizationOperationV1 _v1 = new();

        public string SchemaVersion => "claim-finalization-operation-v2";

        public ClaimFinalizationOperationPayload Canonicalize(ClaimFinalizationDraft draft)
            => _v1.Canonicalize(draft);

        public ClaimFinalizationOperationPayload Rebuild(
            ClaimBatchAggregate aggregate,
            IReadOnlyList<ClaimFinalizationDetailDraft> details)
            => _v1.Rebuild(aggregate, details);
    }

    private sealed class TestCodec : IClaimSnapshotValidationCodec
    {
        internal const string Schema = "claim-snapshot-v1";
        internal const string Id = "test-codec-v1";
        private readonly object _marker;
        private readonly bool _canWrite;

        internal TestCodec(bool canWrite = true, object? marker = null)
        {
            _canWrite = canWrite;
            _marker = marker ?? new object();
        }

        public string SchemaVersion => Schema;
        public string ValidationCodecId => Id;
        public bool CanWrite => _canWrite;

        internal ValidatedClaimSnapshotEnvelope Create(string payloadJson)
        {
            var payload = Encoding.UTF8.GetBytes(payloadJson);
            var payloadHash = Convert.ToHexStringLower(SHA256.HashData(payload));
            var bytes = Encoding.UTF8.GetBytes(
                $"{{\"schemaVersion\":\"{Schema}\",\"validationCodecId\":\"{Id}\",\"payloadSha256\":\"{payloadHash}\",\"payload\":{payloadJson}}}");
            return ValidatedClaimSnapshotEnvelope.CreateValidated(Schema, Id, payloadHash, bytes, _marker);
        }

        public void Validate(ValidatedClaimSnapshotEnvelope envelope)
        {
            if (!ReferenceEquals(envelope.ValidationMarker, _marker))
                throw new ClaimFinalizationException(ClaimErrorCode.InvalidSnapshotEnvelope);
            _ = ReadValidated(envelope.GetCanonicalUtf8Bytes());
        }

        public ValidatedClaimSnapshotEnvelope ReadValidated(ReadOnlyMemory<byte> canonicalUtf8)
        {
            using var document = JsonDocument.Parse(canonicalUtf8);
            var root = document.RootElement;
            var payloadRaw = root.GetProperty("payload").GetRawText();
            var payloadBytes = Encoding.UTF8.GetBytes(payloadRaw);
            var payloadHash = Convert.ToHexStringLower(SHA256.HashData(payloadBytes));
            if (root.GetProperty("schemaVersion").GetString() != Schema
                || root.GetProperty("validationCodecId").GetString() != Id
                || root.GetProperty("payloadSha256").GetString() != payloadHash)
                throw new ClaimFinalizationException(ClaimErrorCode.InvalidSnapshotEnvelope);
            var roundTrip = Encoding.UTF8.GetBytes(root.GetRawText());
            if (!roundTrip.AsSpan().SequenceEqual(canonicalUtf8.Span))
                throw new ClaimFinalizationException(ClaimErrorCode.InvalidSnapshotEnvelope);
            return ValidatedClaimSnapshotEnvelope.CreateValidated(Schema, Id, payloadHash, roundTrip, _marker);
        }
    }
}
