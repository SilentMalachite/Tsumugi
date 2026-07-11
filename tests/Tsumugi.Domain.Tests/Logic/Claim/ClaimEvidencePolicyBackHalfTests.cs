using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Tests.Logic.Claim;

public sealed class ClaimEvidencePolicyTestsBackHalf
{
    private static readonly Guid CertificateId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid CertificateRootId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid StatementRootId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid RecipientId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid ManagingOfficeId = Guid.Parse("20000000-0000-0000-0000-000000000003");

    [Fact]
    public void Certificate_history_allows_correction_after_cancel_and_formal_zero()
    {
        var root = CertificateNew();
        var correction = CertificateCorrect(root);
        var cancellation = CertificateCancel(correction);
        var reentry = CertificateCorrect(cancellation) with
        {
            MonthlyCostCap = new EnteredYen(true, 0),
            Article31AmountYen = new EnteredYen(true, 0),
        };
        var history = new[] { cancellation, root, reentry, correction };

        FluentActions.Invoking(() => CertificateClaimEvidencePolicy.ValidateHistory(history)).Should().NotThrow();
        CertificateClaimEvidencePolicy.Effective(history).Should().Be(reentry);
        CertificateClaimEvidencePolicy.NextRevision(history).Should().Be(5);
    }

    [Fact]
    public void Certificate_cancel_head_has_no_effective_evidence()
    {
        var root = CertificateNew();
        var cancellation = CertificateCancel(root);
        CertificateClaimEvidencePolicy.Effective([root, cancellation]).Should().BeNull();
        FluentActions.Invoking(() => CertificateClaimEvidencePolicy.ValidateHistory([
            root, cancellation with { MonthlyCostCap = root.MonthlyCostCap }]))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Certificate_rejects_unentered_cap_but_accepts_entered_zero()
    {
        var root = CertificateNew();
        FluentActions.Invoking(() => CertificateClaimEvidencePolicy.ValidateHistory([
            root with { MonthlyCostCap = new EnteredYen(false, null) }])).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => CertificateClaimEvidencePolicy.ValidateHistory([
            root with { MonthlyCostCap = new EnteredYen(true, 0) }])).Should().NotThrow();
    }

    [Theory]
    [InlineData(Article31SpecialBurdenStatus.Unknown)]
    [InlineData((Article31SpecialBurdenStatus)999)]
    public void Certificate_rejects_unknown_article31_status(Article31SpecialBurdenStatus status)
    {
        FluentActions.Invoking(() => CertificateClaimEvidencePolicy.ValidateHistory([
            CertificateNew() with { Article31Status = status }])).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Certificate_article31_applicable_requires_amount_period_original_and_confirmation()
    {
        var root = CertificateNew();
        CertificateClaimEvidence[] invalid =
        [
            root with { Article31AmountYen = new EnteredYen(false, null) },
            root with { Article31EffectivePeriod = null },
            root with { OriginalDocumentReference = null },
            root with { ConfirmedAt = null },
            root with { ConfirmedAt = DateTimeOffset.MinValue },
            root with { ConfirmedBy = null },
            root with { ConfirmationReason = null },
            root with { Article31EffectivePeriod = new DateRange(new(2025, 4, 1), new(2027, 3, 31)) },
        ];

        invalid.Should().AllSatisfy(evidence =>
            FluentActions.Invoking(() => CertificateClaimEvidencePolicy.ValidateHistory([evidence]))
                .Should().Throw<InvalidOperationException>());
    }

    [Fact]
    public void Certificate_article31_not_applicable_requires_period_and_rejects_amount()
    {
        var root = CertificateNew() with
        {
            Article31Status = Article31SpecialBurdenStatus.NotApplicable,
            Article31AmountYen = new EnteredYen(false, null),
        };

        FluentActions.Invoking(() => CertificateClaimEvidencePolicy.ValidateHistory([root])).Should().NotThrow();
        FluentActions.Invoking(() => CertificateClaimEvidencePolicy.ValidateHistory([
            root with { Article31AmountYen = new EnteredYen(true, 0) }])).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => CertificateClaimEvidencePolicy.ValidateHistory([
            root with { Article31EffectivePeriod = null }])).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Certificate_upper_limit_applicability_requires_consistent_office_number()
    {
        var root = CertificateNew();
        CertificateClaimEvidence[] invalid =
        [
            root with { UpperLimitManagementApplicability = UpperLimitManagementApplicability.Unknown },
            root with { UpperLimitManagementApplicability = (UpperLimitManagementApplicability)999 },
            root with { UpperLimitManagementOfficeNumber = null },
            root with
            {
                UpperLimitManagementApplicability = UpperLimitManagementApplicability.NotApplicable,
                UpperLimitManagementOfficeNumber = "office-number",
            },
        ];

        invalid.Should().AllSatisfy(evidence =>
            FluentActions.Invoking(() => CertificateClaimEvidencePolicy.ValidateHistory([evidence]))
                .Should().Throw<InvalidOperationException>());
    }

    public static TheoryData<string, IReadOnlyCollection<CertificateClaimEvidence>> InvalidCertificateHistories()
    {
        var root = CertificateNew();
        var correction = CertificateCorrect(root);
        return new()
        {
            { "missing revision", new[] { root, correction with { Revision = 3 } } },
            { "duplicate revision branch", new[] { root, correction, CertificateCorrect(root) } },
            { "duplicate id", new[] { root, correction with { Id = root.Id } } },
            { "root mismatch", new[] { root, correction with { RootId = Guid.NewGuid() } } },
            { "expected head mismatch", new[] { root, correction with { ExpectedHeadId = Guid.NewGuid() } } },
            { "root not self", new[] { root with { RootId = Guid.NewGuid() } } },
            { "certificate changed", new[] { root, correction with { CertificateId = Guid.NewGuid() } } },
            { "period changed", new[] { root, correction with { Validity = new DateRange(new(2026, 5, 1), new(2027, 3, 31)) } } },
            { "new after root", new[] { root, correction with { Kind = RecordKind.New } } },
            { "unknown kind", new[] { root, correction with { Kind = (RecordKind)999 } } },
        };
    }

    [Theory]
    [MemberData(nameof(InvalidCertificateHistories))]
    public void Certificate_rejects_invalid_history(string _, IReadOnlyCollection<CertificateClaimEvidence> history)
        => FluentActions.Invoking(() => CertificateClaimEvidencePolicy.ValidateHistory(history))
            .Should().Throw<InvalidOperationException>();

    [Theory]
    [InlineData(UpperLimitManagementResult.Result1)]
    [InlineData(UpperLimitManagementResult.Result2)]
    [InlineData(UpperLimitManagementResult.Result3)]
    public void Statement_accepts_official_result_relationships(UpperLimitManagementResult result)
    {
        var statement = StatementNew(result);
        var lines = Lines(statement, result);

        FluentActions.Invoking(() => UpperLimitManagementStatementPolicy.ValidateHistory([statement], lines))
            .Should().NotThrow();
        UpperLimitManagementStatementPolicy.Effective([statement], lines).Should().Be(statement);
    }

    [Fact]
    public void Statement_history_allows_correction_after_cancel()
    {
        var root = StatementNew(UpperLimitManagementResult.Result2);
        var correction = StatementCorrect(root);
        var cancellation = StatementCancel(correction);
        var reentry = StatementCorrect(cancellation);
        var history = new[] { reentry, cancellation, root, correction };
        var lines = history.Where(x => x.Kind != RecordKind.Cancel).SelectMany(x => Lines(x, x.Result)).ToArray();

        FluentActions.Invoking(() => UpperLimitManagementStatementPolicy.ValidateHistory(history, lines)).Should().NotThrow();
        UpperLimitManagementStatementPolicy.Effective(history, lines).Should().Be(reentry);
        UpperLimitManagementStatementPolicy.NextRevision(history, lines).Should().Be(5);
    }

    [Fact]
    public void Statement_cancel_head_is_not_effective_and_has_no_lines()
    {
        var root = StatementNew(UpperLimitManagementResult.Result2);
        var cancel = StatementCancel(root);
        var lines = Lines(root, root.Result);
        UpperLimitManagementStatementPolicy.Effective([root, cancel], lines).Should().BeNull();
        FluentActions.Invoking(() => UpperLimitManagementStatementPolicy.ValidateHistory(
            [root, cancel], [.. lines, Lines(cancel, cancel.Result)[0]])).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => UpperLimitManagementStatementPolicy.ValidateHistory(
            [root, cancel with { TotalCostYen = root.TotalCostYen }], lines)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Statement_rejects_unconfirmed_unknown_result_and_mismatched_totals()
    {
        var root = StatementNew(UpperLimitManagementResult.Result2);
        var lines = Lines(root, root.Result);
        UpperLimitManagementStatement[] invalid =
        [
            root with { IsConfirmed = false },
            root with { ReceivedAt = DateTimeOffset.MinValue },
            root with { ConfirmedAt = null },
            root with { ConfirmedAt = DateTimeOffset.MinValue },
            root with { OriginalDocumentReference = null },
            root with { CertificateMonthlyCostCap = new EnteredYen(false, null) },
            root with { UpperLimitManagementApplicability = UpperLimitManagementApplicability.Unknown },
            root with { CertificateManagingOfficeNumber = "different-office" },
            root with { Result = (UpperLimitManagementResult)999 },
            root with { TotalCostYen = Entered(root.TotalCostYen.ValueYen!.Value + 1) },
            root with { TotalPreManagementBurdenYen = Entered(root.TotalPreManagementBurdenYen.ValueYen!.Value + 1) },
            root with { TotalManagedBurdenYen = Entered(root.TotalManagedBurdenYen.ValueYen!.Value + 1) },
            root with { TotalManagedBurdenYen = new EnteredYen(false, null) },
        ];
        invalid.Should().AllSatisfy(statement =>
            FluentActions.Invoking(() => UpperLimitManagementStatementPolicy.ValidateHistory([statement], lines))
                .Should().Throw<InvalidOperationException>());
    }

    [Fact]
    public void Statement_rejects_invalid_lines_and_orphans()
    {
        var root = StatementNew(UpperLimitManagementResult.Result2);
        var lines = Lines(root, root.Result);
        var orphan = lines[0] with { Id = Guid.NewGuid(), StatementId = Guid.NewGuid() };
        IReadOnlyCollection<UpperLimitManagementStatementLine>[] invalid =
        [
            [],
            [lines[0], lines[1] with { Id = lines[0].Id }],
            [lines[0], lines[1] with { LineNumber = lines[0].LineNumber }],
            [lines[0], lines[1] with { LineNumber = 3 }],
            [lines[0], lines[1] with { OfficeNumber = lines[0].OfficeNumber }],
            [lines[0] with { ManagedBurdenYen = Entered(lines[0].PreManagementBurdenYen.ValueYen!.Value + 1) }, lines[1]],
            [.. lines, orphan],
        ];
        invalid.Should().AllSatisfy(candidate =>
            FluentActions.Invoking(() => UpperLimitManagementStatementPolicy.ValidateHistory([root], candidate))
                .Should().Throw<InvalidOperationException>());
    }

    [Fact]
    public void Statement_rejects_each_unentered_line_amount_before_summing()
    {
        var root = StatementNew(UpperLimitManagementResult.Result2);
        var lines = Lines(root, root.Result);
        UpperLimitManagementStatementLine[] invalidFirstLines =
        [
            lines[0] with { TotalCostYen = new EnteredYen(false, null) },
            lines[0] with { PreManagementBurdenYen = new EnteredYen(false, null) },
            lines[0] with { ManagedBurdenYen = new EnteredYen(false, null) },
        ];

        invalidFirstLines.Should().AllSatisfy(invalidLine =>
            FluentActions.Invoking(() => UpperLimitManagementStatementPolicy.ValidateHistory(
                    [root], [invalidLine, lines[1]]))
                .Should().Throw<InvalidOperationException>()
                .WithMessage("*入力済み*"));
    }

    [Theory]
    [InlineData(UpperLimitManagementResult.Result1)]
    [InlineData(UpperLimitManagementResult.Result2)]
    [InlineData(UpperLimitManagementResult.Result3)]
    public void Statement_rejects_broken_result_relationship(UpperLimitManagementResult result)
    {
        var root = StatementNew(result);
        var lines = Lines(root, result);
        lines[0] = lines[0] with
        {
            ManagedBurdenYen = Entered(lines[0].ManagedBurdenYen.ValueYen!.Value - 1),
        };
        var changedTotal = lines.Sum(line => line.ManagedBurdenYen.ValueYen!.Value);
        root = root with { TotalManagedBurdenYen = Entered(changedTotal) };

        FluentActions.Invoking(() => UpperLimitManagementStatementPolicy.ValidateHistory([root], lines))
            .Should().Throw<InvalidOperationException>();
    }

    public static TheoryData<string, IReadOnlyCollection<UpperLimitManagementStatement>> InvalidStatementHistories()
    {
        var root = StatementNew(UpperLimitManagementResult.Result2);
        var correction = StatementCorrect(root);
        return new()
        {
            { "missing revision", new[] { root, correction with { Revision = 3 } } },
            { "branch", new[] { root, correction, StatementCorrect(root) } },
            { "duplicate id", new[] { root, correction with { Id = root.Id } } },
            { "root mismatch", new[] { root, correction with { RootId = Guid.NewGuid() } } },
            { "expected head", new[] { root, correction with { ExpectedHeadId = Guid.NewGuid() } } },
            { "month changed", new[] { root, correction with { ServiceMonth = new ServiceMonth(2026, 8) } } },
            { "recipient changed", new[] { root, correction with { RecipientId = Guid.NewGuid() } } },
            { "certificate changed", new[] { root, correction with { CertificateId = Guid.NewGuid() } } },
            { "office changed", new[] { root, correction with { ManagingOfficeId = Guid.NewGuid() } } },
            { "new after root", new[] { root, correction with { Kind = RecordKind.New } } },
            { "unknown kind", new[] { root, correction with { Kind = (RecordKind)999 } } },
        };
    }

    [Theory]
    [MemberData(nameof(InvalidStatementHistories))]
    public void Statement_rejects_invalid_history(string _, IReadOnlyCollection<UpperLimitManagementStatement> history)
    {
        var lines = history.Where(x => x.Kind != RecordKind.Cancel).SelectMany(x => Lines(x, x.Result)).ToArray();
        FluentActions.Invoking(() => UpperLimitManagementStatementPolicy.ValidateHistory(history, lines))
            .Should().Throw<InvalidOperationException>();
    }

    private static CertificateClaimEvidence CertificateNew() => new()
    {
        Id = CertificateRootId,
        RootId = CertificateRootId,
        Revision = 1,
        Kind = RecordKind.New,
        CertificateId = CertificateId,
        Validity = new DateRange(new(2026, 4, 1), new(2027, 3, 31)),
        MonthlyCostCap = new EnteredYen(true, 1_000),
        UpperLimitManagementApplicability = UpperLimitManagementApplicability.Applicable,
        UpperLimitManagementOfficeNumber = "managing-office",
        Article31Status = Article31SpecialBurdenStatus.Applicable,
        Article31AmountYen = new EnteredYen(true, 500),
        Article31EffectivePeriod = new DateRange(new(2026, 4, 1), new(2027, 3, 31)),
        OriginalDocumentReference = "certificate-original",
        ConfirmedAt = DateTimeOffset.UnixEpoch,
        ConfirmedBy = "tester",
        ConfirmationReason = "original checked",
        CreatedAt = DateTimeOffset.UnixEpoch,
        CreatedBy = "tester",
        ConcurrencyToken = Guid.NewGuid(),
    };

    private static CertificateClaimEvidence CertificateCorrect(CertificateClaimEvidence head)
    {
        var values = head.Kind == RecordKind.Cancel ? CertificateNew() : head;
        return values with
        {
            Id = Guid.NewGuid(),
            RootId = head.RootId,
            Revision = head.Revision + 1,
            Kind = RecordKind.Correct,
            ExpectedHeadId = head.Id,
            CreatedAt = head.CreatedAt.AddMinutes(1),
            ConcurrencyToken = Guid.NewGuid(),
        };
    }

    private static CertificateClaimEvidence CertificateCancel(CertificateClaimEvidence head) => head with
    {
        Id = Guid.NewGuid(),
        Revision = head.Revision + 1,
        Kind = RecordKind.Cancel,
        ExpectedHeadId = head.Id,
        MonthlyCostCap = new EnteredYen(false, null),
        UpperLimitManagementApplicability = UpperLimitManagementApplicability.Unknown,
        UpperLimitManagementOfficeNumber = null,
        Article31Status = Article31SpecialBurdenStatus.Unknown,
        Article31AmountYen = new EnteredYen(false, null),
        Article31EffectivePeriod = null,
        OriginalDocumentReference = null,
        ConfirmedAt = null,
        ConfirmedBy = null,
        ConfirmationReason = null,
        CreatedAt = head.CreatedAt.AddMinutes(1),
        ConcurrencyToken = Guid.NewGuid(),
    };

    private static UpperLimitManagementStatement StatementNew(UpperLimitManagementResult result)
    {
        var (pre, managed) = result switch
        {
            UpperLimitManagementResult.Result1 => (1_500, 1_000),
            UpperLimitManagementResult.Result2 => (900, 900),
            _ => (1_500, 1_000),
        };
        return new()
        {
            Id = StatementRootId,
            RootId = StatementRootId,
            Revision = 1,
            Kind = RecordKind.New,
            ServiceMonth = new ServiceMonth(2026, 7),
            RecipientId = RecipientId,
            CertificateId = CertificateId,
            ManagingOfficeId = ManagingOfficeId,
            MunicipalityNumber = "municipality",
            CertificateNumber = "certificate",
            CertificateMonthlyCostCap = new EnteredYen(true, 1_000),
            UpperLimitManagementApplicability = UpperLimitManagementApplicability.Applicable,
            CertificateManagingOfficeNumber = "managing-office",
            ManagingOfficeNumber = "managing-office",
            ManagingOfficeName = "managing-name",
            OriginalCreationKind = "original-kind",
            ReceivedAt = DateTimeOffset.UnixEpoch,
            OriginalDocumentReference = "statement-original",
            IsConfirmed = true,
            ConfirmedAt = DateTimeOffset.UnixEpoch,
            ConfirmedBy = "tester",
            ConfirmationReason = "original checked",
            Result = result,
            TotalCostYen = Entered(15_000),
            TotalPreManagementBurdenYen = Entered(pre),
            TotalManagedBurdenYen = Entered(managed),
            CreatedAt = DateTimeOffset.UnixEpoch,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };
    }

    private static UpperLimitManagementStatement CorrectBase(UpperLimitManagementStatement head, RecordKind kind) => head with
    {
        Id = Guid.NewGuid(),
        Revision = head.Revision + 1,
        Kind = kind,
        ExpectedHeadId = head.Id,
        CreatedAt = head.CreatedAt.AddMinutes(1),
        ConcurrencyToken = Guid.NewGuid(),
    };

    private static UpperLimitManagementStatement StatementCorrect(UpperLimitManagementStatement head)
    {
        var values = head.Kind == RecordKind.Cancel
            ? StatementNew(UpperLimitManagementResult.Result2)
            : head;
        return values with
        {
            Id = Guid.NewGuid(),
            RootId = head.RootId,
            Revision = head.Revision + 1,
            Kind = RecordKind.Correct,
            ExpectedHeadId = head.Id,
            CreatedAt = head.CreatedAt.AddMinutes(1),
            ConcurrencyToken = Guid.NewGuid(),
        };
    }

    private static UpperLimitManagementStatement StatementCancel(UpperLimitManagementStatement head)
        => CorrectBase(head, RecordKind.Cancel) with
        {
            MunicipalityNumber = "",
            CertificateNumber = "",
            CertificateMonthlyCostCap = new EnteredYen(false, null),
            UpperLimitManagementApplicability = UpperLimitManagementApplicability.Unknown,
            CertificateManagingOfficeNumber = "",
            ManagingOfficeNumber = "",
            ManagingOfficeName = "",
            OriginalCreationKind = "",
            ReceivedAt = null,
            OriginalDocumentReference = null,
            IsConfirmed = false,
            ConfirmedAt = null,
            ConfirmedBy = null,
            ConfirmationReason = null,
            Result = (UpperLimitManagementResult)0,
            TotalCostYen = new EnteredYen(false, null),
            TotalPreManagementBurdenYen = new EnteredYen(false, null),
            TotalManagedBurdenYen = new EnteredYen(false, null),
        };

    private static UpperLimitManagementStatementLine[] Lines(
        UpperLimitManagementStatement statement, UpperLimitManagementResult result)
    {
        var amounts = result switch
        {
            UpperLimitManagementResult.Result1 => new[] { (1_000, 1_000), (500, 0) },
            UpperLimitManagementResult.Result2 => new[] { (500, 500), (400, 400) },
            _ => new[] { (1_000, 700), (500, 300) },
        };
        return amounts.Select((amount, index) => new UpperLimitManagementStatementLine
        {
            Id = Guid.NewGuid(),
            StatementId = statement.Id,
            LineNumber = index + 1,
            OfficeNumber = index == 0 ? "managing-office" : "other-office",
            OfficeName = $"office-{index + 1}",
            TotalCostYen = Entered(index == 0 ? 10_000 : 5_000),
            PreManagementBurdenYen = Entered(amount.Item1),
            ManagedBurdenYen = Entered(amount.Item2),
            CreatedAt = statement.CreatedAt,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        }).ToArray();
    }

    private static EnteredYen Entered(int value) => new(true, value);
}
