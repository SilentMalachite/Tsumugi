using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Persistence;

namespace Tsumugi.Infrastructure.Tests.Persistence;

public sealed class ClaimBatchRepositoryTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;

    public ClaimBatchRepositoryTests(SqliteFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ListHistoryAggregates_returns_every_detail_in_revision_and_recipient_order_without_tracking()
    {
        var officeId = Guid.NewGuid();
        var month = new ServiceMonth(2026, 6);
        await using var context = _fixture.NewContext();
        var first = Batch.New(officeId, month, revision: 1);
        var second = Batch.Correct(officeId, month, first, revision: 2);
        var lateRecipient = Guid.Parse("f0000000-0000-0000-0000-000000000000");
        var earlyRecipient = Guid.Parse("10000000-0000-0000-0000-000000000000");
        context.AddRange(second, first);
        context.AddRange(
            Batch.Detail(second.Id, lateRecipient),
            Batch.Detail(second.Id, earlyRecipient),
            Batch.Detail(first.Id, lateRecipient));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await new ClaimBatchRepository(context)
            .ListHistoryAggregatesAsync(officeId, month, default);

        result.Select(item => item.Header.Revision).Should().Equal(1, 2);
        result[1].Details.Select(item => item.RecipientId).Should().Equal(earlyRecipient, lateRecipient);
        context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task FindByOperationId_returns_header_and_all_details_without_tracking()
    {
        await using var context = _fixture.NewContext();
        var batch = Batch.New(Guid.NewGuid(), new ServiceMonth(2026, 7), revision: 1);
        context.Add(batch);
        context.AddRange(Batch.Detail(batch.Id, Guid.NewGuid()), Batch.Detail(batch.Id, Guid.NewGuid()));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await new ClaimBatchRepository(context)
            .FindByOperationIdAsync(batch.FinalizationOperationId, default);

        result.Should().NotBeNull();
        result!.Details.Should().HaveCount(2);
        context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task ListHistoryAggregates_orders_only_by_revision_when_created_at_and_id_are_reversed()
    {
        var officeId = Guid.NewGuid();
        var month = new ServiceMonth(2026, 8);
        await using var context = _fixture.NewContext();
        var first = Batch.New(officeId, month, revision: 1) with
        {
            Id = Guid.Parse("f0000000-0000-0000-0000-000000000000"),
            CreatedAt = DateTimeOffset.UnixEpoch.AddDays(2),
        };
        var second = Batch.Correct(officeId, month, first, revision: 2) with
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000000"),
            CreatedAt = DateTimeOffset.UnixEpoch.AddDays(1),
        };
        context.AddRange(second, first);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await new ClaimBatchRepository(context)
            .ListHistoryAggregatesAsync(officeId, month, default);

        result.Select(item => item.Header.Revision).Should().Equal(1, 2);
        result.Select(item => item.Header.Id).Should().Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task ListHistoryAggregates_returns_duplicate_revisions_raw_without_id_or_time_rescue()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TsumugiDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new TsumugiDbContext(options);
        await context.Database.MigrateAsync();
        await context.Database.ExecuteSqlRawAsync(
            "DROP INDEX UX_ClaimBatches_OfficeId_ServiceMonthKey_Revision; "
            + "DROP INDEX UX_ClaimBatches_OfficeId_ServiceMonthKey_NewOnly;");
        var officeId = Guid.NewGuid();
        var month = new ServiceMonth(2026, 9);
        var laterId = Batch.New(officeId, month, revision: 1) with
        {
            Id = Guid.Parse("f0000000-0000-0000-0000-000000000000"),
            CreatedAt = DateTimeOffset.UnixEpoch.AddDays(2),
        };
        var earlierId = Batch.New(officeId, month, revision: 1) with
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000000"),
            CreatedAt = DateTimeOffset.UnixEpoch.AddDays(1),
        };
        context.AddRange(laterId, earlierId);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await new ClaimBatchRepository(context)
            .ListHistoryAggregatesAsync(officeId, month, default);

        result.Should().HaveCount(2);
        result.Select(item => item.Header.Revision).Should().Equal(1, 1);
        result.Select(item => item.Header.Id).Should().Contain([laterId.Id, earlierId.Id]);
    }

    internal static class Batch
    {
        internal static ClaimBatch New(Guid officeId, ServiceMonth month, int revision)
            => ClaimBatch.NewRecord(
                Guid.NewGuid(), officeId, month, 0, 0, 0, 0,
                "master-v1", "csv-v1", "report-v1", "snapshot-app-v1", "operation-app-v1",
                Guid.NewGuid(), ClaimBatch.CurrentOperationPayloadSchemaVersion, new string('a', 64),
                "actor", DateTimeOffset.UnixEpoch.AddMinutes(revision));

        internal static ClaimBatch Correct(Guid officeId, ServiceMonth month, ClaimBatch root, int revision)
            => ClaimBatch.Correction(
                Guid.NewGuid(), officeId, month, revision, root.Id, root.Id, revision - 1,
                0, 0, 0, 0, "master-v1", "csv-v1", "report-v1", "snapshot-app-v1", "operation-app-v1",
                Guid.NewGuid(), ClaimBatch.CurrentOperationPayloadSchemaVersion, new string('b', 64),
                "actor", DateTimeOffset.UnixEpoch.AddMinutes(revision));

        internal static ClaimDetail Detail(Guid batchId, Guid recipientId)
            => ClaimDetail.Create(
                Guid.NewGuid(), batchId, recipientId, "claim-snapshot-v1",
                "master-v1", "csv-v1", "report-v1", "snapshot-app-v1",
                "{\"schemaVersion\":\"claim-snapshot-v1\",\"validationCodecId\":\"test-codec-v1\",\"payloadSha256\":\"" + new string('a', 64) + "\",\"payload\":{}}",
                "{\"schemaVersion\":\"claim-snapshot-v1\",\"validationCodecId\":\"test-codec-v1\",\"payloadSha256\":\"" + new string('a', 64) + "\",\"payload\":{}}",
                0, 0, 0, 0, "actor", DateTimeOffset.UnixEpoch);
    }
}
