using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Tests.Logic.Claim;

public sealed class ClaimBatchPolicyTests
{
    private static readonly Guid OfficeId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherOfficeId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid RootId = Guid.Parse("ffffffff-ffff-ffff-ffff-fffffffffff1");
    private static readonly Guid Revision2Id = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Revision3Id = Guid.Parse("10000000-0000-0000-0000-000000000003");
    private static readonly ServiceMonth Month = new(2026, 6);
    private static readonly DateTimeOffset Time = new(2026, 7, 10, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Empty_history_is_valid_without_head_and_next_revision_is_one()
    {
        var history = Array.Empty<ClaimBatch>();

        FluentActions.Invoking(() => ClaimBatchPolicy.ValidateHistory(history)).Should().NotThrow();
        ClaimBatchPolicy.Head(history).Should().BeNull();
        ClaimBatchPolicy.NextRevision(history).Should().Be(1);
    }

    [Fact]
    public void All_operations_reject_null_history()
    {
        FluentActions.Invoking(() => ClaimBatchPolicy.ValidateHistory(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => ClaimBatchPolicy.Head(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => ClaimBatchPolicy.NextRevision(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Revision_one_new_is_head_and_next_revision_is_two()
    {
        var root = New(RootId, Time);

        ClaimBatchPolicy.Head([root]).Should().BeSameAs(root);
        ClaimBatchPolicy.NextRevision([root]).Should().Be(2);
    }

    [Fact]
    public void Root_origin_corrections_resolve_head_by_revision_even_when_input_is_unordered()
    {
        var root = New(RootId, Time);
        var revision2 = Correct(Revision2Id, 2, root.Id, 1, Time.AddMinutes(1));
        var revision3 = Correct(Revision3Id, 3, revision2.Id, 2, Time.AddMinutes(2));
        ClaimBatch[] history = [revision3, root, revision2];

        FluentActions.Invoking(() => ClaimBatchPolicy.ValidateHistory(history)).Should().NotThrow();
        ClaimBatchPolicy.Head(history).Should().BeSameAs(revision3);
        ClaimBatchPolicy.NextRevision(history).Should().Be(4);
    }

    [Fact]
    public void Cancellation_is_included_as_head()
    {
        var root = New(RootId, Time);
        var cancellation = Cancel(Revision2Id, 2, root.Id, 1, Time.AddMinutes(1));
        ClaimBatch[] history = [cancellation, root];

        ClaimBatchPolicy.Head(history).Should().BeSameAs(cancellation);
        ClaimBatchPolicy.NextRevision(history).Should().Be(3);
    }

    [Fact]
    public void Created_at_and_id_order_do_not_affect_revision_head()
    {
        var root = New(RootId, Time.AddHours(2));
        var revision2 = Correct(Revision2Id, 2, root.Id, 1, Time.AddHours(2));
        var revision3 = Correct(Revision3Id, 3, revision2.Id, 2, Time);
        ClaimBatch[] history = [revision2, revision3, root];

        ClaimBatchPolicy.Head(history).Should().BeSameAs(revision3);
    }

    public static TheoryData<string, IReadOnlyCollection<ClaimBatch>> InvalidHistories()
    {
        var root = New(RootId, Time);
        var revision2 = Correct(Revision2Id, 2, root.Id, 1, Time.AddMinutes(1));
        var revision3 = Correct(Revision3Id, 3, revision2.Id, 2, Time.AddMinutes(2));
        var cancellation2 = Cancel(Revision2Id, 2, root.Id, 1, Time.AddMinutes(1));

        return new TheoryData<string, IReadOnlyCollection<ClaimBatch>>
        {
            { "revision gap", new[] { root, revision3 } },
            {
                "duplicate revision",
                new[]
                {
                    root,
                    revision2,
                    revision3 with
                    {
                        Revision = 2,
                        ExpectedHeadBatchId = root.Id,
                        ExpectedHeadRevision = 1,
                    },
                }
            },
            { "zero revision", new[] { root with { Revision = 0 } } },
            { "negative revision", new[] { root with { Revision = -1 } } },
            {
                "new after revision one",
                new[]
                {
                    root,
                    revision2 with
                    {
                        Kind = RecordKind.New,
                        OriginId = null,
                        ExpectedHeadBatchId = null,
                        ExpectedHeadRevision = null,
                    },
                }
            },
            {
                "revision one is not new",
                new[]
                {
                    revision2 with
                    {
                        Revision = 1,
                        ExpectedHeadRevision = 0,
                    },
                }
            },
            {
                "missing new",
                new[]
                {
                    revision2 with
                    {
                        Revision = 1,
                        ExpectedHeadRevision = 0,
                    },
                }
            },
            {
                "multiple new",
                new[]
                {
                    root,
                    New(Revision2Id, Time.AddMinutes(1)) with { Revision = 2 },
                }
            },
            { "new origin", new[] { root with { OriginId = RootId } } },
            { "new expected head id", new[] { root with { ExpectedHeadBatchId = RootId } } },
            { "new expected head revision", new[] { root with { ExpectedHeadRevision = 0 } } },
            { "null correction origin", new[] { root, revision2 with { OriginId = null } } },
            { "empty correction origin", new[] { root, revision2 with { OriginId = Guid.Empty } } },
            { "non-root correction origin", new[] { root, revision2 with { OriginId = Revision2Id } } },
            { "null expected head", new[] { root, revision2 with { ExpectedHeadBatchId = null } } },
            { "empty expected head", new[] { root, revision2 with { ExpectedHeadBatchId = Guid.Empty } } },
            { "wrong expected head", new[] { root, revision2 with { ExpectedHeadBatchId = Revision3Id } } },
            { "null expected head revision", new[] { root, revision2 with { ExpectedHeadRevision = null } } },
            { "wrong expected head revision", new[] { root, revision2 with { ExpectedHeadRevision = 2 } } },
            { "null cancellation origin", new[] { root, cancellation2 with { OriginId = null } } },
            { "empty cancellation origin", new[] { root, cancellation2 with { OriginId = Guid.Empty } } },
            {
                "non-root cancellation origin",
                new[] { root, cancellation2 with { OriginId = Revision2Id } }
            },
            {
                "null cancellation expected head",
                new[] { root, cancellation2 with { ExpectedHeadBatchId = null } }
            },
            {
                "empty cancellation expected head",
                new[] { root, cancellation2 with { ExpectedHeadBatchId = Guid.Empty } }
            },
            {
                "wrong cancellation expected head",
                new[] { root, cancellation2 with { ExpectedHeadBatchId = Revision3Id } }
            },
            {
                "null cancellation expected head revision",
                new[] { root, cancellation2 with { ExpectedHeadRevision = null } }
            },
            {
                "wrong cancellation expected head revision",
                new[] { root, cancellation2 with { ExpectedHeadRevision = 2 } }
            },
            {
                "record after cancellation",
                new[]
                {
                    root,
                    cancellation2,
                    revision3 with { ExpectedHeadBatchId = cancellation2.Id },
                }
            },
            {
                "multiple cancellations",
                new[]
                {
                    root,
                    cancellation2,
                    Cancel(Revision3Id, 3, cancellation2.Id, 2, Time.AddMinutes(2)),
                }
            },
            { "mixed office", new[] { root, revision2 with { OfficeId = OtherOfficeId } } },
            {
                "mixed service month",
                new[] { root, revision2 with { ServiceMonth = new ServiceMonth(2026, 7) } }
            },
            {
                "unknown record kind",
                new[] { root, revision2 with { Kind = (RecordKind)99 } }
            },
        };
    }

    [Theory]
    [MemberData(nameof(InvalidHistories))]
    public void Invalid_history_fails_closed(string _, IReadOnlyCollection<ClaimBatch> history)
    {
        FluentActions.Invoking(() => ClaimBatchPolicy.ValidateHistory(history))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Duplicate_revision_is_not_rescued_by_created_at_or_id_tie_breaking()
    {
        var root = New(RootId, Time);
        var earlierId = Correct(Revision2Id, 2, root.Id, 1, Time.AddHours(1));
        var laterId = Correct(Revision3Id, 2, root.Id, 1, Time.AddHours(-1));

        FluentActions.Invoking(() => ClaimBatchPolicy.ValidateHistory([root, earlierId, laterId]))
            .Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("TotalUnits")]
    [InlineData("TotalCostYen")]
    [InlineData("TotalBenefitYen")]
    [InlineData("TotalBurdenYen")]
    public void Tampered_negative_header_totals_fail_closed(string field)
    {
        var root = New(RootId, Time);
        var tampered = field switch
        {
            "TotalUnits" => root with { TotalUnits = -1 },
            "TotalCostYen" => root with { TotalCostYen = -1 },
            "TotalBenefitYen" => root with { TotalBenefitYen = -1 },
            "TotalBurdenYen" => root with { TotalBurdenYen = -1 },
            _ => throw new InvalidOperationException(),
        };

        FluentActions.Invoking(() => ClaimBatchPolicy.ValidateHistory([tampered]))
            .Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("TotalUnits")]
    [InlineData("TotalCostYen")]
    [InlineData("TotalBenefitYen")]
    [InlineData("TotalBurdenYen")]
    public void Tampered_nonzero_cancellation_totals_fail_closed(string field)
    {
        var root = New(RootId, Time);
        var cancellation = Cancel(Revision2Id, 2, root.Id, 1, Time.AddMinutes(1));
        var tampered = field switch
        {
            "TotalUnits" => cancellation with { TotalUnits = 1 },
            "TotalCostYen" => cancellation with { TotalCostYen = 1 },
            "TotalBenefitYen" => cancellation with { TotalBenefitYen = 1 },
            "TotalBurdenYen" => cancellation with { TotalBurdenYen = 1 },
            _ => throw new InvalidOperationException(),
        };

        FluentActions.Invoking(() => ClaimBatchPolicy.ValidateHistory([root, tampered]))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Head_and_NextRevision_also_validate_history()
    {
        var root = New(RootId, Time);
        var revision3 = Correct(Revision3Id, 3, root.Id, 2, Time.AddMinutes(2));
        ClaimBatch[] invalid = [root, revision3];

        FluentActions.Invoking(() => ClaimBatchPolicy.Head(invalid))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => ClaimBatchPolicy.NextRevision(invalid))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void NextRevision_does_not_silently_overflow_int_max_value()
    {
        var intMaxHead = New(RootId, Time) with { Revision = int.MaxValue };

        FluentActions.Invoking(() => ClaimBatchPolicy.NextRevision([intMaxHead]))
            .Should().Throw<OverflowException>();
    }

    private static ClaimBatch New(Guid id, DateTimeOffset createdAt) => ClaimBatch.NewRecord(
        id,
        OfficeId,
        Month,
        totalUnits: 100,
        totalCostYen: 10_000,
        totalBenefitYen: 9_000,
        totalBurdenYen: 1_000,
        claimMasterVersion: "claim-master-r8-06",
        csvSpecificationVersion: "csv-r7-10",
        reportSpecificationVersion: "report-r8-06",
        snapshotApplicationVersion: "snapshot-app-v1",
        operationApplicationVersion: "operation-app-v1",
        finalizationOperationId: id,
        operationPayloadSchemaVersion: ClaimBatch.CurrentOperationPayloadSchemaVersion,
        operationPayloadSha256: new string('a', 64),
        createdBy: "tester",
        createdAt);

    private static ClaimBatch Correct(
        Guid id,
        int revision,
        Guid expectedHeadBatchId,
        int expectedHeadRevision,
        DateTimeOffset createdAt) => ClaimBatch.Correction(
        id,
        OfficeId,
        Month,
        revision,
        RootId,
        expectedHeadBatchId,
        expectedHeadRevision,
        totalUnits: 100,
        totalCostYen: 10_000,
        totalBenefitYen: 9_000,
        totalBurdenYen: 1_000,
        claimMasterVersion: "claim-master-r8-06",
        csvSpecificationVersion: "csv-r7-10",
        reportSpecificationVersion: "report-r8-06",
        snapshotApplicationVersion: "snapshot-app-v1",
        operationApplicationVersion: "operation-app-v1",
        finalizationOperationId: id,
        operationPayloadSchemaVersion: ClaimBatch.CurrentOperationPayloadSchemaVersion,
        operationPayloadSha256: new string('b', 64),
        createdBy: "tester",
        createdAt);

    private static ClaimBatch Cancel(
        Guid id,
        int revision,
        Guid expectedHeadBatchId,
        int expectedHeadRevision,
        DateTimeOffset createdAt) => ClaimBatch.Cancellation(
        id,
        OfficeId,
        Month,
        revision,
        RootId,
        expectedHeadBatchId,
        expectedHeadRevision,
        claimMasterVersion: "claim-master-r8-06",
        csvSpecificationVersion: "csv-r7-10",
        reportSpecificationVersion: "report-r8-06",
        snapshotApplicationVersion: "snapshot-app-v1",
        operationApplicationVersion: "operation-app-v1",
        finalizationOperationId: id,
        operationPayloadSchemaVersion: ClaimBatch.CurrentOperationPayloadSchemaVersion,
        operationPayloadSha256: new string('c', 64),
        createdBy: "tester",
        createdAt);
}
