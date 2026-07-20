using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Application.Dtos.Claim.Reports;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Application.Tests.Claim;

public sealed class OperationLocalSnapshotReaderTests
{
    private static readonly Guid OfficeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RecipientId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly ServiceMonth Month = new(2026, 5);

    [Fact]
    public async Task ReadAsync_captures_all_21_report_fields_from_entities()
    {
        var office = SampleOffice();
        var recipient = SampleRecipient();
        var certificate = SampleCertificate();
        var dailyRecord = SampleDailyRecord();
        var episode = SampleEpisode();
        var claimInput = SampleClaimInput();
        var sut = CreateSut(office, recipient, certificate, [dailyRecord], [episode], [claimInput]);

        var snapshot = await sut.ReadAsync(
            OfficeId, RecipientId, Month, SampleCalculationResult(),
            "r6-2026-04", "r7-10", "r1-10", CancellationToken.None);

        // Office 4 fields (report:benefit-claim-form:header:004/005/006/008)
        snapshot.Office.PostalCode.Should().Be("1000001");
        snapshot.Office.Address.Should().Be("東京都千代田区千代田1-1");
        snapshot.Office.PhoneNumber.Should().Be("03-0000-0000");
        snapshot.Office.RepresentativeTitleAndName.Should().Be("代表取締役 山田太郎");

        // Certificate 3 fields (report:benefit-claim-detail:header:001/003,
        // upper-limit-management:001)
        snapshot.Certificate.MunicipalityNumber.Should().Be("131016");
        snapshot.Certificate.SubsidyMunicipalityNumber.Should().Be("131017");
        snapshot.Certificate.UpperLimitManagementProviderNumber.Should().Be("0123456789");

        // ClaimInput 3 fields (upper-limit-management:003/004, summary:015)
        snapshot.ClaimInput.UpperLimitManagementResult.Should().Be("Result1");
        snapshot.ClaimInput.UpperLimitManagedAmountYen.Should().Be(1000);
        snapshot.ClaimInput.MunicipalSubsidyAmountYen.Should().Be(500);

        // DailyRecord 10 fields (service-performance:daily:004/005/008/010/011/
        // 012/013/014/015/016)
        var record = snapshot.DailyRecords.Should().ContainSingle().Subject;
        record.ServiceStartTime.Should().Be(new TimeOnly(9, 0));
        record.ServiceEndTime.Should().Be(new TimeOnly(16, 0));
        record.SpecialVisitSupportMinutes.Should().Be(45);
        record.MedicalCoordinationType.Should().Be("TypeI");
        record.TrialUseSupportType.Should().Be("TypeII");
        record.RegionalCollaborationApplied.Should().BeTrue();
        record.EmergencyAdmissionApplied.Should().BeTrue();
        record.IntensiveSupportApplied.Should().BeTrue();
        record.OffsiteSupportApplied.Should().BeTrue();
        record.RecipientConfirmation.Should().BeTrue();

        // IntensiveSupportEpisode 1 field (service-performance:intensive-support:001)
        snapshot.IntensiveSupportEpisode.Should().NotBeNull();
        snapshot.IntensiveSupportEpisode!.StartDate.Should().Be(new DateOnly(2026, 4, 1));

        // Identity/version fields carried through untouched.
        snapshot.RecipientId.Should().Be(RecipientId);
        snapshot.ServiceMonth.Should().Be(Month);
        snapshot.ClaimMasterVersion.Should().Be("r6-2026-04");
        snapshot.CsvSpecificationVersion.Should().Be("r7-10");
        snapshot.ReportSpecificationVersion.Should().Be("r1-10");
    }

    [Fact]
    public async Task ReadAsync_throws_when_office_missing()
    {
        var sut = CreateSut(
            office: null, SampleRecipient(), SampleCertificate(), [], [], []);

        var act = () => sut.ReadAsync(
            OfficeId, RecipientId, Month, SampleCalculationResult(),
            "r6-2026-04", "r7-10", "r1-10", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ReadAsync_throws_when_certificate_missing()
    {
        var sut = CreateSut(
            SampleOffice(), SampleRecipient(), certificate: null, [], [], []);

        var act = () => sut.ReadAsync(
            OfficeId, RecipientId, Month, SampleCalculationResult(),
            "r6-2026-04", "r7-10", "r1-10", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ReadAsync_treats_missing_claim_input_and_episode_as_absent_not_an_error()
    {
        var sut = CreateSut(
            SampleOffice(), SampleRecipient(), SampleCertificate(), [], [], []);

        var snapshot = await sut.ReadAsync(
            OfficeId, RecipientId, Month, SampleCalculationResult(),
            "r6-2026-04", "r7-10", "r1-10", CancellationToken.None);

        snapshot.IntensiveSupportEpisode.Should().BeNull();
        snapshot.ClaimInput.Should().Be(
            new ClaimFinalizationClaimInputSnapshot(null, null, null, null, null, null, null));
    }

    [Fact]
    public async Task ReadAsync_builds_claim_lines_from_calculation_result()
    {
        var sut = CreateSut(SampleOffice(), SampleRecipient(), SampleCertificate(), [], [], []);
        var calculationResult = SampleCalculationResult();

        var snapshot = await sut.ReadAsync(
            OfficeId, RecipientId, Month, calculationResult,
            "r6-2026-04", "r7-10", "r1-10", CancellationToken.None);

        snapshot.ClaimLines.Should().HaveCount(2);
        var basic = snapshot.ClaimLines[0];
        basic.Kind.Should().Be(ClaimDetailLineKind.Basic);
        basic.ServiceCode.Should().Be("B_BASE_W1_C20_S1");
        basic.Unit.Should().Be(600);
        basic.Count.Should().Be(20);

        var addition = snapshot.ClaimLines[1];
        addition.Kind.Should().Be(ClaimDetailLineKind.Addition);
        addition.ServiceCode.Should().Be("MEAL_PROVISION_I");
        addition.Unit.Should().Be(600);
        addition.Count.Should().Be(1);

        // Line amounts reconcile to the recipient-level total (see production XML doc for the
        // approximation rationale: no region unit price is available to this reader).
        (basic.AmountYen + addition.AmountYen).Should().Be(calculationResult.TotalCostYen);
    }

    /// <summary>
    /// Task 3 review Finding 2: the sibling test above uses TotalCostYen=126_000 / TotalUnits=12_600,
    /// which divides evenly (unitPriceApprox = 10.0m exactly), so <c>Math.Floor</c> never truncates
    /// and the "remainder allocated to the basic line" rule is never actually exercised. This fixture
    /// uses TotalCostYen=1234 / TotalUnits=100 (unitPriceApprox = 12.34m, fractional) with two addition
    /// lines of distinct unit counts, and pins the exact per-line <c>AmountYen</c> derived by hand from
    /// <see cref="OperationLocalSnapshotReader.BuildClaimLines"/>'s documented rule:
    /// addition amount = Math.Floor(units * 12.34m), basic amount = TotalCostYen - Σ(addition amounts).
    /// addition A: 7 * 12.34 = 86.38 → floor 86. addition B: 13 * 12.34 = 160.42 → floor 160.
    /// basic: 1234 - (86 + 160) = 988.
    /// </summary>
    [Fact]
    public async Task ReadAsync_floors_addition_amounts_and_allocates_remainder_to_basic_line_when_unit_price_is_fractional()
    {
        var sut = CreateSut(SampleOffice(), SampleRecipient(), SampleCertificate(), [], [], []);
        var calculationResult = new RecipientClaimResult(
            RecipientId,
            "B_BASE_FRACTIONAL",
            BilledDays: 20,
            TotalUnits: 100,
            TotalCostYen: 1234,
            BenefitYen: 1110,
            BurdenYen: 124,
            AdditionLines:
            [
                new RecipientClaimAdditionLine("ADDITION_A", "加算A", 7),
                new RecipientClaimAdditionLine("ADDITION_B", "加算B", 13),
            ]);

        var snapshot = await sut.ReadAsync(
            OfficeId, RecipientId, Month, calculationResult,
            "r6-2026-04", "r7-10", "r1-10", CancellationToken.None);

        snapshot.ClaimLines.Should().HaveCount(3);

        var basic = snapshot.ClaimLines[0];
        basic.Kind.Should().Be(ClaimDetailLineKind.Basic);
        basic.ServiceCode.Should().Be("B_BASE_FRACTIONAL");
        basic.Unit.Should().Be(4); // (100 - 20) / 20
        basic.Count.Should().Be(20);
        basic.AmountYen.Should().Be(988);

        var additionA = snapshot.ClaimLines[1];
        additionA.Kind.Should().Be(ClaimDetailLineKind.Addition);
        additionA.ServiceCode.Should().Be("ADDITION_A");
        additionA.Unit.Should().Be(7);
        additionA.Count.Should().Be(1);
        additionA.AmountYen.Should().Be(86);

        var additionB = snapshot.ClaimLines[2];
        additionB.Kind.Should().Be(ClaimDetailLineKind.Addition);
        additionB.ServiceCode.Should().Be("ADDITION_B");
        additionB.Unit.Should().Be(13);
        additionB.Count.Should().Be(1);
        additionB.AmountYen.Should().Be(160);

        (basic.AmountYen + additionA.AmountYen + additionB.AmountYen)
            .Should().Be(calculationResult.TotalCostYen);
    }

    private static RecipientClaimResult SampleCalculationResult() => new(
        RecipientId,
        "B_BASE_W1_C20_S1",
        BilledDays: 20,
        TotalUnits: 12_600,
        TotalCostYen: 126_000,
        BenefitYen: 113_400,
        BurdenYen: 12_600,
        AdditionLines: [new RecipientClaimAdditionLine("MEAL_PROVISION_I", "食事提供体制加算", 600)]);

    private static Office SampleOffice() => Office.Create(
        OfficeId, "0123456789", "テスト事業所", ServiceCategory.TypeB, RegionGrade.None,
        createdBy: "seed", createdAt: Now, concurrencyToken: Guid.NewGuid(),
        postalCode: "1000001", address: "東京都千代田区千代田1-1",
        phoneNumber: "03-0000-0000", representativeTitleAndName: "代表取締役 山田太郎");

    private static Recipient SampleRecipient() => Recipient.Create(
        RecipientId, "山田太郎", "ヤマダタロウ", new DateOnly(1990, 1, 1),
        createdBy: "seed", createdAt: Now, concurrencyToken: Guid.NewGuid());

    private static Certificate SampleCertificate() => Certificate.Create(
        Guid.NewGuid(), RecipientId, "9876543210",
        new DateRange(new DateOnly(2024, 4, 1), null),
        supplyDays: 23, monthlyCostCap: 9300, municipality: "テスト市",
        createdBy: "seed", createdAt: Now, concurrencyToken: Guid.NewGuid(),
        municipalityNumber: "131016",
        subsidyMunicipalityNumber: "131017",
        upperLimitManagementProviderNumber: "0123456789",
        upperLimitManagementProvider: "テスト上限管理事業所");

    private static DailyRecord SampleDailyRecord() => DailyRecord.NewRecord(
        Guid.NewGuid(), RecipientId, new DateOnly(2026, 5, 1),
        Attendance.Present, TransportKind.Round, mealProvided: true,
        note: null, createdBy: "seed", createdAt: Now,
        serviceStartTime: new TimeOnly(9, 0),
        serviceEndTime: new TimeOnly(16, 0),
        specialVisitSupportMinutes: 45,
        offsiteSupportApplied: true,
        medicalCoordinationType: MedicalCoordinationType.TypeI,
        trialUseSupportType: TrialUseSupportType.TypeII,
        regionalCollaborationApplied: true,
        intensiveSupportApplied: true,
        emergencyAdmissionApplied: true,
        recipientConfirmation: RecipientConfirmationStatus.Confirmed);

    private static IntensiveSupportEpisode SampleEpisode()
    {
        var id = Guid.NewGuid();
        return new IntensiveSupportEpisode
        {
            Id = id,
            OfficeId = OfficeId,
            RecipientId = RecipientId,
            RootId = id,
            Revision = 1,
            Kind = RecordKind.New,
            StartDate = new DateOnly(2026, 4, 1),
            CreatedAt = Now,
            CreatedBy = "seed",
            ConcurrencyToken = Guid.NewGuid(),
        };
    }

    private static ClaimInput SampleClaimInput()
    {
        var id = Guid.NewGuid();
        return new ClaimInput
        {
            Id = id,
            OfficeId = OfficeId,
            RecipientId = RecipientId,
            ServiceMonth = Month,
            RootId = id,
            Revision = 1,
            Kind = RecordKind.New,
            UpperLimitManagementResult = UpperLimitManagementResult.Result1,
            UpperLimitManagedAmountYen = 1000,
            MunicipalSubsidyAmountYen = 500,
            CreatedAt = Now,
            CreatedBy = "seed",
            ConcurrencyToken = Guid.NewGuid(),
        };
    }

    private static OperationLocalSnapshotReader CreateSut(
        Office? office,
        Recipient? recipient,
        Certificate? certificate,
        IReadOnlyList<DailyRecord> dailyRecords,
        IReadOnlyList<IntensiveSupportEpisode> episodes,
        IReadOnlyList<ClaimInput> claimInputs)
        => new(
            new FakeOfficeRepository(office),
            new FakeRecipientRepository(recipient),
            new FakeCertificateRepository(certificate),
            new FakeDailyRecordRepository(dailyRecords),
            new FakeIntensiveSupportEpisodeRepository(episodes),
            new FakeClaimInputRepository(claimInputs));

    private sealed class FakeOfficeRepository(Office? office) : IOfficeRepository
    {
        public Task AddAsync(Office entity, CancellationToken ct) => throw new NotSupportedException();
        public Task<Office?> FindByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(office);
        public Task<Office?> FindByNumberAsync(string officeNumber, CancellationToken ct)
            => throw new NotSupportedException();
        public Task UpdateAsync(Office entity, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Office>> ListAsync(CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeRecipientRepository(Recipient? recipient) : IRecipientRepository
    {
        public Task AddAsync(Recipient entity, CancellationToken ct) => throw new NotSupportedException();
        public Task<Recipient?> FindByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(recipient);
        public Task UpdateAsync(Recipient entity, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Recipient>> ListAsync(bool includeArchived, CancellationToken ct)
            => throw new NotSupportedException();
    }

    private sealed class FakeCertificateRepository(Certificate? certificate) : ICertificateRepository
    {
        public Task AddAsync(Certificate entity, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Certificate>> ListByRecipientAsync(Guid recipientId, CancellationToken ct)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<Certificate>> ListAllAsync(CancellationToken ct)
            => throw new NotSupportedException();
        public Task<Certificate?> FindEffectiveAsync(Guid recipientId, DateOnly asOf, CancellationToken ct)
            => Task.FromResult(certificate);
    }

    private sealed class FakeDailyRecordRepository(IReadOnlyList<DailyRecord> records)
        : IDailyRecordRepository
    {
        public Task AddAsync(DailyRecord record, CancellationToken ct) => throw new NotSupportedException();
        public Task<DailyRecord?> FindByIdAsync(Guid id, CancellationToken ct)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<DailyRecord>> ListByRecipientAndDateAsync(
            Guid recipientId, DateOnly serviceDate, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<DailyRecord>> ListByRecipientAndMonthAsync(
            Guid recipientId, int year, int month, CancellationToken ct) => Task.FromResult(records);
    }

    private sealed class FakeIntensiveSupportEpisodeRepository(IReadOnlyList<IntensiveSupportEpisode> history)
        : IIntensiveSupportEpisodeRepository
    {
        public Task AddAsync(IntensiveSupportEpisode episode, CancellationToken ct)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<IntensiveSupportEpisode>> ListHistoryAsync(
            Guid officeId, Guid recipientId, CancellationToken ct) => Task.FromResult(history);
    }

    private sealed class FakeClaimInputRepository(IReadOnlyList<ClaimInput> history) : IClaimInputRepository
    {
        public Task AddAsync(ClaimInput input, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ClaimInput>> ListHistoryAsync(
            Guid officeId, Guid recipientId, ServiceMonth serviceMonth, CancellationToken ct)
            => Task.FromResult(history);
    }
}
