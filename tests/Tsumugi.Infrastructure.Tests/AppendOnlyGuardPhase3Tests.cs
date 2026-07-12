using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Persistence;

namespace Tsumugi.Infrastructure.Tests;

public sealed class AppendOnlyGuardPhase3Tests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;

    public AppendOnlyGuardPhase3Tests(SqliteFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData(typeof(ClaimBatch))]
    [InlineData(typeof(ClaimDetail))]
    [InlineData(typeof(ClaimInput))]
    [InlineData(typeof(IntensiveSupportEpisode))]
    [InlineData(typeof(AverageWageAnnualEvidence))]
    [InlineData(typeof(OfficeClaimProfile))]
    [InlineData(typeof(CertificateClaimEvidence))]
    [InlineData(typeof(UpperLimitManagementStatement))]
    [InlineData(typeof(UpperLimitManagementStatementLine))]
    public void Append_only_types_include_phase3_claim_entities(Type entityType)
    {
        AppendOnlyGuard.GetAppendOnlyTypesForTests().Should().Contain(entityType);
    }

    [Fact]
    public async Task Modifying_claim_batch_through_db_context_save_changes_throws()
    {
        await using var context = _fixture.NewContext();
        var batch = NewBatch();
        context.Set<ClaimBatch>().Add(batch);
        await context.SaveChangesAsync();

        context.Entry(batch).Property(nameof(ClaimBatch.TotalUnits)).CurrentValue = 11;
        context.Entry(batch).Property(nameof(ClaimBatch.TotalUnits)).IsModified = true;

        Func<Task> act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<AppendOnlyViolationException>()
            .Where(exception => exception.EntityName == nameof(ClaimBatch));
    }

    [Fact]
    public async Task Deleting_claim_batch_through_db_context_save_changes_throws()
    {
        await using var context = _fixture.NewContext();
        var batch = NewBatch();
        context.Set<ClaimBatch>().Add(batch);
        await context.SaveChangesAsync();

        context.Set<ClaimBatch>().Remove(batch);

        Func<Task> act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<AppendOnlyViolationException>()
            .Where(exception => exception.EntityName == nameof(ClaimBatch));
    }

    [Fact]
    public async Task Modifying_claim_detail_through_db_context_save_changes_throws()
    {
        await using var context = _fixture.NewContext();
        var batch = NewBatch();
        var detail = NewDetail(batch.Id);
        context.Set<ClaimBatch>().Add(batch);
        context.Set<ClaimDetail>().Add(detail);
        await context.SaveChangesAsync();

        context.Entry(detail).Property(nameof(ClaimDetail.TotalUnits)).CurrentValue = 11;
        context.Entry(detail).Property(nameof(ClaimDetail.TotalUnits)).IsModified = true;

        Func<Task> act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<AppendOnlyViolationException>()
            .Where(exception => exception.EntityName == nameof(ClaimDetail));
    }

    [Fact]
    public async Task Deleting_claim_detail_through_db_context_save_changes_throws()
    {
        await using var context = _fixture.NewContext();
        var batch = NewBatch();
        var detail = NewDetail(batch.Id);
        context.Set<ClaimBatch>().Add(batch);
        context.Set<ClaimDetail>().Add(detail);
        await context.SaveChangesAsync();

        context.Set<ClaimDetail>().Remove(detail);

        Func<Task> act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<AppendOnlyViolationException>()
            .Where(exception => exception.EntityName == nameof(ClaimDetail));
    }

    [Theory]
    [MemberData(nameof(ClaimInputHistoryGuardCases))]
    public async Task Mutating_claim_input_history_through_db_context_save_changes_throws(
        Type entityType,
        EntityState state)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        await using var context = _fixture.NewContext();
        var entity = NewClaimInputHistory(entityType);
        context.Attach(entity);
        context.Entry(entity).State = state;

        Func<Task> act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<AppendOnlyViolationException>()
            .Where(exception => exception.EntityName == entityType.Name && exception.State == state);
    }

    public static IEnumerable<object[]> ClaimInputHistoryGuardCases()
    {
        var entityTypes = new[]
        {
            typeof(ClaimInput),
            typeof(IntensiveSupportEpisode),
            typeof(AverageWageAnnualEvidence),
            typeof(OfficeClaimProfile),
            typeof(CertificateClaimEvidence),
            typeof(UpperLimitManagementStatement),
            typeof(UpperLimitManagementStatementLine),
        };

        foreach (var entityType in entityTypes)
        {
            yield return [entityType, EntityState.Modified];
            yield return [entityType, EntityState.Deleted];
        }
    }

    private static ClaimBatch NewBatch() => ClaimBatch.NewRecord(
        Guid.NewGuid(),
        Guid.NewGuid(),
        new ServiceMonth(2026, 7),
        totalUnits: 10,
        totalCostYen: 1_000,
        totalBenefitYen: 900,
        totalBurdenYen: 100,
        claimMasterVersion: "claim-master-v1",
        csvSpecificationVersion: "csv-v1",
        reportSpecificationVersion: "report-v1",
        snapshotApplicationVersion: "snapshot-app-v1",
        operationApplicationVersion: "operation-app-v1",
        finalizationOperationId: Guid.NewGuid(),
        operationPayloadSchemaVersion: ClaimBatch.CurrentOperationPayloadSchemaVersion,
        operationPayloadSha256: new string('a', 64),
        createdBy: "tester",
        createdAt: DateTimeOffset.UtcNow);

    private static ClaimDetail NewDetail(Guid claimBatchId) => ClaimDetail.Create(
        Guid.NewGuid(),
        claimBatchId,
        Guid.NewGuid(),
        snapshotSchemaVersion: "snapshot-v1",
        claimMasterVersion: "claim-master-v1",
        csvSpecificationVersion: "csv-v1",
        reportSpecificationVersion: "report-v1",
        snapshotApplicationVersion: "snapshot-app-v1",
        inputSnapshotJson: "{}",
        calculationSnapshotJson: "{}",
        totalUnits: 10,
        totalCostYen: 1_000,
        benefitYen: 900,
        burdenYen: 100,
        createdBy: "tester",
        createdAt: DateTimeOffset.UtcNow);

    private static Entity NewClaimInputHistory(Type entityType) => entityType.Name switch
    {
        nameof(ClaimInput) => NewClaimInput(),
        nameof(IntensiveSupportEpisode) => NewIntensiveSupportEpisode(),
        nameof(AverageWageAnnualEvidence) => NewAverageWageAnnualEvidence(),
        nameof(OfficeClaimProfile) => NewOfficeClaimProfile(),
        nameof(CertificateClaimEvidence) => NewCertificateClaimEvidence(),
        nameof(UpperLimitManagementStatement) => NewUpperLimitManagementStatement(),
        nameof(UpperLimitManagementStatementLine) => NewUpperLimitManagementStatementLine(),
        _ => throw new ArgumentOutOfRangeException(nameof(entityType), entityType, null),
    };

    private static ClaimInput NewClaimInput()
    {
        var id = Guid.NewGuid();
        return new ClaimInput
        {
            Id = id,
            OfficeId = Guid.NewGuid(),
            RecipientId = Guid.NewGuid(),
            ServiceMonth = new ServiceMonth(2026, 7),
            RootId = id,
            Revision = 1,
            Kind = RecordKind.New,
            CreatedAt = DateTimeOffset.UnixEpoch,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };
    }

    private static IntensiveSupportEpisode NewIntensiveSupportEpisode()
    {
        var id = Guid.NewGuid();
        return new IntensiveSupportEpisode
        {
            Id = id,
            OfficeId = Guid.NewGuid(),
            RecipientId = Guid.NewGuid(),
            RootId = id,
            Revision = 1,
            Kind = RecordKind.New,
            StartDate = new DateOnly(2026, 7, 1),
            CreatedAt = DateTimeOffset.UnixEpoch,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };
    }

    private static AverageWageAnnualEvidence NewAverageWageAnnualEvidence()
    {
        var id = Guid.NewGuid();
        return new AverageWageAnnualEvidence
        {
            Id = id,
            OfficeId = Guid.NewGuid(),
            SourceFiscalYear = 2025,
            PeriodStart = new DateOnly(2025, 4, 1),
            PeriodEnd = new DateOnly(2026, 3, 31),
            RootId = id,
            Revision = 1,
            Kind = RecordKind.New,
            CreatedAt = DateTimeOffset.UnixEpoch,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };
    }

    private static OfficeClaimProfile NewOfficeClaimProfile()
    {
        var id = Guid.NewGuid();
        return new OfficeClaimProfile
        {
            Id = id,
            OfficeId = Guid.NewGuid(),
            EffectiveFrom = new DateOnly(2026, 4, 1),
            RootId = id,
            Revision = 1,
            Kind = RecordKind.New,
            CreatedAt = DateTimeOffset.UnixEpoch,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };
    }

    private static CertificateClaimEvidence NewCertificateClaimEvidence()
    {
        var id = Guid.NewGuid();
        return new CertificateClaimEvidence
        {
            Id = id,
            CertificateId = Guid.NewGuid(),
            Validity = new DateRange(new DateOnly(2026, 4, 1), null),
            RootId = id,
            Revision = 1,
            Kind = RecordKind.New,
            MonthlyCostCap = new EnteredYen(true, 37_200),
            UpperLimitManagementApplicability = UpperLimitManagementApplicability.Applicable,
            Article31Status = Article31SpecialBurdenStatus.NotApplicable,
            Article31AmountYen = new EnteredYen(false, null),
            Article31EffectivePeriod = null,
            CreatedAt = DateTimeOffset.UnixEpoch,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };
    }

    private static UpperLimitManagementStatement NewUpperLimitManagementStatement()
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
            CertificateMonthlyCostCap = new EnteredYen(true, 37_200),
            UpperLimitManagementApplicability = UpperLimitManagementApplicability.Applicable,
            CertificateManagingOfficeNumber = "certificate-office",
            ManagingOfficeNumber = "managing-office",
            ManagingOfficeName = "管理事業所",
            OriginalCreationKind = "original",
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

    private static UpperLimitManagementStatementLine NewUpperLimitManagementStatementLine() => new()
    {
        Id = Guid.NewGuid(),
        StatementId = Guid.NewGuid(),
        LineNumber = 1,
        OfficeNumber = "line-office",
        OfficeName = "明細事業所",
        TotalCostYen = new EnteredYen(true, 10_000),
        PreManagementBurdenYen = new EnteredYen(true, 1_000),
        ManagedBurdenYen = new EnteredYen(true, 1_000),
        CreatedAt = DateTimeOffset.UnixEpoch,
        CreatedBy = "tester",
        ConcurrencyToken = Guid.NewGuid(),
    };
}
