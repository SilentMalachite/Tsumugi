using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests.Entities;

public sealed class ClaimCsvExportTests
{
    private static ClaimCsvExport Create(
        string sha256,
        int byteLength = 1024,
        string csvSpecificationVersion = "r7-10",
        string claimMasterVersion = "master-v1",
        string createdBy = "actor",
        Guid? id = null,
        Guid? claimBatchId = null) => ClaimCsvExport.NewRecord(
        id ?? Guid.NewGuid(),
        claimBatchId ?? Guid.NewGuid(),
        new ProcessingMonth(2026, 8),
        csvSpecificationVersion,
        claimMasterVersion,
        sha256,
        byteLength,
        createdBy,
        DateTimeOffset.UnixEpoch);

    [Fact]
    public void NewRecord_keeps_every_provenance_value_and_zeroes_the_concurrency_token()
    {
        var id = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        var export = Create(new string('a', 64), id: id, claimBatchId: batchId);

        export.Id.Should().Be(id);
        export.ClaimBatchId.Should().Be(batchId);
        export.ProcessingMonth.Should().Be(new ProcessingMonth(2026, 8));
        export.CsvSpecificationVersion.Should().Be("r7-10");
        export.ClaimMasterVersion.Should().Be("master-v1");
        export.Sha256.Should().Be(new string('a', 64));
        export.ByteLength.Should().Be(1024);
        export.CreatedBy.Should().Be("actor");
        export.CreatedAt.Should().Be(DateTimeOffset.UnixEpoch);
        export.ConcurrencyToken.Should().Be(Guid.Empty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789")]
    [InlineData("g000000000000000000000000000000000000000000000000000000000000000")]
    public void NewRecord_rejects_a_sha256_that_is_not_64_lowercase_hex_characters(string sha256)
    {
        var act = () => Create(sha256);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NewRecord_rejects_a_non_positive_byte_length(int byteLength)
    {
        var act = () => Create(new string('a', 64), byteLength);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NewRecord_rejects_empty_identities()
    {
        var withoutId = () => Create(new string('a', 64), id: Guid.Empty);
        var withoutBatch = () => Create(new string('a', 64), claimBatchId: Guid.Empty);

        withoutId.Should().Throw<ArgumentException>();
        withoutBatch.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("", "master-v1", "actor")]
    [InlineData("r7-10", "", "actor")]
    [InlineData("r7-10", "master-v1", " ")]
    public void NewRecord_rejects_blank_provenance_strings(
        string csvSpecificationVersion, string claimMasterVersion, string createdBy)
    {
        var act = () => Create(
            new string('a', 64),
            csvSpecificationVersion: csvSpecificationVersion,
            claimMasterVersion: claimMasterVersion,
            createdBy: createdBy);

        act.Should().Throw<ArgumentException>();
    }
}
