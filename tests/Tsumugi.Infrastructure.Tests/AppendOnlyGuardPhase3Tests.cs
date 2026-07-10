using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Persistence;

namespace Tsumugi.Infrastructure.Tests;

public sealed class AppendOnlyGuardPhase3Tests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;

    public AppendOnlyGuardPhase3Tests(SqliteFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData(typeof(ClaimBatch))]
    [InlineData(typeof(ClaimDetail))]
    public void Append_only_types_include_phase3_claim_entities(Type entityType)
    {
        AppendOnlyGuard.GetAppendOnlyTypesForTests().Should().Contain(entityType);
    }

    [Fact]
    public async Task Modifying_claim_batch_through_db_context_save_changes_throws()
    {
        await using var context = _fixture.NewContext();
        var batch = NewBatch();
        context.Set<ClaimBatch>().Add(batch);
        await context.SaveChangesAsync();

        context.Entry(batch).Property(nameof(ClaimBatch.TotalUnits)).CurrentValue = 11;
        context.Entry(batch).Property(nameof(ClaimBatch.TotalUnits)).IsModified = true;

        Func<Task> act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<AppendOnlyViolationException>()
            .Where(exception => exception.EntityName == nameof(ClaimBatch));
    }

    [Fact]
    public async Task Deleting_claim_batch_through_db_context_save_changes_throws()
    {
        await using var context = _fixture.NewContext();
        var batch = NewBatch();
        context.Set<ClaimBatch>().Add(batch);
        await context.SaveChangesAsync();

        context.Set<ClaimBatch>().Remove(batch);

        Func<Task> act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<AppendOnlyViolationException>()
            .Where(exception => exception.EntityName == nameof(ClaimBatch));
    }

    [Fact]
    public async Task Modifying_claim_detail_through_db_context_save_changes_throws()
    {
        await using var context = _fixture.NewContext();
        var batch = NewBatch();
        var detail = NewDetail(batch.Id);
        context.Set<ClaimBatch>().Add(batch);
        context.Set<ClaimDetail>().Add(detail);
        await context.SaveChangesAsync();

        context.Entry(detail).Property(nameof(ClaimDetail.TotalUnits)).CurrentValue = 11;
        context.Entry(detail).Property(nameof(ClaimDetail.TotalUnits)).IsModified = true;

        Func<Task> act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<AppendOnlyViolationException>()
            .Where(exception => exception.EntityName == nameof(ClaimDetail));
    }

    [Fact]
    public async Task Deleting_claim_detail_through_db_context_save_changes_throws()
    {
        await using var context = _fixture.NewContext();
        var batch = NewBatch();
        var detail = NewDetail(batch.Id);
        context.Set<ClaimBatch>().Add(batch);
        context.Set<ClaimDetail>().Add(detail);
        await context.SaveChangesAsync();

        context.Set<ClaimDetail>().Remove(detail);

        Func<Task> act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<AppendOnlyViolationException>()
            .Where(exception => exception.EntityName == nameof(ClaimDetail));
    }

    private static ClaimBatch NewBatch() => ClaimBatch.NewRecord(
        Guid.NewGuid(),
        Guid.NewGuid(),
        new ServiceMonth(2026, 7),
        totalUnits: 10,
        totalCostYen: 1_000,
        totalBenefitYen: 900,
        totalBurdenYen: 100,
        claimMasterVersion: "claim-master-v1",
        csvSpecificationVersion: "csv-v1",
        reportSpecificationVersion: "report-v1",
        snapshotApplicationVersion: "snapshot-app-v1",
        operationApplicationVersion: "operation-app-v1",
        finalizationOperationId: Guid.NewGuid(),
        operationPayloadSchemaVersion: ClaimBatch.CurrentOperationPayloadSchemaVersion,
        operationPayloadSha256: new string('a', 64),
        createdBy: "tester",
        createdAt: DateTimeOffset.UtcNow);

    private static ClaimDetail NewDetail(Guid claimBatchId) => ClaimDetail.Create(
        Guid.NewGuid(),
        claimBatchId,
        Guid.NewGuid(),
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
