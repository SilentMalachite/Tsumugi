using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Tests.Persistence;

public sealed class ClaimInputRepositoryTests
{
    [Fact]
    public void Statement_aggregate_orders_lines_and_defensively_copies_source()
    {
        var statement = ClaimRows.Statement();
        var lines = new List<UpperLimitManagementStatementLine>
        {
            ClaimRows.Line(statement.Id, 2),
            ClaimRows.Line(statement.Id, 1),
        };

        var aggregate = new UpperLimitManagementStatementAggregate(statement, lines);

        lines.Clear();

        aggregate.Header.Should().BeSameAs(statement);
        aggregate.Lines.Select(line => line.LineNumber).Should().Equal(1, 2);
    }

    private static class ClaimRows
    {
        public static UpperLimitManagementStatement Statement()
        {
            var id = Guid.NewGuid();
            return new UpperLimitManagementStatement
            {
                Id = id,
                RootId = id,
                Revision = 1,
                Kind = RecordKind.New,
                ServiceMonth = new ServiceMonth(2026, 7),
                RecipientId = Guid.NewGuid(),
                CertificateId = Guid.NewGuid(),
                ManagingOfficeId = Guid.NewGuid(),
                MunicipalityNumber = "municipality",
                CertificateNumber = "certificate",
                CertificateMonthlyCostCap = new EnteredYen(true, 1_000),
                UpperLimitManagementApplicability = UpperLimitManagementApplicability.Applicable,
                CertificateManagingOfficeNumber = "managing-office",
                ManagingOfficeNumber = "managing-office",
                ManagingOfficeName = "managing-name",
                OriginalCreationKind = "original-kind",
                IsConfirmed = true,
                Result = UpperLimitManagementResult.Result1,
                TotalCostYen = new EnteredYen(true, 10_000),
                TotalPreManagementBurdenYen = new EnteredYen(true, 1_000),
                TotalManagedBurdenYen = new EnteredYen(true, 1_000),
                CreatedAt = DateTimeOffset.UnixEpoch,
                CreatedBy = "tester",
                ConcurrencyToken = Guid.NewGuid(),
            };
        }

        public static UpperLimitManagementStatementLine Line(Guid statementId, int lineNumber)
            => new()
            {
                Id = Guid.NewGuid(),
                StatementId = statementId,
                LineNumber = lineNumber,
                OfficeNumber = $"office-{lineNumber}",
                OfficeName = $"office-{lineNumber}",
                TotalCostYen = new EnteredYen(true, 1_000),
                PreManagementBurdenYen = new EnteredYen(true, 100),
                ManagedBurdenYen = new EnteredYen(true, 100),
                CreatedAt = DateTimeOffset.UnixEpoch,
                CreatedBy = "tester",
                ConcurrencyToken = Guid.NewGuid(),
            };
    }
}
