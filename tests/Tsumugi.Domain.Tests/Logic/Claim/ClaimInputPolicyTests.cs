using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Tests.Logic.Claim;

public sealed class ClaimInputPolicyTests
{
    private static readonly Guid OfficeId = Guid.Parse("00000000-0000-0000-0000-000000000101");
    private static readonly Guid RecipientId = Guid.Parse("00000000-0000-0000-0000-000000000102");
    private static readonly Guid RootId = Guid.Parse("00000000-0000-0000-0000-000000000103");
    private static readonly ServiceMonth Month = new(2026, 7);

    [Fact]
    public void Empty_history_has_no_effective_input_and_next_revision_is_one()
    {
        ClaimInputPolicy.ValidateHistory([]);

        ClaimInputPolicy.Effective([]).Should().BeNull();
        ClaimInputPolicy.NextRevision([]).Should().Be(1);
    }

    [Fact]
    public void New_correction_cancel_and_correction_reentry_form_a_valid_chain()
    {
        var root = New();
        var correction = Correct(root);
        var cancellation = Cancel(correction);
        var reentry = Correct(cancellation) with { UpperLimitManagedAmountYen = 1_000 };

        var history = new[] { cancellation, root, reentry, correction };

        FluentActions.Invoking(() => ClaimInputPolicy.ValidateHistory(history)).Should().NotThrow();
        ClaimInputPolicy.Effective(history).Should().Be(reentry);
        ClaimInputPolicy.NextRevision(history).Should().Be(5);
    }

    [Fact]
    public void Cancel_head_has_no_effective_input()
    {
        var root = New();
        var cancellation = Cancel(root);

        ClaimInputPolicy.Effective([root, cancellation]).Should().BeNull();
    }

    public static TheoryData<string, IReadOnlyCollection<ClaimInput>> InvalidHistories()
    {
        var root = New();
        var correction = Correct(root);
        var otherRootId = Guid.Parse("10000000-0000-0000-0000-000000000103");

        return new()
        {
            { "missing revision", new[] { root, correction with { Revision = 3 } } },
            { "duplicate revision and branch", new[] { root, correction, Correct(root) } },
            { "duplicate id", new[] { root, correction with { Id = root.Id } } },
            { "root id mismatch", new[] { root, correction with { RootId = otherRootId } } },
            { "expected head mismatch", new[] { root, correction with { ExpectedHeadId = Guid.NewGuid() } } },
            { "root is not self", new[] { root with { RootId = otherRootId } } },
            { "office id changed", new[] { root, correction with { OfficeId = Guid.NewGuid() } } },
            { "recipient id changed", new[] { root, correction with { RecipientId = Guid.NewGuid() } } },
            { "service month changed", new[] { root, correction with { ServiceMonth = new ServiceMonth(2026, 8) } } },
            { "new after root", new[] { root, correction with { Kind = RecordKind.New } } },
            { "unknown kind", new[] { root, correction with { Kind = (RecordKind)999 } } },
            {
                "unknown management result",
                new[] { root with { UpperLimitManagementResult = (UpperLimitManagementResult)999 } }
            },
        };
    }

    [Theory]
    [MemberData(nameof(InvalidHistories))]
    public void Invalid_history_is_rejected(string _, IReadOnlyCollection<ClaimInput> history)
    {
        FluentActions.Invoking(() => ClaimInputPolicy.ValidateHistory(history))
            .Should().Throw<InvalidOperationException>();
    }

    public static TheoryData<string, IReadOnlyCollection<ClaimInput>> CancelPayloadHistories()
    {
        var root = New();
        var cancellation = Cancel(root);

        return new()
        {
            {
                "upper limit management result",
                new[] { root, cancellation with { UpperLimitManagementResult = root.UpperLimitManagementResult } }
            },
            {
                "upper limit managed amount",
                new[] { root, cancellation with { UpperLimitManagedAmountYen = root.UpperLimitManagedAmountYen } }
            },
            {
                "municipal subsidy amount",
                new[] { root, cancellation with { MunicipalSubsidyAmountYen = root.MunicipalSubsidyAmountYen } }
            },
            {
                "exceptional usage start month",
                new[] { root, cancellation with { ExceptionalUsageStartMonth = Month } }
            },
            {
                "exceptional usage end month",
                new[] { root, cancellation with { ExceptionalUsageEndMonth = Month } }
            },
            {
                "exceptional usage days",
                new[] { root, cancellation with { ExceptionalUsageDays = root.ExceptionalUsageDays } }
            },
            {
                "standard usage day total",
                new[] { root, cancellation with { StandardUsageDayTotal = root.StandardUsageDayTotal } }
            },
            {
                "special visit support billed count",
                new[]
                {
                    root,
                    cancellation with
                    {
                        SpecialVisitSupportBilledCount = root.SpecialVisitSupportBilledCount,
                    },
                }
            },
            {
                "offsite support cumulative days",
                new[]
                {
                    root,
                    cancellation with
                    {
                        OffsiteSupportCumulativeDays = root.OffsiteSupportCumulativeDays,
                    },
                }
            },
        };
    }

    [Theory]
    [MemberData(nameof(CancelPayloadHistories))]
    public void Cancel_with_claim_input_payload_is_rejected(
        string _,
        IReadOnlyCollection<ClaimInput> history)
    {
        FluentActions.Invoking(() => ClaimInputPolicy.ValidateHistory(history))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("ClaimInputのCancelは請求入力値を持てません。");
    }

    public static TheoryData<string, ClaimInput> NegativeGroupBAdditionInputs()
    {
        var root = New();

        return new()
        {
            { "special visit support billed count", root with { SpecialVisitSupportBilledCount = -1 } },
            { "offsite support cumulative days", root with { OffsiteSupportCumulativeDays = -1 } },
        };
    }

    [Theory]
    [MemberData(nameof(NegativeGroupBAdditionInputs))]
    public void Negative_group_b_addition_inputs_are_rejected(string _, ClaimInput input)
    {
        FluentActions.Invoking(() => ClaimInputPolicy.ValidateHistory([input]))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Group_b_addition_inputs_round_trip_through_with_and_stay_effective()
    {
        // provider:J611:01:052（訪問支援特別加算・算定回数）と provider:J611:01:054（施設外支援・累計日数）は
        // 上限をコードに持たないため、0 と大きな値の双方をそのまま保持できる。
        var root = New() with
        {
            SpecialVisitSupportBilledCount = 0,
            OffsiteSupportCumulativeDays = 0,
        };
        var correction = Correct(root) with
        {
            SpecialVisitSupportBilledCount = 2,
            OffsiteSupportCumulativeDays = 181,
        };

        var effective = ClaimInputPolicy.Effective([root, correction]);

        effective.Should().Be(correction);
        effective!.SpecialVisitSupportBilledCount.Should().Be(2);
        effective.OffsiteSupportCumulativeDays.Should().Be(181);
        root.SpecialVisitSupportBilledCount.Should().Be(0);
        root.OffsiteSupportCumulativeDays.Should().Be(0);
    }

    [Fact]
    public void Official_upper_limit_management_results_are_closed_to_codes_one_through_three()
    {
        Enum.GetValues<UpperLimitManagementResult>()
            .Should().Equal(
                UpperLimitManagementResult.Result1,
                UpperLimitManagementResult.Result2,
                UpperLimitManagementResult.Result3);
    }

    private static ClaimInput New() => new()
    {
        Id = RootId,
        OfficeId = OfficeId,
        RecipientId = RecipientId,
        ServiceMonth = Month,
        RootId = RootId,
        Revision = 1,
        Kind = RecordKind.New,
        ExpectedHeadId = null,
        UpperLimitManagementResult = UpperLimitManagementResult.Result2,
        UpperLimitManagedAmountYen = 0,
        MunicipalSubsidyAmountYen = 0,
        ExceptionalUsageStartMonth = null,
        ExceptionalUsageEndMonth = null,
        ExceptionalUsageDays = 0,
        StandardUsageDayTotal = 0,
        SpecialVisitSupportBilledCount = 0,
        OffsiteSupportCumulativeDays = 0,
        CreatedAt = DateTimeOffset.UnixEpoch,
        CreatedBy = "tester",
        ConcurrencyToken = Guid.Parse("00000000-0000-0000-0000-000000000104"),
    };

    private static ClaimInput Correct(ClaimInput head) => head with
    {
        Id = Guid.NewGuid(),
        RootId = head.RootId,
        Revision = head.Revision + 1,
        Kind = RecordKind.Correct,
        ExpectedHeadId = head.Id,
        CreatedAt = head.CreatedAt.AddMinutes(1),
        ConcurrencyToken = Guid.NewGuid(),
    };

    private static ClaimInput Cancel(ClaimInput head) => head with
    {
        Id = Guid.NewGuid(),
        RootId = head.RootId,
        Revision = head.Revision + 1,
        Kind = RecordKind.Cancel,
        ExpectedHeadId = head.Id,
        UpperLimitManagementResult = null,
        UpperLimitManagedAmountYen = null,
        MunicipalSubsidyAmountYen = null,
        ExceptionalUsageStartMonth = null,
        ExceptionalUsageEndMonth = null,
        ExceptionalUsageDays = null,
        StandardUsageDayTotal = null,
        SpecialVisitSupportBilledCount = null,
        OffsiteSupportCumulativeDays = null,
        CreatedAt = head.CreatedAt.AddMinutes(1),
        ConcurrencyToken = Guid.NewGuid(),
    };
}
