using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Tests.Logic.Claim;

public sealed class ClaimEvidencePolicyTests
{
    private static readonly Guid OfficeId = Guid.Parse("00000000-0000-0000-0000-000000000301");
    private static readonly Guid AverageRootId = Guid.Parse("00000000-0000-0000-0000-000000000302");
    private static readonly Guid ProfileRootId = Guid.Parse("00000000-0000-0000-0000-000000000303");
    private const string MasterVersionValue = "claim-master-r8-06";
    private static readonly AverageWageBandOption NumericOption = new(
        AverageWageBandOptionKind.Numeric,
        11);
    private static readonly AverageWageBandOption FiledTransitionOption = new(
        AverageWageBandOptionKind.FiledTransition,
        8);
    private static readonly AverageWageBandOption ReformExemptOption = new(
        AverageWageBandOptionKind.Numeric,
        2);
    private static readonly AverageWageBandOption UnchangedOption = new(
        AverageWageBandOptionKind.Numeric,
        7);

    [Fact]
    public void Average_evidence_supports_correction_cancel_and_correction_reentry()
    {
        var root = NewAverage();
        var correction = Correct(root) with { AnnualWagePaidYen = 1_100_000 };
        var cancellation = Cancel(correction);
        var reentry = Correct(cancellation) with
        {
            AnnualWagePaidYen = 1_200_000,
            AnnualExtendedUsers = 2_500,
            AnnualOpeningDays = 240,
            Completeness = FiscalYearCompleteness.Complete,
            EvidenceDocumentId = "average-evidence-2",
            DailyEvidenceReference = "daily-record-ledger-2",
            MonthlyEvidenceReference = "monthly-wage-ledger-2",
            ConfirmedAt = DateTimeOffset.UnixEpoch.AddDays(1),
            ConfirmedBy = "reviewer",
            ConfirmationReason = "訂正後の原本を再確認",
        };
        var history = new[] { reentry, cancellation, root, correction };

        FluentActions.Invoking(() => AverageWageAnnualEvidencePolicy.ValidateHistory(history))
            .Should().NotThrow();
        AverageWageAnnualEvidencePolicy.Effective(history).Should().Be(reentry);
        AverageWageAnnualEvidencePolicy.NextRevision(history).Should().Be(5);
    }

    [Fact]
    public void Average_cancel_head_has_no_effective_evidence()
    {
        var root = NewAverage();
        var cancellation = Cancel(root);

        AverageWageAnnualEvidencePolicy.Effective([root, cancellation]).Should().BeNull();
        FluentActions.Invoking(() => AverageWageAnnualEvidencePolicy.ValidateHistory([
            root, cancellation with { AnnualWagePaidYen = root.AnnualWagePaidYen }]))
            .Should().Throw<InvalidOperationException>();
    }

    public static TheoryData<string, IReadOnlyCollection<AverageWageAnnualEvidence>>
        InvalidAverageHistories()
    {
        var root = NewAverage();
        var correction = Correct(root);

        return new()
        {
            { "missing revision", new[] { root, correction with { Revision = 3 } } },
            { "branch", new[] { root, correction, Correct(root) } },
            { "duplicate id", new[] { root, correction with { Id = root.Id } } },
            { "root mismatch", new[] { root, correction with { RootId = Guid.NewGuid() } } },
            { "expected head mismatch", new[] { root, correction with { ExpectedHeadId = Guid.NewGuid() } } },
            { "root is not self", new[] { root with { RootId = Guid.NewGuid() } } },
            { "office changed", new[] { root, correction with { OfficeId = Guid.NewGuid() } } },
            { "fiscal year changed", new[] { root, correction with { SourceFiscalYear = 2024 } } },
            { "period changed", new[] { root, correction with { PeriodEnd = new DateOnly(2026, 3, 30) } } },
            { "new after root", new[] { root, correction with { Kind = RecordKind.New } } },
            { "unknown kind", new[] { root, correction with { Kind = (RecordKind)999 } } },
        };
    }

    [Theory]
    [MemberData(nameof(InvalidAverageHistories))]
    public void Average_history_invariants_are_enforced(
        string _,
        IReadOnlyCollection<AverageWageAnnualEvidence> history)
    {
        FluentActions.Invoking(() => AverageWageAnnualEvidencePolicy.ValidateHistory(history))
            .Should().Throw<InvalidOperationException>();
    }

    public static TheoryData<string, AverageWageAnnualEvidence> InvalidAverageValues()
    {
        var root = NewAverage();

        return new()
        {
            { "period start does not match fiscal year", root with { PeriodStart = new DateOnly(2025, 4, 2) } },
            { "period end does not match fiscal year", root with { PeriodEnd = new DateOnly(2026, 4, 1) } },
            { "negative wage", root with { AnnualWagePaidYen = -1 } },
            { "zero extended users", root with { AnnualExtendedUsers = 0 } },
            { "zero opening days", root with { AnnualOpeningDays = 0 } },
            { "incomplete", root with { Completeness = FiscalYearCompleteness.Incomplete } },
            { "unknown completeness", root with { Completeness = FiscalYearCompleteness.Unknown } },
            { "no evidence", root with { EvidenceDocumentId = "" } },
            { "no daily trace", root with { DailyEvidenceReference = "" } },
            { "no monthly trace", root with { MonthlyEvidenceReference = "" } },
            { "not confirmed at", root with { ConfirmedAt = null } },
            { "default confirmed at", root with { ConfirmedAt = DateTimeOffset.MinValue } },
            { "not confirmed by", root with { ConfirmedBy = null } },
            { "no confirmation reason", root with { ConfirmationReason = null } },
        };
    }

    [Theory]
    [MemberData(nameof(InvalidAverageValues))]
    public void Average_incomplete_or_unconfirmed_values_are_rejected(
        string _,
        AverageWageAnnualEvidence evidence)
    {
        FluentActions.Invoking(() => AverageWageAnnualEvidencePolicy.ValidateHistory([evidence]))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Office_profile_supports_correction_cancel_and_correction_reentry()
    {
        var policy = ProfilePolicy();
        var root = NewProfile();
        var correction = Correct(root) with { ConfirmationReason = "台帳訂正" };
        var cancellation = Cancel(correction);
        var reentry = Correct(cancellation) with
        {
            MasterVersion = new ClaimMasterVersion(MasterVersionValue),
            ReformStatus = R8ReformStatus.ReformTarget,
            AverageWageBandOption = NumericOption,
            EvidenceDocumentId = "profile-evidence-2",
            ConfirmedAt = DateTimeOffset.UnixEpoch.AddDays(1),
            ConfirmedBy = "reviewer",
            ConfirmationReason = "訂正後の台帳を再確認",
        };
        var history = new[] { cancellation, root, reentry, correction };

        FluentActions.Invoking(() => policy.ValidateHistory(history)).Should().NotThrow();
        policy.Effective(history).Should().Be(reentry);
        policy.NextRevision(history).Should().Be(5);
    }

    [Fact]
    public void Office_cancel_head_has_no_effective_profile()
    {
        var policy = ProfilePolicy();
        var root = NewProfile();

        policy.Effective([root, Cancel(root)]).Should().BeNull();
        FluentActions.Invoking(() => policy.ValidateHistory([
            root, Cancel(root) with { MasterVersion = root.MasterVersion }]))
            .Should().Throw<InvalidOperationException>();
    }

    public static TheoryData<string, IReadOnlyCollection<OfficeClaimProfile>>
        InvalidProfileHistories()
    {
        var root = NewProfile();
        var correction = Correct(root);

        return new()
        {
            { "missing revision", new[] { root, correction with { Revision = 3 } } },
            { "branch", new[] { root, correction, Correct(root) } },
            { "duplicate id", new[] { root, correction with { Id = root.Id } } },
            { "root mismatch", new[] { root, correction with { RootId = Guid.NewGuid() } } },
            { "expected head mismatch", new[] { root, correction with { ExpectedHeadId = Guid.NewGuid() } } },
            { "root is not self", new[] { root with { RootId = Guid.NewGuid() } } },
            { "office changed", new[] { root, correction with { OfficeId = Guid.NewGuid() } } },
            { "effective start changed", new[] { root, correction with { EffectiveFrom = new DateOnly(2026, 7, 2) } } },
            { "effective end changed", new[] { root, correction with { EffectiveTo = new DateOnly(2027, 1, 2) } } },
            { "new after root", new[] { root, correction with { Kind = RecordKind.New } } },
            { "unknown kind", new[] { root, correction with { Kind = (RecordKind)999 } } },
        };
    }

    [Theory]
    [MemberData(nameof(InvalidProfileHistories))]
    public void Office_profile_history_invariants_are_enforced(
        string _,
        IReadOnlyCollection<OfficeClaimProfile> history)
    {
        FluentActions.Invoking(() => ProfilePolicy().ValidateHistory(history))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Office_profile_rejects_unknown_status_disallowed_option_and_missing_evidence()
    {
        var policy = ProfilePolicy();
        var root = NewProfile();
        var disallowed = new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 12);

        FluentActions.Invoking(
                () => policy.ValidateHistory([root with { ReformStatus = R8ReformStatus.Unknown }]))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(
                () => policy.ValidateHistory([root with { AverageWageBandOption = disallowed }]))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(
                () => policy.ValidateHistory([root with { EvidenceDocumentId = "" }]))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(
                () => policy.ValidateHistory([
                    root with { MasterVersion = new ClaimMasterVersion("claim-master-r6-06") }]))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(
                () => policy.ValidateHistory([root with { EffectiveFrom = DateOnly.MinValue, EffectiveTo = null }]))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(
                () => policy.ValidateHistory([root with { ConfirmedAt = DateTimeOffset.MinValue }]))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Office_profile_rejects_invalid_allowed_option_and_r8_period_status_combinations()
    {
        FluentActions.Invoking(
                () => new AverageWageBandOptionVersionRule(
                    new ClaimMasterVersion(MasterVersionValue),
                    new ServiceMonth(2026, 6),
                    null,
                    [default],
                    new Dictionary<R8ReformStatus, IReadOnlyCollection<AverageWageBandOption>>()))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(
                () => new AverageWageBandOptionVersionRule(
                    new ClaimMasterVersion(MasterVersionValue),
                    new ServiceMonth(2026, 6),
                    null,
                    [NumericOption, NumericOption],
                    new Dictionary<R8ReformStatus, IReadOnlyCollection<AverageWageBandOption>>
                    {
                        [R8ReformStatus.ReformTarget] = [NumericOption],
                    }))
            .Should().Throw<ArgumentException>();

        var postR8 = NewProfile() with { ReformStatus = R8ReformStatus.NotApplicableBeforeR8 };
        var crossingR8 = NewProfile() with
        {
            EffectiveFrom = new DateOnly(2026, 5, 1),
            EffectiveTo = new DateOnly(2026, 6, 30),
        };

        FluentActions.Invoking(() => ProfilePolicy().ValidateHistory([postR8]))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => ProfilePolicy().ValidateHistory([crossingR8]))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Office_policy_does_not_convert_numeric_code_eight_to_filed_transition()
    {
        var root = NewProfile() with
        {
            AverageWageBandOption = new AverageWageBandOption(
                AverageWageBandOptionKind.Numeric,
                8),
        };

        FluentActions.Invoking(() => ProfilePolicy().ValidateHistory([root]))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reform_exempt_requires_complete_official_registration_comparison()
    {
        var profile = NewProfile() with
        {
            ReformStatus = R8ReformStatus.ReformExempt,
            AverageWageBandOption = ReformExemptOption,
            EarlierRegisteredBandOption = new VersionedAverageWageBandOption(
                new ClaimMasterVersion("claim-master-r6-04"),
                new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 2)),
            EarlierRegistrationMonth = new ServiceMonth(2024, 3),
            LaterRegisteredBandOption = new VersionedAverageWageBandOption(
                new ClaimMasterVersion("claim-master-r6-06"),
                new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 3)),
            LaterRegistrationMonth = new ServiceMonth(2024, 4),
            ReformComparisonEvidenceDocumentId = "official-band-comparison",
        };

        FluentActions.Invoking(() => ProfilePolicy().ValidateHistory([profile])).Should().NotThrow();
        FluentActions.Invoking(() => ProfilePolicy().ValidateHistory([
            profile with { AverageWageBandOption = NumericOption }]))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*組合せ*");
        FluentActions.Invoking(() => ProfilePolicy().ValidateHistory([
            profile with { EarlierRegisteredBandOption = null }])).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => ProfilePolicy().ValidateHistory([
            profile with { LaterRegistrationMonth = new ServiceMonth(2024, 2) }])).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => ProfilePolicy().ValidateHistory([
            profile with { ReformComparisonEvidenceDocumentId = null }])).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => ProfilePolicy().ValidateHistory([
            profile with
            {
                EarlierRegisteredBandOption = new VersionedAverageWageBandOption(
                    new ClaimMasterVersion("bogus-version"),
                    new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 999)),
            }])).Should().Throw<InvalidOperationException>()
            .WithMessage("*過去比較option*");
        FluentActions.Invoking(() => ProfilePolicy().ValidateHistory([
            profile with { LaterRegistrationMonth = new ServiceMonth(2026, 6) }]))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*公式登録区分比較*");

        var conflictingTransition = profile with
        {
            AverageWageBandOption = FiledTransitionOption,
            DesignationDate = new DateOnly(2026, 7, 1),
            SupportStartDate = new DateOnly(2026, 7, 1),
            FiledTransitionPeriod = new DateRange(
                new DateOnly(2026, 7, 1),
                new DateOnly(2027, 1, 1)),
            FiledTransitionEvidenceDocumentId = "filed-transition-evidence",
        };
        FluentActions.Invoking(() => ProfilePolicy().ValidateHistory([conflictingTransition]))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*組合せ*");
    }

    public static TheoryData<string, OfficeClaimProfile> InvalidFiledTransitionProfiles()
    {
        var transition = NewProfile() with
        {
            EffectiveFrom = new DateOnly(2026, 7, 1),
            EffectiveTo = new DateOnly(2027, 6, 30),
            AverageWageBandOption = FiledTransitionOption,
            DesignationDate = new DateOnly(2026, 7, 1),
            SupportStartDate = new DateOnly(2026, 7, 1),
            FiledTransitionPeriod = new DateRange(
                new DateOnly(2026, 7, 1),
                new DateOnly(2027, 6, 30)),
            FiledTransitionEvidenceDocumentId = "filed-transition-evidence",
        };

        return new()
        {
            { "designation date missing", transition with { DesignationDate = null } },
            { "support start date missing", transition with { SupportStartDate = null } },
            { "unbounded transition", transition with { EffectiveTo = null } },
            { "starts before designation", transition with { EffectiveFrom = new DateOnly(2026, 6, 30) } },
            { "support starts before designation", transition with { SupportStartDate = new DateOnly(2026, 6, 30) } },
            {
                "extends to first anniversary",
                transition with
                {
                    EffectiveTo = new DateOnly(2027, 7, 1),
                    FiledTransitionPeriod = new DateRange(
                        new DateOnly(2026, 7, 1),
                        new DateOnly(2027, 7, 1)),
                }
            },
            { "transition period missing", transition with { FiledTransitionPeriod = null } },
            { "transition evidence missing", transition with { FiledTransitionEvidenceDocumentId = null } },
            {
                "transition period differs from registration",
                transition with
                {
                    FiledTransitionPeriod = new DateRange(
                        new DateOnly(2026, 8, 1),
                        new DateOnly(2027, 6, 30)),
                }
            },
            { "support starts after transition", transition with { SupportStartDate = new DateOnly(2027, 7, 1) } },
        };
    }

    [Theory]
    [MemberData(nameof(InvalidFiledTransitionProfiles))]
    public void Filed_transition_requires_consistent_official_initial_period(
        string _,
        OfficeClaimProfile profile)
    {
        FluentActions.Invoking(() => ProfilePolicy().ValidateHistory([profile]))
            .Should().Throw<InvalidOperationException>();
    }

    private static AverageWageAnnualEvidence NewAverage() => new()
    {
        Id = AverageRootId,
        OfficeId = OfficeId,
        SourceFiscalYear = 2025,
        PeriodStart = new DateOnly(2025, 4, 1),
        PeriodEnd = new DateOnly(2026, 3, 31),
        RootId = AverageRootId,
        Revision = 1,
        Kind = RecordKind.New,
        ExpectedHeadId = null,
        AnnualWagePaidYen = 1_000_000,
        AnnualExtendedUsers = 2_400,
        AnnualOpeningDays = 240,
        Completeness = FiscalYearCompleteness.Complete,
        EvidenceDocumentId = "average-evidence-1",
        DailyEvidenceReference = "daily-record-ledger",
        MonthlyEvidenceReference = "monthly-wage-ledger",
        ConfirmedAt = DateTimeOffset.UnixEpoch,
        ConfirmedBy = "reviewer",
        ConfirmationReason = "年度実績を照合済み",
        CreatedAt = DateTimeOffset.UnixEpoch,
        CreatedBy = "tester",
        ConcurrencyToken = Guid.Parse("00000000-0000-0000-0000-000000000304"),
    };

    private static AverageWageAnnualEvidence Correct(AverageWageAnnualEvidence head) => head with
    {
        Id = Guid.NewGuid(),
        RootId = head.RootId,
        Revision = head.Revision + 1,
        Kind = RecordKind.Correct,
        ExpectedHeadId = head.Id,
        CreatedAt = head.CreatedAt.AddMinutes(1),
        ConcurrencyToken = Guid.NewGuid(),
    };

    private static AverageWageAnnualEvidence Cancel(AverageWageAnnualEvidence head) => head with
    {
        Id = Guid.NewGuid(),
        RootId = head.RootId,
        Revision = head.Revision + 1,
        Kind = RecordKind.Cancel,
        ExpectedHeadId = head.Id,
        AnnualWagePaidYen = null,
        AnnualExtendedUsers = null,
        AnnualOpeningDays = null,
        Completeness = null,
        EvidenceDocumentId = null,
        DailyEvidenceReference = null,
        MonthlyEvidenceReference = null,
        ConfirmedAt = null,
        ConfirmedBy = null,
        ConfirmationReason = null,
        CreatedAt = head.CreatedAt.AddMinutes(1),
        ConcurrencyToken = Guid.NewGuid(),
    };

    private static OfficeClaimProfile NewProfile() => new()
    {
        Id = ProfileRootId,
        OfficeId = OfficeId,
        EffectiveFrom = new DateOnly(2026, 7, 1),
        EffectiveTo = new DateOnly(2027, 1, 1),
        RootId = ProfileRootId,
        Revision = 1,
        Kind = RecordKind.New,
        ExpectedHeadId = null,
        MasterVersion = new ClaimMasterVersion(MasterVersionValue),
        ReformStatus = R8ReformStatus.ReformTarget,
        AverageWageBandOption = NumericOption,
        DesignationDate = null,
        SupportStartDate = null,
        EvidenceDocumentId = "profile-evidence-1",
        EarlierRegisteredBandOption = null,
        EarlierRegistrationMonth = null,
        LaterRegisteredBandOption = null,
        LaterRegistrationMonth = null,
        ReformComparisonEvidenceDocumentId = null,
        FiledTransitionPeriod = null,
        FiledTransitionEvidenceDocumentId = null,
        ConfirmedAt = DateTimeOffset.UnixEpoch,
        ConfirmedBy = "reviewer",
        ConfirmationReason = "指定権者台帳を確認済み",
        CreatedAt = DateTimeOffset.UnixEpoch,
        CreatedBy = "tester",
        ConcurrencyToken = Guid.Parse("00000000-0000-0000-0000-000000000305"),
    };

    private static OfficeClaimProfile Correct(OfficeClaimProfile head) => head with
    {
        Id = Guid.NewGuid(),
        RootId = head.RootId,
        Revision = head.Revision + 1,
        Kind = RecordKind.Correct,
        ExpectedHeadId = head.Id,
        CreatedAt = head.CreatedAt.AddMinutes(1),
        ConcurrencyToken = Guid.NewGuid(),
    };

    private static OfficeClaimProfile Cancel(OfficeClaimProfile head) => head with
    {
        Id = Guid.NewGuid(),
        RootId = head.RootId,
        Revision = head.Revision + 1,
        Kind = RecordKind.Cancel,
        ExpectedHeadId = head.Id,
        MasterVersion = null,
        ReformStatus = null,
        AverageWageBandOption = null,
        DesignationDate = null,
        SupportStartDate = null,
        EvidenceDocumentId = null,
        EarlierRegisteredBandOption = null,
        EarlierRegistrationMonth = null,
        LaterRegisteredBandOption = null,
        LaterRegistrationMonth = null,
        ReformComparisonEvidenceDocumentId = null,
        FiledTransitionPeriod = null,
        FiledTransitionEvidenceDocumentId = null,
        ConfirmedAt = null,
        ConfirmedBy = null,
        ConfirmationReason = null,
        CreatedAt = head.CreatedAt.AddMinutes(1),
        ConcurrencyToken = Guid.NewGuid(),
    };

    private static OfficeClaimProfilePolicy ProfilePolicy() => new(
        new ClaimMasterVersion(MasterVersionValue),
        OptionRules(),
        new DateOnly(2026, 6, 1),
        designationDate => designationDate.AddYears(1));

    private static AverageWageBandOptionVersionRule[] OptionRules() =>
    [
        new(
            new ClaimMasterVersion("claim-master-r6-04"),
            new ServiceMonth(2024, 1),
            new ServiceMonth(2024, 3),
            [ReformExemptOption],
            new Dictionary<R8ReformStatus, IReadOnlyCollection<AverageWageBandOption>>
            {
                [R8ReformStatus.NotApplicableBeforeR8] = [ReformExemptOption],
            }),
        new(
            new ClaimMasterVersion("claim-master-r6-06"),
            new ServiceMonth(2024, 4),
            new ServiceMonth(2026, 5),
            [new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 3)],
            new Dictionary<R8ReformStatus, IReadOnlyCollection<AverageWageBandOption>>
            {
                [R8ReformStatus.NotApplicableBeforeR8] =
                    [new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 3)],
            }),
        new(
            new ClaimMasterVersion(MasterVersionValue),
            new ServiceMonth(2026, 6),
            null,
            [NumericOption, FiledTransitionOption, ReformExemptOption, UnchangedOption],
            new Dictionary<R8ReformStatus, IReadOnlyCollection<AverageWageBandOption>>
            {
                [R8ReformStatus.ReformTarget] = [NumericOption, FiledTransitionOption],
                [R8ReformStatus.ReformExempt] = [ReformExemptOption],
                [R8ReformStatus.UnchangedBelow15000] = [UnchangedOption],
            }),
    ];
}
