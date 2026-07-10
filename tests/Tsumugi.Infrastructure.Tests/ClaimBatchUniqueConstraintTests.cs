using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Persistence;

namespace Tsumugi.Infrastructure.Tests;

public sealed class ClaimBatchUniqueConstraintTests : IClassFixture<SqliteFixture>
{
    private static int _serviceMonthSequence;
    private readonly SqliteFixture _fixture;

    public ClaimBatchUniqueConstraintTests(SqliteFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Duplicate_new_batch_for_same_office_and_service_month_is_rejected()
    {
        await using var context = await NewForeignKeyEnabledContextAsync();
        var officeId = Guid.NewGuid();
        var serviceMonth = NewServiceMonth();
        context.Set<ClaimBatch>().Add(NewBatch(officeId, serviceMonth));
        await context.SaveChangesAsync();

        var secondNew = NewBatch(officeId, serviceMonth) with { Revision = 2 };
        context.Set<ClaimBatch>().Add(secondNew);

        await AssertSqliteConstraintAsync(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Correct_and_cancel_batches_for_same_office_and_service_month_are_allowed()
    {
        await using var context = await NewForeignKeyEnabledContextAsync();
        var officeId = Guid.NewGuid();
        var serviceMonth = NewServiceMonth();
        var root = NewBatch(officeId, serviceMonth);
        context.Set<ClaimBatch>().Add(root);
        await context.SaveChangesAsync();

        var correction = CorrectedBatch(officeId, serviceMonth, revision: 2, root.Id, root.Id);
        context.Set<ClaimBatch>().Add(correction);
        await context.SaveChangesAsync();

        var cancellation = CancelledBatch(officeId, serviceMonth, revision: 3, root.Id, correction.Id);
        context.Set<ClaimBatch>().Add(cancellation);

        Func<Task> act = () => context.SaveChangesAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Duplicate_finalization_operation_id_is_rejected()
    {
        await using var context = await NewForeignKeyEnabledContextAsync();
        var operationId = Guid.NewGuid();
        context.Set<ClaimBatch>().Add(NewBatch(Guid.NewGuid(), NewServiceMonth(), operationId: operationId));
        await context.SaveChangesAsync();

        context.Set<ClaimBatch>().Add(NewBatch(Guid.NewGuid(), NewServiceMonth(), operationId: operationId));

        await AssertSqliteConstraintAsync(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Duplicate_office_service_month_and_revision_is_rejected()
    {
        await using var context = await NewForeignKeyEnabledContextAsync();
        var officeId = Guid.NewGuid();
        var serviceMonth = NewServiceMonth();
        var root = NewBatch(officeId, serviceMonth);
        context.Set<ClaimBatch>().Add(root);
        await context.SaveChangesAsync();

        context.Set<ClaimBatch>().Add(CorrectedBatch(officeId, serviceMonth, revision: 2, root.Id, root.Id));
        await context.SaveChangesAsync();

        context.Set<ClaimBatch>().Add(CorrectedBatch(officeId, serviceMonth, revision: 2, root.Id, root.Id));

        await AssertSqliteConstraintAsync(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Duplicate_batch_and_recipient_detail_is_rejected()
    {
        await using var context = await NewForeignKeyEnabledContextAsync();
        var batch = NewBatch(Guid.NewGuid(), NewServiceMonth());
        var recipientId = Guid.NewGuid();
        context.Set<ClaimBatch>().Add(batch);
        context.Set<ClaimDetail>().Add(NewDetail(batch.Id, recipientId));
        await context.SaveChangesAsync();

        context.Set<ClaimDetail>().Add(NewDetail(batch.Id, recipientId));

        await AssertSqliteConstraintAsync(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Missing_origin_batch_is_rejected_by_foreign_key()
    {
        await using var context = await NewForeignKeyEnabledContextAsync();
        var officeId = Guid.NewGuid();
        var serviceMonth = NewServiceMonth();
        var head = NewBatch(officeId, serviceMonth);
        context.Set<ClaimBatch>().Add(head);
        await context.SaveChangesAsync();

        context.Set<ClaimBatch>().Add(
            CorrectedBatch(officeId, serviceMonth, revision: 2, Guid.NewGuid(), head.Id));

        await AssertSqliteConstraintAsync(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Missing_expected_head_batch_is_rejected_by_foreign_key()
    {
        await using var context = await NewForeignKeyEnabledContextAsync();
        var officeId = Guid.NewGuid();
        var serviceMonth = NewServiceMonth();
        var root = NewBatch(officeId, serviceMonth);
        context.Set<ClaimBatch>().Add(root);
        await context.SaveChangesAsync();

        context.Set<ClaimBatch>().Add(
            CorrectedBatch(officeId, serviceMonth, revision: 2, root.Id, Guid.NewGuid()));

        await AssertSqliteConstraintAsync(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Missing_claim_batch_for_detail_is_rejected_by_foreign_key()
    {
        await using var context = await NewForeignKeyEnabledContextAsync();
        context.Set<ClaimDetail>().Add(NewDetail(Guid.NewGuid(), Guid.NewGuid()));

        await AssertSqliteConstraintAsync(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Deleting_origin_batch_referenced_by_correction_is_rejected_by_foreign_key()
    {
        await using var context = await NewForeignKeyEnabledContextAsync();
        var origin = NewBatch(Guid.NewGuid(), NewServiceMonth());
        var expectedHead = NewBatch(Guid.NewGuid(), NewServiceMonth());
        var correction = CorrectedBatch(
            origin.OfficeId, origin.ServiceMonth, revision: 2, origin.Id, expectedHead.Id);
        context.Set<ClaimBatch>().AddRange(origin, expectedHead, correction);
        await context.SaveChangesAsync();

        await AssertRawSqlConstraintAsync(
            () => context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM ClaimBatches WHERE Id = {origin.Id}"));
    }

    [Fact]
    public async Task Deleting_expected_head_batch_referenced_by_correction_is_rejected_by_foreign_key()
    {
        await using var context = await NewForeignKeyEnabledContextAsync();
        var origin = NewBatch(Guid.NewGuid(), NewServiceMonth());
        var expectedHead = NewBatch(Guid.NewGuid(), NewServiceMonth());
        var correction = CorrectedBatch(
            origin.OfficeId, origin.ServiceMonth, revision: 2, origin.Id, expectedHead.Id);
        context.Set<ClaimBatch>().AddRange(origin, expectedHead, correction);
        await context.SaveChangesAsync();

        await AssertRawSqlConstraintAsync(
            () => context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM ClaimBatches WHERE Id = {expectedHead.Id}"));
    }

    [Fact]
    public async Task Deleting_batch_with_details_is_rejected_by_foreign_key()
    {
        await using var context = await NewForeignKeyEnabledContextAsync();
        var batch = NewBatch(Guid.NewGuid(), NewServiceMonth());
        context.Set<ClaimBatch>().Add(batch);
        context.Set<ClaimDetail>().Add(NewDetail(batch.Id, Guid.NewGuid()));
        await context.SaveChangesAsync();

        await AssertRawSqlConstraintAsync(
            () => context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM ClaimBatches WHERE Id = {batch.Id}"));
    }

    private async Task<TsumugiDbContext> NewForeignKeyEnabledContextAsync()
    {
        var context = _fixture.NewContext();
        await context.Database.OpenConnectionAsync();

        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        await command.ExecuteNonQueryAsync();
        command.CommandText = "PRAGMA foreign_keys;";
        var enabled = Convert.ToInt64(await command.ExecuteScalarAsync());
        enabled.Should().Be(1);

        return context;
    }

    private static async Task AssertSqliteConstraintAsync(Func<Task> action)
    {
        var exception = await action.Should().ThrowAsync<DbUpdateException>();
        exception.WithInnerException<SqliteException>()
            .Which.SqliteErrorCode.Should().Be(19);
    }

    private static async Task AssertRawSqlConstraintAsync(Func<Task<int>> action)
    {
        var exception = await action.Should().ThrowAsync<SqliteException>();
        exception.Which.SqliteErrorCode.Should().Be(19);
    }

    private static ServiceMonth NewServiceMonth()
    {
        var sequence = Interlocked.Increment(ref _serviceMonthSequence) - 1;
        return new ServiceMonth(2026 + (sequence / 12), (sequence % 12) + 1);
    }

    private static ClaimBatch NewBatch(
        Guid officeId,
        ServiceMonth serviceMonth,
        Guid? operationId = null) => ClaimBatch.NewRecord(
            Guid.NewGuid(),
            officeId,
            serviceMonth,
            totalUnits: 10,
            totalCostYen: 1_000,
            totalBenefitYen: 900,
            totalBurdenYen: 100,
            claimMasterVersion: "claim-master-v1",
            csvSpecificationVersion: "csv-v1",
            reportSpecificationVersion: "report-v1",
            snapshotApplicationVersion: "snapshot-app-v1",
            operationApplicationVersion: "operation-app-v1",
            finalizationOperationId: operationId ?? Guid.NewGuid(),
            operationPayloadSchemaVersion: ClaimBatch.CurrentOperationPayloadSchemaVersion,
            operationPayloadSha256: new string('a', 64),
            createdBy: "tester",
            createdAt: DateTimeOffset.UtcNow);

    private static ClaimBatch CorrectedBatch(
        Guid officeId,
        ServiceMonth serviceMonth,
        int revision,
        Guid originId,
        Guid expectedHeadBatchId) => ClaimBatch.Correction(
            Guid.NewGuid(),
            officeId,
            serviceMonth,
            revision,
            originId,
            expectedHeadBatchId,
            expectedHeadRevision: revision - 1,
            totalUnits: 11,
            totalCostYen: 1_100,
            totalBenefitYen: 990,
            totalBurdenYen: 110,
            claimMasterVersion: "claim-master-v1",
            csvSpecificationVersion: "csv-v1",
            reportSpecificationVersion: "report-v1",
            snapshotApplicationVersion: "snapshot-app-v1",
            operationApplicationVersion: "operation-app-v1",
            finalizationOperationId: Guid.NewGuid(),
            operationPayloadSchemaVersion: ClaimBatch.CurrentOperationPayloadSchemaVersion,
            operationPayloadSha256: new string('b', 64),
            createdBy: "tester",
            createdAt: DateTimeOffset.UtcNow);

    private static ClaimBatch CancelledBatch(
        Guid officeId,
        ServiceMonth serviceMonth,
        int revision,
        Guid originId,
        Guid expectedHeadBatchId) => ClaimBatch.Cancellation(
            Guid.NewGuid(),
            officeId,
            serviceMonth,
            revision,
            originId,
            expectedHeadBatchId,
            expectedHeadRevision: revision - 1,
            claimMasterVersion: "claim-master-v1",
            csvSpecificationVersion: "csv-v1",
            reportSpecificationVersion: "report-v1",
            snapshotApplicationVersion: "snapshot-app-v1",
            operationApplicationVersion: "operation-app-v1",
            finalizationOperationId: Guid.NewGuid(),
            operationPayloadSchemaVersion: ClaimBatch.CurrentOperationPayloadSchemaVersion,
            operationPayloadSha256: new string('c', 64),
            createdBy: "tester",
            createdAt: DateTimeOffset.UtcNow);

    private static ClaimDetail NewDetail(Guid claimBatchId, Guid recipientId) => ClaimDetail.Create(
        Guid.NewGuid(),
        claimBatchId,
        recipientId,
        snapshotSchemaVersion: "snapshot-v1",
        claimMasterVersion: "claim-master-v1",
        csvSpecificationVersion: "csv-v1",
        reportSpecificationVersion: "report-v1",
        snapshotApplicationVersion: "snapshot-app-v1",
        inputSnapshotJson: "{}",
        calculationSnapshotJson: "{}",
        totalUnits: 10,
        totalCostYen: 1_000,
        benefitYen: 900,
        burdenYen: 100,
        createdBy: "tester",
        createdAt: DateTimeOffset.UtcNow);
}
