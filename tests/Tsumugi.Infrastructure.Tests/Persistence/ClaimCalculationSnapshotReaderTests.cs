using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Persistence;

namespace Tsumugi.Infrastructure.Tests.Persistence;

public sealed class ClaimCalculationSnapshotReaderTests : IClassFixture<SqliteFixture>
{
    private static readonly ClaimMasterVersion MasterVersion = new("claim-master-test");
    private readonly SqliteFixture _fixture;

    public ClaimCalculationSnapshotReaderTests(SqliteFixture fixture)
    {
        _fixture = fixture;
        using var migration = _fixture.NewContext(); // マイグレーション適用のみ（破棄後にfactory経由で使う）
    }

    [Fact]
    public async Task Reads_effective_inputs_and_billed_days_in_one_snapshot()
    {
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var certificateId = Guid.NewGuid();
        var month = new ServiceMonth(2026, 7);
        var factory = new TestDbContextFactory(_fixture.DbPath);
        var certValidity = new DateRange(new DateOnly(2026, 4, 1), null);

        var claimInputRootId = Guid.NewGuid();
        var claimInputNew = new ClaimInput
        {
            Id = claimInputRootId,
            OfficeId = officeId,
            RecipientId = recipientId,
            ServiceMonth = month,
            RootId = claimInputRootId,
            Revision = 1,
            Kind = RecordKind.New,
            ExpectedHeadId = null,
            UpperLimitManagementResult = UpperLimitManagementResult.Result1,
            UpperLimitManagedAmountYen = 1_000,
            MunicipalSubsidyAmountYen = null,
            ExceptionalUsageStartMonth = null,
            ExceptionalUsageEndMonth = null,
            ExceptionalUsageDays = null,
            StandardUsageDayTotal = 20,
            CreatedAt = DateTimeOffset.UnixEpoch,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };
        var claimInputCorrected = claimInputNew with
        {
            Id = Guid.NewGuid(),
            Revision = 2,
            Kind = RecordKind.Correct,
            ExpectedHeadId = claimInputNew.Id,
            StandardUsageDayTotal = 21,
            CreatedAt = DateTimeOffset.UnixEpoch.AddMinutes(1),
            ConcurrencyToken = Guid.NewGuid(),
        };

        var profileRootId = Guid.NewGuid();
        var profile = new OfficeClaimProfile
        {
            Id = profileRootId,
            OfficeId = officeId,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            EffectiveTo = null,
            RootId = profileRootId,
            Revision = 1,
            Kind = RecordKind.New,
            ExpectedHeadId = null,
            MasterVersion = MasterVersion,
            ReformStatus = R8ReformStatus.ReformTarget,
            AverageWageBandOption = new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 1),
            DesignationDate = null,
            SupportStartDate = null,
            EarlierRegisteredBandOption = null,
            EarlierRegistrationMonth = null,
            LaterRegisteredBandOption = null,
            LaterRegistrationMonth = null,
            ReformComparisonEvidenceDocumentId = null,
            FiledTransitionPeriod = null,
            FiledTransitionEvidenceDocumentId = null,
            EvidenceDocumentId = "profile-evidence",
            ConfirmedAt = DateTimeOffset.UnixEpoch,
            ConfirmedBy = "tester",
            ConfirmationReason = "checked",
            CreatedAt = DateTimeOffset.UnixEpoch,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };

        var evidenceRootId = Guid.NewGuid();
        var evidence = new CertificateClaimEvidence
        {
            Id = evidenceRootId,
            CertificateId = certificateId,
            Validity = certValidity,
            RootId = evidenceRootId,
            Revision = 1,
            Kind = RecordKind.New,
            ExpectedHeadId = null,
            MonthlyCostCap = new EnteredYen(true, 0),
            UpperLimitManagementApplicability = UpperLimitManagementApplicability.Applicable,
            UpperLimitManagementOfficeNumber = "1234567890",
            Article31Status = Article31SpecialBurdenStatus.Applicable,
            Article31AmountYen = new EnteredYen(true, 0),
            Article31EffectivePeriod = new DateRange(
                new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)),
            OriginalDocumentReference = "certificate-original",
            ConfirmedAt = DateTimeOffset.UnixEpoch,
            ConfirmedBy = "tester",
            ConfirmationReason = "checked",
            CreatedAt = DateTimeOffset.UnixEpoch,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };

        var annualRootId = Guid.NewGuid();
        var annual = new AverageWageAnnualEvidence
        {
            Id = annualRootId,
            OfficeId = officeId,
            SourceFiscalYear = 2026,
            PeriodStart = new DateOnly(2026, 4, 1),
            PeriodEnd = new DateOnly(2027, 3, 31),
            RootId = annualRootId,
            Revision = 1,
            Kind = RecordKind.New,
            ExpectedHeadId = null,
            AnnualWagePaidYen = 1_200_000,
            AnnualExtendedUsers = 240,
            AnnualOpeningDays = 250,
            Completeness = FiscalYearCompleteness.Complete,
            EvidenceDocumentId = "annual-evidence",
            DailyEvidenceReference = "daily-evidence",
            MonthlyEvidenceReference = "monthly-evidence",
            ConfirmedAt = DateTimeOffset.UnixEpoch,
            ConfirmedBy = "tester",
            ConfirmationReason = "checked",
            CreatedAt = DateTimeOffset.UnixEpoch,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };

        var t0 = DateTimeOffset.UnixEpoch;
        var correctedOriginId = Guid.NewGuid();
        var dailyRecords = new[]
        {
            DailyRecord.NewRecord(
                Guid.NewGuid(), recipientId, new DateOnly(2026, 7, 1),
                Attendance.Present, TransportKind.None, false, null, "tester", t0),
            DailyRecord.NewRecord(
                Guid.NewGuid(), recipientId, new DateOnly(2026, 7, 2),
                Attendance.Present, TransportKind.None, false, null, "tester", t0),
            DailyRecord.NewRecord(
                Guid.NewGuid(), recipientId, new DateOnly(2026, 7, 3),
                Attendance.Present, TransportKind.None, false, null, "tester", t0),
            DailyRecord.NewRecord(
                correctedOriginId, recipientId, new DateOnly(2026, 7, 4),
                Attendance.Present, TransportKind.None, false, null, "tester", t0),
            DailyRecord.Correction(
                Guid.NewGuid(), recipientId, new DateOnly(2026, 7, 4), correctedOriginId,
                Attendance.Absent, TransportKind.None, false, null, "tester", t0.AddMinutes(1)),
            DailyRecord.NewRecord(
                Guid.NewGuid(), recipientId, new DateOnly(2026, 7, 5),
                Attendance.Absent, TransportKind.None, false, null, "tester", t0),
            DailyRecord.NewRecord(
                Guid.NewGuid(), recipientId, new DateOnly(2026, 6, 30),
                Attendance.Present, TransportKind.None, false, null, "tester", t0),
        };

        await using (var seed = factory.CreateDbContext())
        {
            seed.AddRange(
                Rows.Office(officeId),
                Rows.Recipient(recipientId),
                Rows.Certificate(certificateId, recipientId, certValidity));
            seed.AddRange(claimInputNew, claimInputCorrected);
            seed.Add(profile);
            seed.Add(evidence);
            seed.Add(annual);
            seed.AddRange(dailyRecords);
            await seed.SaveChangesAsync();
        }

        var reader = new ClaimCalculationSnapshotReader(
            factory, new FakeOfficeClaimProfilePolicyProvider(MasterVersion));

        var snapshot = await reader.ReadAsync(officeId, month, default);

        snapshot.EffectiveClaimInputs.Should().ContainSingle();
        snapshot.EffectiveClaimInputs[0].Id.Should().Be(claimInputCorrected.Id);
        snapshot.EffectiveClaimInputs[0].Kind.Should().Be(RecordKind.Correct);
        snapshot.EffectiveClaimInputs[0].StandardUsageDayTotal.Should().Be(21);

        snapshot.BilledDaysByRecipient.Should().ContainKey(recipientId);
        snapshot.BilledDaysByRecipient[recipientId].Should().Be(3);

        snapshot.Profile.Should().NotBeNull();
        snapshot.Profile!.Id.Should().Be(profile.Id);

        snapshot.EffectiveCertificateEvidences.Should().ContainSingle()
            .Which.Id.Should().Be(evidence.Id);
        snapshot.EffectiveAverageWageEvidences.Should().ContainSingle()
            .Which.Id.Should().Be(annual.Id);
    }

    [Fact]
    public async Task Returns_empty_snapshot_for_month_without_records()
    {
        var officeId = Guid.NewGuid();
        var month = new ServiceMonth(2099, 1);
        var factory = new TestDbContextFactory(_fixture.DbPath);
        var reader = new ClaimCalculationSnapshotReader(
            factory, new FakeOfficeClaimProfilePolicyProvider(MasterVersion));

        var snapshot = await reader.ReadAsync(officeId, month, default);

        snapshot.Profile.Should().BeNull();
        snapshot.EffectiveClaimInputs.Should().BeEmpty();
        snapshot.EffectiveCertificateEvidences.Should().BeEmpty();
        snapshot.EffectiveAverageWageEvidences.Should().BeEmpty();
        snapshot.BilledDaysByRecipient.Should().BeEmpty();
    }

    private static class Rows
    {
        public static Office Office(Guid id) => Tsumugi.Domain.Entities.Office.Create(
            id,
            $"office-{id:N}"[..20],
            "スナップショット読取テスト事業所",
            ServiceCategory.TypeB,
            RegionGrade.None,
            "tester",
            DateTimeOffset.UnixEpoch,
            Guid.NewGuid());

        public static Recipient Recipient(Guid id) => Tsumugi.Domain.Entities.Recipient.Create(
            id,
            "読取テスト利用者",
            "ドクトリテストリヨウシャ",
            new DateOnly(1990, 1, 1),
            "tester",
            DateTimeOffset.UnixEpoch,
            Guid.NewGuid());

        public static Certificate Certificate(Guid id, Guid recipientId, DateRange validity) =>
            Tsumugi.Domain.Entities.Certificate.Create(
                id,
                recipientId,
                $"certificate-{id:N}"[..20],
                validity,
                23,
                37_200,
                "読取テスト自治体",
                "tester",
                DateTimeOffset.UnixEpoch,
                Guid.NewGuid());
    }

    private sealed class FakeOfficeClaimProfilePolicyProvider(ClaimMasterVersion version)
        : IOfficeClaimProfilePolicyProvider
    {
        private readonly OfficeClaimProfilePolicy _policy = new(
            version,
            [
                new AverageWageBandOptionVersionRule(
                    version,
                    new ServiceMonth(2024, 4),
                    null,
                    [new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 1)],
                    new Dictionary<R8ReformStatus, IReadOnlyCollection<AverageWageBandOption>>
                    {
                        [R8ReformStatus.ReformTarget] =
                            [new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 1)],
                    }),
            ],
            new DateOnly(2024, 4, 1),
            designation => designation.AddYears(3));

        public OfficeClaimProfilePolicy Resolve(ClaimMasterVersion masterVersion) =>
            masterVersion == version
                ? _policy
                : throw new ClaimMasterPolicyUnavailableException(
                    ClaimMasterPolicyUnavailableCode.Unavailable);
    }

    private sealed class TestDbContextFactory(string dbPath) : IDbContextFactory<TsumugiDbContext>
    {
        public TsumugiDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<TsumugiDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;
            return new TsumugiDbContext(options);
        }

        public Task<TsumugiDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
