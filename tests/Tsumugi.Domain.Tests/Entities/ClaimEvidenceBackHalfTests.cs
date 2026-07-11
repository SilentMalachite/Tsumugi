using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Tests.Entities;

public sealed class ClaimEvidenceTestsBackHalf
{
    [Fact]
    public void Certificate_evidence_preserves_formal_zero_and_article31_evidence()
    {
        var evidence = new CertificateClaimEvidence
        {
            Id = Guid.NewGuid(),
            RootId = Guid.NewGuid(),
            Revision = 1,
            Kind = RecordKind.New,
            CertificateId = Guid.NewGuid(),
            Validity = new DateRange(new(2026, 4, 1), new(2027, 3, 31)),
            MonthlyCostCap = new EnteredYen(true, 0),
            UpperLimitManagementApplicability = UpperLimitManagementApplicability.Applicable,
            UpperLimitManagementOfficeNumber = "office-number",
            Article31Status = Article31SpecialBurdenStatus.Applicable,
            Article31AmountYen = new EnteredYen(true, 0),
            Article31EffectivePeriod = new DateRange(new(2026, 4, 1), new(2027, 3, 31)),
            OriginalDocumentReference = "certificate-original",
            ConfirmedAt = DateTimeOffset.UnixEpoch,
            ConfirmedBy = "tester",
            ConfirmationReason = "original checked",
            CreatedAt = DateTimeOffset.UnixEpoch,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };

        evidence.MonthlyCostCap.ValueYen.Should().Be(0);
        evidence.Article31AmountYen.ValueYen.Should().Be(0);
    }

    [Fact]
    public void Statement_line_preserves_all_three_formal_amounts()
    {
        var line = new UpperLimitManagementStatementLine
        {
            Id = Guid.NewGuid(),
            StatementId = Guid.NewGuid(),
            LineNumber = 1,
            OfficeNumber = "office-number",
            OfficeName = "office-name",
            TotalCostYen = new EnteredYen(true, 10_000),
            PreManagementBurdenYen = new EnteredYen(true, 1_000),
            ManagedBurdenYen = new EnteredYen(true, 0),
            CreatedAt = DateTimeOffset.UnixEpoch,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };

        line.TotalCostYen.ValueYen.Should().Be(10_000);
        line.PreManagementBurdenYen.ValueYen.Should().Be(1_000);
        line.ManagedBurdenYen.ValueYen.Should().Be(0);
    }
}
