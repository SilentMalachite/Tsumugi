using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Tests.Entities;

public sealed class ClaimBatchTests
{
    private const string OperationSchema = "claim-finalization-operation-v1";
    private static readonly Guid BatchId = Guid.Parse("00000000-0000-0000-0000-000000000101");
    private static readonly Guid OfficeId = Guid.Parse("00000000-0000-0000-0000-000000000102");
    private static readonly Guid RootId = Guid.Parse("00000000-0000-0000-0000-000000000103");
    private static readonly Guid HeadId = Guid.Parse("00000000-0000-0000-0000-000000000104");
    private static readonly Guid OperationId = Guid.Parse("00000000-0000-0000-0000-000000000105");
    private static readonly ServiceMonth Month = new(2026, 6);
    private static readonly DateTimeOffset CreatedAt = new(2026, 7, 10, 1, 2, 3, TimeSpan.Zero);
    private static readonly string Hash = new('a', 64);

    [Fact]
    public void NewRecord_creates_revision_one_without_lineage_or_expected_head()
    {
        var batch = NewRecord(Valid);

        batch.Should().BeEquivalentTo(new
        {
            Id = BatchId,
            OfficeId,
            ServiceMonth = Month,
            Revision = 1,
            Kind = RecordKind.New,
            OriginId = (Guid?)null,
            ExpectedHeadBatchId = (Guid?)null,
            ExpectedHeadRevision = (int?)null,
            TotalUnits = 100,
            TotalCostYen = 10_000,
            TotalBenefitYen = 9_000,
            TotalBurdenYen = 1_000,
            ClaimMasterVersion = "claim-master-r8-06",
            CsvSpecificationVersion = "csv-r7-10",
            ReportSpecificationVersion = "report-r8-06",
            SnapshotApplicationVersion = "snapshot-app-v1",
            OperationApplicationVersion = "operation-app-v1",
            FinalizationOperationId = OperationId,
            OperationPayloadSchemaVersion = OperationSchema,
            OperationPayloadSha256 = Hash,
            CreatedBy = "tester",
            CreatedAt,
            ConcurrencyToken = Guid.Empty,
        });
    }

    [Fact]
    public void Correction_creates_root_origin_revision_with_expected_previous_head()
    {
        var batch = Correction(Valid);

        batch.Revision.Should().Be(2);
        batch.Kind.Should().Be(RecordKind.Correct);
        batch.OriginId.Should().Be(RootId);
        batch.ExpectedHeadBatchId.Should().Be(HeadId);
        batch.ExpectedHeadRevision.Should().Be(1);
        batch.TotalUnits.Should().Be(100);
        batch.ConcurrencyToken.Should().BeEmpty();
    }

    [Fact]
    public void Cancellation_creates_zero_totals()
    {
        var batch = Cancellation(Valid);

        batch.Revision.Should().Be(2);
        batch.Kind.Should().Be(RecordKind.Cancel);
        batch.OriginId.Should().Be(RootId);
        batch.ExpectedHeadBatchId.Should().Be(HeadId);
        batch.ExpectedHeadRevision.Should().Be(1);
        batch.TotalUnits.Should().Be(0);
        batch.TotalCostYen.Should().Be(0);
        batch.TotalBenefitYen.Should().Be(0);
        batch.TotalBurdenYen.Should().Be(0);
        batch.ConcurrencyToken.Should().BeEmpty();
    }

    [Fact]
    public void All_factories_reject_empty_entity_identity()
    {
        foreach (var factory in Factories(Valid with { Id = Guid.Empty }))
            factory.Should().Throw<ArgumentException>().WithParameterName("id");
    }

    [Fact]
    public void All_factories_reject_empty_office_identity()
    {
        foreach (var factory in Factories(Valid with { OfficeId = Guid.Empty }))
            factory.Should().Throw<ArgumentException>().WithParameterName("officeId");
    }

    [Fact]
    public void All_factories_reject_uninitialized_service_month()
    {
        foreach (var factory in Factories(Valid with { ServiceMonth = default }))
            factory.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void All_factories_reject_empty_finalization_operation_identity()
    {
        foreach (var factory in Factories(Valid with { FinalizationOperationId = Guid.Empty }))
            factory.Should().Throw<ArgumentException>().WithParameterName("finalizationOperationId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void All_factories_reject_blank_created_by(string? createdBy)
    {
        foreach (var factory in Factories(Valid with { CreatedBy = createdBy! }))
            factory.Should().Throw<ArgumentException>().WithParameterName(nameof(createdBy));
    }

    [Theory]
    [InlineData("claimMasterVersion")]
    [InlineData("csvSpecificationVersion")]
    [InlineData("reportSpecificationVersion")]
    [InlineData("snapshotApplicationVersion")]
    [InlineData("operationApplicationVersion")]
    public void All_factories_reject_blank_versions(string field)
    {
        var input = field switch
        {
            "claimMasterVersion" => Valid with { ClaimMasterVersion = " " },
            "csvSpecificationVersion" => Valid with { CsvSpecificationVersion = " " },
            "reportSpecificationVersion" => Valid with { ReportSpecificationVersion = " " },
            "snapshotApplicationVersion" => Valid with { SnapshotApplicationVersion = " " },
            "operationApplicationVersion" => Valid with { OperationApplicationVersion = " " },
            _ => throw new InvalidOperationException(),
        };

        foreach (var factory in Factories(input))
            factory.Should().Throw<ArgumentException>().WithParameterName(field);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("claim-finalization-operation-v2")]
    [InlineData(" claim-finalization-operation-v1")]
    public void All_factories_require_exact_operation_payload_schema(string? schema)
    {
        foreach (var factory in Factories(Valid with { OperationPayloadSchemaVersion = schema! }))
            factory.Should().Throw<ArgumentException>().WithParameterName("operationPayloadSchemaVersion");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaA")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public void All_factories_require_64_character_lowercase_hex_operation_hash(string? hash)
    {
        foreach (var factory in Factories(Valid with { OperationPayloadSha256 = hash! }))
            factory.Should().Throw<ArgumentException>().WithParameterName("operationPayloadSha256");
    }

    [Theory]
    [InlineData("totalUnits")]
    [InlineData("totalCostYen")]
    [InlineData("totalBenefitYen")]
    [InlineData("totalBurdenYen")]
    public void NewRecord_and_Correction_reject_negative_totals(string field)
    {
        var input = field switch
        {
            "totalUnits" => Valid with { TotalUnits = -1 },
            "totalCostYen" => Valid with { TotalCostYen = -1 },
            "totalBenefitYen" => Valid with { TotalBenefitYen = -1 },
            "totalBurdenYen" => Valid with { TotalBurdenYen = -1 },
            _ => throw new InvalidOperationException(),
        };

        Action[] factories = [() => NewRecord(input), () => Correction(input)];
        foreach (var factory in factories)
            factory.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(field);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    public void Correction_and_Cancellation_require_revision_two_or_later(int revision)
    {
        Action[] factories =
        [
            () => Correction(Valid with { Revision = revision }),
            () => Cancellation(Valid with { Revision = revision }),
        ];

        foreach (var factory in factories)
            factory.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(revision));
    }

    [Fact]
    public void Correction_and_Cancellation_require_nonempty_root_origin()
    {
        Action[] factories =
        [
            () => Correction(Valid with { OriginId = Guid.Empty }),
            () => Cancellation(Valid with { OriginId = Guid.Empty }),
        ];

        foreach (var factory in factories)
            factory.Should().Throw<ArgumentException>().WithParameterName("originId");
    }

    [Fact]
    public void Correction_and_Cancellation_require_nonempty_expected_head()
    {
        Action[] factories =
        [
            () => Correction(Valid with { ExpectedHeadBatchId = Guid.Empty }),
            () => Cancellation(Valid with { ExpectedHeadBatchId = Guid.Empty }),
        ];

        foreach (var factory in factories)
            factory.Should().Throw<ArgumentException>().WithParameterName("expectedHeadBatchId");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Correction_and_Cancellation_require_expected_head_revision_immediately_before_revision(
        int expectedHeadRevision)
    {
        Action[] factories =
        [
            () => Correction(Valid with { ExpectedHeadRevision = expectedHeadRevision }),
            () => Cancellation(Valid with { ExpectedHeadRevision = expectedHeadRevision }),
        ];

        foreach (var factory in factories)
            factory.Should().Throw<ArgumentException>().WithParameterName(nameof(expectedHeadRevision));
    }

    private static readonly BatchInput Valid = new(
        BatchId,
        OfficeId,
        Month,
        Revision: 2,
        RootId,
        HeadId,
        ExpectedHeadRevision: 1,
        TotalUnits: 100,
        TotalCostYen: 10_000,
        TotalBenefitYen: 9_000,
        TotalBurdenYen: 1_000,
        ClaimMasterVersion: "claim-master-r8-06",
        CsvSpecificationVersion: "csv-r7-10",
        ReportSpecificationVersion: "report-r8-06",
        SnapshotApplicationVersion: "snapshot-app-v1",
        OperationApplicationVersion: "operation-app-v1",
        OperationId,
        OperationSchema,
        Hash,
        CreatedBy: "tester",
        CreatedAt);

    private static ClaimBatch NewRecord(BatchInput input) => ClaimBatch.NewRecord(
        input.Id,
        input.OfficeId,
        input.ServiceMonth,
        input.TotalUnits,
        input.TotalCostYen,
        input.TotalBenefitYen,
        input.TotalBurdenYen,
        input.ClaimMasterVersion,
        input.CsvSpecificationVersion,
        input.ReportSpecificationVersion,
        input.SnapshotApplicationVersion,
        input.OperationApplicationVersion,
        input.FinalizationOperationId,
        input.OperationPayloadSchemaVersion,
        input.OperationPayloadSha256,
        input.CreatedBy,
        input.CreatedAt);

    private static ClaimBatch Correction(BatchInput input) => ClaimBatch.Correction(
        input.Id,
        input.OfficeId,
        input.ServiceMonth,
        input.Revision,
        input.OriginId,
        input.ExpectedHeadBatchId,
        input.ExpectedHeadRevision,
        input.TotalUnits,
        input.TotalCostYen,
        input.TotalBenefitYen,
        input.TotalBurdenYen,
        input.ClaimMasterVersion,
        input.CsvSpecificationVersion,
        input.ReportSpecificationVersion,
        input.SnapshotApplicationVersion,
        input.OperationApplicationVersion,
        input.FinalizationOperationId,
        input.OperationPayloadSchemaVersion,
        input.OperationPayloadSha256,
        input.CreatedBy,
        input.CreatedAt);

    private static ClaimBatch Cancellation(BatchInput input) => ClaimBatch.Cancellation(
        input.Id,
        input.OfficeId,
        input.ServiceMonth,
        input.Revision,
        input.OriginId,
        input.ExpectedHeadBatchId,
        input.ExpectedHeadRevision,
        input.ClaimMasterVersion,
        input.CsvSpecificationVersion,
        input.ReportSpecificationVersion,
        input.SnapshotApplicationVersion,
        input.OperationApplicationVersion,
        input.FinalizationOperationId,
        input.OperationPayloadSchemaVersion,
        input.OperationPayloadSha256,
        input.CreatedBy,
        input.CreatedAt);

    private static Action[] Factories(BatchInput input) =>
    [
        () => NewRecord(input),
        () => Correction(input),
        () => Cancellation(input),
    ];

    private sealed record BatchInput(
        Guid Id,
        Guid OfficeId,
        ServiceMonth ServiceMonth,
        int Revision,
        Guid OriginId,
        Guid ExpectedHeadBatchId,
        int ExpectedHeadRevision,
        int TotalUnits,
        int TotalCostYen,
        int TotalBenefitYen,
        int TotalBurdenYen,
        string ClaimMasterVersion,
        string CsvSpecificationVersion,
        string ReportSpecificationVersion,
        string SnapshotApplicationVersion,
        string OperationApplicationVersion,
        Guid FinalizationOperationId,
        string OperationPayloadSchemaVersion,
        string OperationPayloadSha256,
        string CreatedBy,
        DateTimeOffset CreatedAt);
}
