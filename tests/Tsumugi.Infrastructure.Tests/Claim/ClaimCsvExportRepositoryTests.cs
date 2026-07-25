using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Persistence;

namespace Tsumugi.Infrastructure.Tests.Claim;

public sealed class ClaimCsvExportRepositoryTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;

    public ClaimCsvExportRepositoryTests(SqliteFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AppendAsync_persists_export_and_lists_it_by_batch_without_tracking()
    {
        await using var context = _fixture.NewContext();
        var batch = ClaimCsvExportFakes.Batch(new ServiceMonth(2026, 7));
        context.Add(batch);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new ClaimCsvExportRepository(context);
        var export = ClaimCsvExportFakes.Export(batch.Id, sha256Seed: 'a');

        await repository.AppendAsync(export, default);
        context.ChangeTracker.Clear();

        var listed = await repository.ListByBatchAsync(batch.Id, default);
        listed.Should().ContainSingle().Which.Sha256.Should().Be(export.Sha256);
        context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task AppendAsync_allows_multiple_exports_for_same_batch_in_created_order()
    {
        await using var context = _fixture.NewContext();
        var batch = ClaimCsvExportFakes.Batch(new ServiceMonth(2026, 8));
        context.Add(batch);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new ClaimCsvExportRepository(context);
        foreach (var (seed, minutes) in new[] { ('c', 3), ('a', 1), ('b', 2) })
        {
            await repository.AppendAsync(
                ClaimCsvExportFakes.Export(batch.Id, seed, minutes),
                default);
        }

        var listed = await repository.ListByBatchAsync(batch.Id, default);
        listed.Select(item => item.Sha256[0]).Should().Equal('a', 'b', 'c');
    }

    [Fact]
    public async Task ListByBatchAsync_returns_empty_for_unknown_batch()
    {
        await using var context = _fixture.NewContext();

        var listed = await new ClaimCsvExportRepository(context)
            .ListByBatchAsync(Guid.NewGuid(), default);

        listed.Should().BeEmpty();
    }

    // NOTE(teeth): ClaimCsvExport は追記専用。AppendOnlyGuard の網から外れたらここが RED になる。
    [Fact]
    public void Append_only_types_include_claim_csv_export()
    {
        AppendOnlyGuard.GetAppendOnlyTypesForTests().Should().Contain(typeof(ClaimCsvExport));
    }

    [Fact]
    public async Task Modifying_ClaimCsvExport_throws()
    {
        await using var context = _fixture.NewContext();
        var batch = ClaimCsvExportFakes.Batch(new ServiceMonth(2026, 9));
        context.Add(batch);
        var export = ClaimCsvExportFakes.Export(batch.Id, sha256Seed: 'd');
        context.Add(export);
        await context.SaveChangesAsync();

        var loaded = await context.Set<ClaimCsvExport>().SingleAsync(item => item.Id == export.Id);
        context.Entry(loaded).Property(nameof(ClaimCsvExport.Sha256)).CurrentValue = new string('e', 64);
        context.Entry(loaded).Property(nameof(ClaimCsvExport.Sha256)).IsModified = true;

        Func<Task> act = () => context.SaveChangesAsync();
        await act.Should().ThrowAsync<AppendOnlyViolationException>()
            .Where(exception => exception.EntityName == nameof(ClaimCsvExport));
    }

    [Fact]
    public async Task Export_requires_an_existing_claim_batch()
    {
        await using var context = _fixture.NewContext();
        context.Add(ClaimCsvExportFakes.Export(Guid.NewGuid(), sha256Seed: 'f'));

        Func<Task> act = () => context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }
}

internal static class ClaimCsvExportFakes
{
    internal static ClaimBatch Batch(ServiceMonth month) => ClaimBatch.NewRecord(
        Guid.NewGuid(), Guid.NewGuid(), month, 0, 0, 0, 0,
        "master-v1", "csv-v1", "report-v1", "snapshot-app-v1", "operation-app-v1",
        Guid.NewGuid(), ClaimBatch.CurrentOperationPayloadSchemaVersion, new string('a', 64),
        "actor", DateTimeOffset.UnixEpoch);

    internal static ClaimCsvExport Export(Guid batchId, char sha256Seed, int createdAtMinutes = 0) =>
        ClaimCsvExport.NewRecord(
            Guid.NewGuid(),
            batchId,
            new ProcessingMonth(2026, 8),
            csvSpecificationVersion: "r7-10",
            claimMasterVersion: "master-v1",
            sha256: new string(sha256Seed, 64),
            byteLength: 1234,
            createdBy: "actor",
            createdAt: DateTimeOffset.UnixEpoch.AddMinutes(createdAtMinutes));
}
