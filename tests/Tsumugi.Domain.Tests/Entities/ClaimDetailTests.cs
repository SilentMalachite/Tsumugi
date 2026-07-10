using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Tests.Entities;

public sealed class ClaimDetailTests
{
    private static readonly Guid DetailId = Guid.Parse("00000000-0000-0000-0000-000000000201");
    private static readonly Guid BatchId = Guid.Parse("00000000-0000-0000-0000-000000000202");
    private static readonly Guid RecipientId = Guid.Parse("00000000-0000-0000-0000-000000000203");
    private static readonly DateTimeOffset CreatedAt = new(2026, 7, 10, 2, 3, 4, TimeSpan.Zero);

    [Fact]
    public void Create_persists_snapshot_and_totals()
    {
        var detail = Create(Valid);

        detail.Should().BeEquivalentTo(new
        {
            Id = DetailId,
            ClaimBatchId = BatchId,
            RecipientId,
            SnapshotSchemaVersion = "claim-snapshot-v1",
            ClaimMasterVersion = "claim-master-r8-06",
            CsvSpecificationVersion = "csv-r7-10",
            ReportSpecificationVersion = "report-r8-06",
            SnapshotApplicationVersion = "snapshot-app-v1",
            InputSnapshotJson = "{\"input\":true}",
            CalculationSnapshotJson = "{\"calculation\":true}",
            TotalUnits = 100,
            TotalCostYen = 10_000,
            BenefitYen = 9_000,
            BurdenYen = 1_000,
            CreatedBy = "tester",
            CreatedAt,
            ConcurrencyToken = Guid.Empty,
        });
    }

    [Fact]
    public void Create_accepts_nonempty_malformed_json_without_parsing_it()
    {
        var detail = Create(Valid with
        {
            InputSnapshotJson = "not-json",
            CalculationSnapshotJson = "{broken",
        });

        detail.InputSnapshotJson.Should().Be("not-json");
        detail.CalculationSnapshotJson.Should().Be("{broken");
    }

    [Fact]
    public void Batch_and_detail_factories_do_not_enforce_aggregate_version_or_total_consistency()
    {
        var batch = ClaimBatch.NewRecord(
            BatchId,
            Guid.Parse("00000000-0000-0000-0000-000000000204"),
            new ServiceMonth(2026, 6),
            totalUnits: 100,
            totalCostYen: 10_000,
            totalBenefitYen: 9_000,
            totalBurdenYen: 1_000,
            claimMasterVersion: "batch-claim-master",
            csvSpecificationVersion: "batch-csv-specification",
            reportSpecificationVersion: "batch-report-specification",
            snapshotApplicationVersion: "batch-snapshot-application",
            operationApplicationVersion: "operation-application",
            finalizationOperationId: Guid.Parse("00000000-0000-0000-0000-000000000205"),
            operationPayloadSchemaVersion: ClaimBatch.CurrentOperationPayloadSchemaVersion,
            operationPayloadSha256: new string('a', 64),
            createdBy: "tester",
            CreatedAt);
        var detail = Create(Valid with
        {
            ClaimMasterVersion = "detail-claim-master",
            CsvSpecificationVersion = "detail-csv-specification",
            ReportSpecificationVersion = "detail-report-specification",
            SnapshotApplicationVersion = "detail-snapshot-application",
            TotalUnits = 1,
            TotalCostYen = 2,
            BenefitYen = 3,
            BurdenYen = 4,
        });

        detail.ClaimMasterVersion.Should().NotBe(batch.ClaimMasterVersion);
        detail.CsvSpecificationVersion.Should().NotBe(batch.CsvSpecificationVersion);
        detail.ReportSpecificationVersion.Should().NotBe(batch.ReportSpecificationVersion);
        detail.SnapshotApplicationVersion.Should().NotBe(batch.SnapshotApplicationVersion);
        detail.TotalUnits.Should().NotBe(batch.TotalUnits);
        detail.TotalCostYen.Should().NotBe(batch.TotalCostYen);
        detail.BenefitYen.Should().NotBe(batch.TotalBenefitYen);
        detail.BurdenYen.Should().NotBe(batch.TotalBurdenYen);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("claimBatchId")]
    [InlineData("recipientId")]
    public void Create_rejects_empty_identities(string field)
    {
        var input = field switch
        {
            "id" => Valid with { Id = Guid.Empty },
            "claimBatchId" => Valid with { ClaimBatchId = Guid.Empty },
            "recipientId" => Valid with { RecipientId = Guid.Empty },
            _ => throw new InvalidOperationException(),
        };

        FluentActions.Invoking(() => Create(input))
            .Should().Throw<ArgumentException>().WithParameterName(field);
    }

    [Theory]
    [InlineData("snapshotSchemaVersion")]
    [InlineData("claimMasterVersion")]
    [InlineData("csvSpecificationVersion")]
    [InlineData("reportSpecificationVersion")]
    [InlineData("snapshotApplicationVersion")]
    [InlineData("inputSnapshotJson")]
    [InlineData("calculationSnapshotJson")]
    [InlineData("createdBy")]
    public void Create_rejects_blank_required_text(string field)
    {
        var input = field switch
        {
            "snapshotSchemaVersion" => Valid with { SnapshotSchemaVersion = " " },
            "claimMasterVersion" => Valid with { ClaimMasterVersion = " " },
            "csvSpecificationVersion" => Valid with { CsvSpecificationVersion = " " },
            "reportSpecificationVersion" => Valid with { ReportSpecificationVersion = " " },
            "snapshotApplicationVersion" => Valid with { SnapshotApplicationVersion = " " },
            "inputSnapshotJson" => Valid with { InputSnapshotJson = " " },
            "calculationSnapshotJson" => Valid with { CalculationSnapshotJson = " " },
            "createdBy" => Valid with { CreatedBy = " " },
            _ => throw new InvalidOperationException(),
        };

        FluentActions.Invoking(() => Create(input))
            .Should().Throw<ArgumentException>().WithParameterName(field);
    }

    [Theory]
    [InlineData("inputSnapshotJson")]
    [InlineData("calculationSnapshotJson")]
    public void Create_rejects_null_json(string field)
    {
        var input = field switch
        {
            "inputSnapshotJson" => Valid with { InputSnapshotJson = null! },
            "calculationSnapshotJson" => Valid with { CalculationSnapshotJson = null! },
            _ => throw new InvalidOperationException(),
        };

        FluentActions.Invoking(() => Create(input))
            .Should().Throw<ArgumentException>().WithParameterName(field);
    }

    [Theory]
    [InlineData("totalUnits")]
    [InlineData("totalCostYen")]
    [InlineData("benefitYen")]
    [InlineData("burdenYen")]
    public void Create_rejects_negative_totals(string field)
    {
        var input = field switch
        {
            "totalUnits" => Valid with { TotalUnits = -1 },
            "totalCostYen" => Valid with { TotalCostYen = -1 },
            "benefitYen" => Valid with { BenefitYen = -1 },
            "burdenYen" => Valid with { BurdenYen = -1 },
            _ => throw new InvalidOperationException(),
        };

        FluentActions.Invoking(() => Create(input))
            .Should().Throw<ArgumentOutOfRangeException>().WithParameterName(field);
    }

    private static readonly DetailInput Valid = new(
        DetailId,
        BatchId,
        RecipientId,
        SnapshotSchemaVersion: "claim-snapshot-v1",
        ClaimMasterVersion: "claim-master-r8-06",
        CsvSpecificationVersion: "csv-r7-10",
        ReportSpecificationVersion: "report-r8-06",
        SnapshotApplicationVersion: "snapshot-app-v1",
        InputSnapshotJson: "{\"input\":true}",
        CalculationSnapshotJson: "{\"calculation\":true}",
        TotalUnits: 100,
        TotalCostYen: 10_000,
        BenefitYen: 9_000,
        BurdenYen: 1_000,
        CreatedBy: "tester",
        CreatedAt);

    private static ClaimDetail Create(DetailInput input) => ClaimDetail.Create(
        input.Id,
        input.ClaimBatchId,
        input.RecipientId,
        input.SnapshotSchemaVersion,
        input.ClaimMasterVersion,
        input.CsvSpecificationVersion,
        input.ReportSpecificationVersion,
        input.SnapshotApplicationVersion,
        input.InputSnapshotJson,
        input.CalculationSnapshotJson,
        input.TotalUnits,
        input.TotalCostYen,
        input.BenefitYen,
        input.BurdenYen,
        input.CreatedBy,
        input.CreatedAt);

    private sealed record DetailInput(
        Guid Id,
        Guid ClaimBatchId,
        Guid RecipientId,
        string SnapshotSchemaVersion,
        string ClaimMasterVersion,
        string CsvSpecificationVersion,
        string ReportSpecificationVersion,
        string SnapshotApplicationVersion,
        string InputSnapshotJson,
        string CalculationSnapshotJson,
        int TotalUnits,
        int TotalCostYen,
        int BenefitYen,
        int BurdenYen,
        string CreatedBy,
        DateTimeOffset CreatedAt);
}
