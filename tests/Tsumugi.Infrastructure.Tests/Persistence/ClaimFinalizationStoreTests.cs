using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
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

        var replay = await store.CommitAsync(draft, default);

        replay.Should().Be(new ClaimFinalizationResult(first.BatchId, 1, IsReplay: true));
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
        await using var verification = _factory.CreateDbContext();
        (await verification.ClaimBatches.CountAsync()).Should().Be(0);
        (await verification.ClaimDetails.CountAsync()).Should().Be(0);
        (await verification.AuditEntries.CountAsync()).Should().Be(0);
    }

    private ClaimFinalizationStore CreateStore() => new(
        _factory,
        new ClaimFinalizationOperationRegistry(),
        new TestCodecRegistry(_codec),
        new ClaimAuditEntryFactory(),
        TimeProvider.System);

    private ClaimFinalizationDraft Draft(Guid operationId)
    {
        var envelope = _codec.Create("{\"value\":1}");
        return new ClaimFinalizationDraft(
            operationId, RecordKind.New, Guid.NewGuid(), new ServiceMonth(2026, 6),
            null, null, "actor", "operation-app-v1", "master-v1", "csv-v1", "report-v1",
            "snapshot-app-v1", 1, 10, 8, 2,
            [new ClaimFinalizationDetailDraft(
                Guid.NewGuid(), TestCodec.Schema, "master-v1", "csv-v1", "report-v1", "snapshot-app-v1",
                envelope, envelope, 1, 10, 8, 2)]);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _path, _path + "-shm", _path + "-wal" })
            if (File.Exists(path)) File.Delete(path);
    }

    private sealed class RecordingFactory(string path) : IDbContextFactory<TsumugiDbContext>
    {
        internal List<TsumugiDbContext> Contexts { get; } = [];

        public TsumugiDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<TsumugiDbContext>()
                .UseSqlite($"Data Source={path}")
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

    private sealed class TestCodec : IClaimSnapshotValidationCodec
    {
        internal const string Schema = "claim-snapshot-v1";
        internal const string Id = "test-codec-v1";
        private readonly object _marker = new();

        public string SchemaVersion => Schema;
        public string ValidationCodecId => Id;
        public bool CanWrite => true;

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
