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

        snapshot.RecipientIds.Should().ContainSingle().Which.Should().Be(recipientId);

        snapshot.EffectiveClaimInputs.Should().ContainSingle();
        snapshot.EffectiveClaimInputs[0].Id.Should().Be(claimInputCorrected.Id);
        snapshot.EffectiveClaimInputs[0].Kind.Should().Be(RecordKind.Correct);
        snapshot.EffectiveClaimInputs[0].StandardUsageDayTotal.Should().Be(21);

        snapshot.BilledDaysByRecipient.Should().ContainKey(recipientId);
        snapshot.BilledDaysByRecipient[recipientId].Should().Be(3);

        snapshot.Profile.Should().NotBeNull();
        snapshot.Profile!.Id.Should().Be(profile.Id);

        snapshot.EffectiveCertificateCountByRecipient.Should().ContainKey(recipientId);
        snapshot.EffectiveCertificateCountByRecipient[recipientId].Should().Be(1);
        snapshot.EffectiveCertificateEvidenceByRecipient.Should().ContainKey(recipientId);
        snapshot.EffectiveCertificateEvidenceByRecipient[recipientId].Id.Should().Be(evidence.Id);
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

        snapshot.RecipientIds.Should().BeEmpty();
        snapshot.Profile.Should().BeNull();
        snapshot.EffectiveClaimInputs.Should().BeEmpty();
        snapshot.EffectiveCertificateEvidenceByRecipient.Should().BeEmpty();
        snapshot.EffectiveCertificateCountByRecipient.Should().BeEmpty();
        snapshot.EffectiveAverageWageEvidences.Should().BeEmpty();
        snapshot.BilledDaysByRecipient.Should().BeEmpty();
    }

    [Fact]
    public async Task Recipient_with_present_days_but_no_claim_input_is_visible_in_scope()
    {
        // Critical finding fix: 出席実績はあるがClaimInput未入力の利用者を、
        // 請求対象から黙って落とさず可視化する（サイレント過小請求の防止）。
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var month = new ServiceMonth(2026, 8);
        var factory = new TestDbContextFactory(_fixture.DbPath);
        var t0 = DateTimeOffset.UnixEpoch;

        var dailyRecords = new[]
        {
            DailyRecord.NewRecord(
                Guid.NewGuid(), recipientId, new DateOnly(2026, 8, 3),
                Attendance.Present, TransportKind.None, false, null, "tester", t0),
            DailyRecord.NewRecord(
                Guid.NewGuid(), recipientId, new DateOnly(2026, 8, 4),
                Attendance.Present, TransportKind.None, false, null, "tester", t0),
        };

        await using (var seed = factory.CreateDbContext())
        {
            seed.AddRange(Rows.Office(officeId), Rows.Recipient(recipientId));
            seed.AddRange(dailyRecords);
            await seed.SaveChangesAsync();
        }

        var reader = new ClaimCalculationSnapshotReader(
            factory, new FakeOfficeClaimProfilePolicyProvider(MasterVersion));

        var snapshot = await reader.ReadAsync(officeId, month, default);

        snapshot.RecipientIds.Should().Contain(recipientId);
        snapshot.BilledDaysByRecipient.Should().ContainKey(recipientId);
        snapshot.BilledDaysByRecipient[recipientId].Should().Be(2);
        snapshot.EffectiveClaimInputs.Should().NotContain(input => input.RecipientId == recipientId);
    }

    [Fact]
    public async Task Recipient_with_effective_contract_only_appears_with_zero_billed_days()
    {
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var month = new ServiceMonth(2026, 9);
        var factory = new TestDbContextFactory(_fixture.DbPath);
        var t0 = DateTimeOffset.UnixEpoch;

        // Contract に OfficeId はなくSqliteFixtureはテストclass内でDBファイルを共有するため、
        // 有効期間はこのテスト固有の月にだけ重なるよう厳密に区切る（無期限にすると他テストの月へ
        // 意図せず越境してしまう）。
        var contract = Contract.Create(
            Guid.NewGuid(), recipientId,
            new DateRange(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)),
            20, "tester", t0, Guid.NewGuid());

        await using (var seed = factory.CreateDbContext())
        {
            seed.AddRange(Rows.Office(officeId), Rows.Recipient(recipientId));
            seed.Add(contract);
            await seed.SaveChangesAsync();
        }

        var reader = new ClaimCalculationSnapshotReader(
            factory, new FakeOfficeClaimProfilePolicyProvider(MasterVersion));

        var snapshot = await reader.ReadAsync(officeId, month, default);

        snapshot.RecipientIds.Should().Contain(recipientId);
        snapshot.BilledDaysByRecipient.Should().ContainKey(recipientId);
        snapshot.BilledDaysByRecipient[recipientId].Should().Be(0);
        snapshot.EffectiveClaimInputs.Should().NotContain(input => input.RecipientId == recipientId);
        snapshot.EffectiveCertificateCountByRecipient.Should().ContainKey(recipientId);
        snapshot.EffectiveCertificateCountByRecipient[recipientId].Should().Be(0);
    }

    [Fact]
    public async Task Mid_month_certificate_transition_yields_count_two_and_no_evidence_entry()
    {
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        // test1と月が衝突するとContractがOfficeIdを持たないためRecipientIdsが越境する
        // （SqliteFixtureはテストclass内でDBファイルを共有）。他テストと重複しない月を使う。
        var month = new ServiceMonth(2027, 1);
        var factory = new TestDbContextFactory(_fixture.DbPath);
        var t0 = DateTimeOffset.UnixEpoch;

        var contract = Contract.Create(
            Guid.NewGuid(), recipientId,
            new DateRange(new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 31)),
            20, "tester", t0, Guid.NewGuid());

        var certificateAId = Guid.NewGuid();
        var certificateBId = Guid.NewGuid();
        var certificateA = Rows.Certificate(
            certificateAId, recipientId,
            new DateRange(new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 15)));
        var certificateB = Rows.Certificate(
            certificateBId, recipientId,
            new DateRange(new DateOnly(2027, 1, 16), null));

        var evidenceRootId = Guid.NewGuid();
        var evidenceForCertificateA = new CertificateClaimEvidence
        {
            Id = evidenceRootId,
            CertificateId = certificateAId,
            Validity = certificateA.Validity,
            RootId = evidenceRootId,
            Revision = 1,
            Kind = RecordKind.New,
            ExpectedHeadId = null,
            MonthlyCostCap = new EnteredYen(true, 0),
            UpperLimitManagementApplicability = UpperLimitManagementApplicability.Applicable,
            UpperLimitManagementOfficeNumber = "1234567890",
            Article31Status = Article31SpecialBurdenStatus.Applicable,
            Article31AmountYen = new EnteredYen(true, 0),
            Article31EffectivePeriod = certificateA.Validity,
            OriginalDocumentReference = "certificate-a-original",
            ConfirmedAt = t0,
            ConfirmedBy = "tester",
            ConfirmationReason = "checked",
            CreatedAt = t0,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };

        await using (var seed = factory.CreateDbContext())
        {
            seed.AddRange(
                Rows.Office(officeId), Rows.Recipient(recipientId),
                certificateA, certificateB);
            seed.Add(contract);
            seed.Add(evidenceForCertificateA);
            await seed.SaveChangesAsync();
        }

        var reader = new ClaimCalculationSnapshotReader(
            factory, new FakeOfficeClaimProfilePolicyProvider(MasterVersion));

        var snapshot = await reader.ReadAsync(officeId, month, default);

        snapshot.RecipientIds.Should().Contain(recipientId);
        snapshot.EffectiveCertificateCountByRecipient.Should().ContainKey(recipientId);
        snapshot.EffectiveCertificateCountByRecipient[recipientId].Should().Be(2);
        snapshot.EffectiveCertificateEvidenceByRecipient.Should().NotContainKey(recipientId);
    }

    [Fact]
    public async Task Correction_chain_reduces_to_latest_revision_for_profile_certificate_and_annual_evidence()
    {
        // Important finding #3 の解消: Profile / CertificateClaimEvidence / AverageWageAnnualEvidence の
        // 3種すべてでNew->Correctの縮約を検証する（従来はClaimInputのみ検証済みだった）。
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var certificateId = Guid.NewGuid();
        var month = new ServiceMonth(2026, 10);
        var factory = new TestDbContextFactory(_fixture.DbPath);
        var certValidity = new DateRange(new DateOnly(2026, 1, 1), null);
        var t0 = DateTimeOffset.UnixEpoch;

        // Contract はOfficeId非依存でこのテスト固有の月にだけ有効期間を区切る（他テストへ越境させない）。
        var contract = Contract.Create(
            Guid.NewGuid(), recipientId,
            new DateRange(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)),
            20, "tester", t0, Guid.NewGuid());

        var profileRootId = Guid.NewGuid();
        var profileNew = new OfficeClaimProfile
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
            EvidenceDocumentId = "profile-evidence-v1",
            ConfirmedAt = t0,
            ConfirmedBy = "tester",
            ConfirmationReason = "checked",
            CreatedAt = t0,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };
        var profileCorrected = profileNew with
        {
            Id = Guid.NewGuid(),
            Revision = 2,
            Kind = RecordKind.Correct,
            ExpectedHeadId = profileNew.Id,
            EvidenceDocumentId = "profile-evidence-v2",
            ConfirmedAt = t0.AddMinutes(1),
            ConcurrencyToken = Guid.NewGuid(),
        };

        var evidenceRootId = Guid.NewGuid();
        var evidenceNew = new CertificateClaimEvidence
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
            Article31EffectivePeriod = certValidity,
            OriginalDocumentReference = "certificate-original-v1",
            ConfirmedAt = t0,
            ConfirmedBy = "tester",
            ConfirmationReason = "checked",
            CreatedAt = t0,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };
        var evidenceCorrected = evidenceNew with
        {
            Id = Guid.NewGuid(),
            Revision = 2,
            Kind = RecordKind.Correct,
            ExpectedHeadId = evidenceNew.Id,
            OriginalDocumentReference = "certificate-original-v2",
            ConfirmedAt = t0.AddMinutes(1),
            ConcurrencyToken = Guid.NewGuid(),
        };

        var annualRootId = Guid.NewGuid();
        var annualNew = new AverageWageAnnualEvidence
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
            AnnualWagePaidYen = 1_000_000,
            AnnualExtendedUsers = 200,
            AnnualOpeningDays = 240,
            Completeness = FiscalYearCompleteness.Complete,
            EvidenceDocumentId = "annual-evidence-v1",
            DailyEvidenceReference = "daily-evidence-v1",
            MonthlyEvidenceReference = "monthly-evidence-v1",
            ConfirmedAt = t0,
            ConfirmedBy = "tester",
            ConfirmationReason = "checked",
            CreatedAt = t0,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };
        var annualCorrected = annualNew with
        {
            Id = Guid.NewGuid(),
            Revision = 2,
            Kind = RecordKind.Correct,
            ExpectedHeadId = annualNew.Id,
            AnnualWagePaidYen = 1_500_000,
            ConfirmedAt = t0.AddMinutes(1),
            ConcurrencyToken = Guid.NewGuid(),
        };

        await using (var seed = factory.CreateDbContext())
        {
            seed.AddRange(
                Rows.Office(officeId), Rows.Recipient(recipientId),
                Rows.Certificate(certificateId, recipientId, certValidity));
            seed.Add(contract);
            seed.AddRange(profileNew, profileCorrected);
            seed.AddRange(evidenceNew, evidenceCorrected);
            seed.AddRange(annualNew, annualCorrected);
            await seed.SaveChangesAsync();
        }

        var reader = new ClaimCalculationSnapshotReader(
            factory, new FakeOfficeClaimProfilePolicyProvider(MasterVersion));

        var snapshot = await reader.ReadAsync(officeId, month, default);

        snapshot.Profile.Should().NotBeNull();
        snapshot.Profile!.Revision.Should().Be(2);
        snapshot.Profile.Kind.Should().Be(RecordKind.Correct);
        snapshot.Profile.EvidenceDocumentId.Should().Be("profile-evidence-v2");

        snapshot.EffectiveCertificateEvidenceByRecipient.Should().ContainKey(recipientId);
        var effectiveEvidence = snapshot.EffectiveCertificateEvidenceByRecipient[recipientId];
        effectiveEvidence.Revision.Should().Be(2);
        effectiveEvidence.Kind.Should().Be(RecordKind.Correct);
        effectiveEvidence.OriginalDocumentReference.Should().Be("certificate-original-v2");

        snapshot.EffectiveAverageWageEvidences.Should().ContainSingle();
        snapshot.EffectiveAverageWageEvidences[0].Revision.Should().Be(2);
        snapshot.EffectiveAverageWageEvidences[0].Kind.Should().Be(RecordKind.Correct);
        snapshot.EffectiveAverageWageEvidences[0].AnnualWagePaidYen.Should().Be(1_500_000);
    }

    [Fact]
    public async Task Cancelled_claim_input_is_excluded_but_recipient_remains_in_scope_via_contract()
    {
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var month = new ServiceMonth(2026, 11);
        var factory = new TestDbContextFactory(_fixture.DbPath);
        var t0 = DateTimeOffset.UnixEpoch;

        // Contract はOfficeId非依存でこのテスト固有の月にだけ有効期間を区切る（他テストへ越境させない）。
        var contract = Contract.Create(
            Guid.NewGuid(), recipientId,
            new DateRange(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)),
            20, "tester", t0, Guid.NewGuid());

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
            CreatedAt = t0,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };
        var claimInputCancelled = claimInputNew with
        {
            Id = Guid.NewGuid(),
            Revision = 2,
            Kind = RecordKind.Cancel,
            ExpectedHeadId = claimInputNew.Id,
            UpperLimitManagementResult = null,
            UpperLimitManagedAmountYen = null,
            MunicipalSubsidyAmountYen = null,
            ExceptionalUsageStartMonth = null,
            ExceptionalUsageEndMonth = null,
            ExceptionalUsageDays = null,
            StandardUsageDayTotal = null,
            CreatedAt = t0.AddMinutes(1),
            ConcurrencyToken = Guid.NewGuid(),
        };

        await using (var seed = factory.CreateDbContext())
        {
            seed.AddRange(Rows.Office(officeId), Rows.Recipient(recipientId));
            seed.Add(contract);
            seed.AddRange(claimInputNew, claimInputCancelled);
            await seed.SaveChangesAsync();
        }

        var reader = new ClaimCalculationSnapshotReader(
            factory, new FakeOfficeClaimProfilePolicyProvider(MasterVersion));

        var snapshot = await reader.ReadAsync(officeId, month, default);

        snapshot.EffectiveClaimInputs.Should().BeEmpty();
        snapshot.RecipientIds.Should().Contain(recipientId);
        snapshot.BilledDaysByRecipient.Should().ContainKey(recipientId);
        snapshot.BilledDaysByRecipient[recipientId].Should().Be(0);
    }

    [Fact]
    public async Task Multiple_recipients_do_not_leak_across_dictionaries()
    {
        var officeId = Guid.NewGuid();
        var recipientA = Guid.NewGuid();
        var recipientB = Guid.NewGuid();
        var month = new ServiceMonth(2026, 12);
        var factory = new TestDbContextFactory(_fixture.DbPath);
        var t0 = DateTimeOffset.UnixEpoch;

        // Contract はOfficeId非依存でこのテスト固有の月にだけ有効期間を区切る（他テストへ越境させない）。
        var contractA = Contract.Create(
            Guid.NewGuid(), recipientA,
            new DateRange(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)),
            20, "tester", t0, Guid.NewGuid());
        var contractB = Contract.Create(
            Guid.NewGuid(), recipientB,
            new DateRange(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)),
            20, "tester", t0, Guid.NewGuid());

        var dailyRecordsA = new[]
        {
            DailyRecord.NewRecord(
                Guid.NewGuid(), recipientA, new DateOnly(2026, 12, 1),
                Attendance.Present, TransportKind.None, false, null, "tester", t0),
            DailyRecord.NewRecord(
                Guid.NewGuid(), recipientA, new DateOnly(2026, 12, 2),
                Attendance.Present, TransportKind.None, false, null, "tester", t0),
        };
        var dailyRecordsB = new[]
        {
            DailyRecord.NewRecord(
                Guid.NewGuid(), recipientB, new DateOnly(2026, 12, 1),
                Attendance.Present, TransportKind.None, false, null, "tester", t0),
        };

        var certificateAId = Guid.NewGuid();
        var certificateBId = Guid.NewGuid();
        var certificateA = Rows.Certificate(
            certificateAId, recipientA, new DateRange(new DateOnly(2026, 1, 1), null));
        // certificateB の有効期間はサービス月(2026-12)と重ならない -> recipientBの証件数は0
        var certificateB = Rows.Certificate(
            certificateBId, recipientB,
            new DateRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30)));

        await using (var seed = factory.CreateDbContext())
        {
            seed.AddRange(
                Rows.Office(officeId), Rows.Recipient(recipientA), Rows.Recipient(recipientB));
            seed.AddRange(contractA, contractB);
            seed.AddRange(dailyRecordsA);
            seed.AddRange(dailyRecordsB);
            seed.AddRange(certificateA, certificateB);
            await seed.SaveChangesAsync();
        }

        var reader = new ClaimCalculationSnapshotReader(
            factory, new FakeOfficeClaimProfilePolicyProvider(MasterVersion));

        var snapshot = await reader.ReadAsync(officeId, month, default);

        snapshot.RecipientIds.Should().Contain(recipientA).And.Contain(recipientB);
        snapshot.RecipientIds.Should().BeInAscendingOrder();

        snapshot.BilledDaysByRecipient[recipientA].Should().Be(2);
        snapshot.BilledDaysByRecipient[recipientB].Should().Be(1);

        snapshot.EffectiveCertificateCountByRecipient[recipientA].Should().Be(1);
        snapshot.EffectiveCertificateCountByRecipient[recipientB].Should().Be(0);
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
