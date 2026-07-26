using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.ClaimMasters;
using Tsumugi.Infrastructure.Csv.Mapping;
using Tsumugi.Infrastructure.Csv.Specifications;
using Xunit;

namespace Tsumugi.Infrastructure.Tests.Claim;

/// <summary>
/// 本タスク（ADR 0049の一般化）の要: <see cref="OfficeCapabilityCoveragePolicy.FindUnsatisfiableDeclaredKeys"/>
/// を実production配線（実<see cref="JsonClaimMasterProvider.LoadEmbedded"/> R6マスタ・実
/// <see cref="OfficeClaimBillingTokenProvider"/>・実<see cref="CalculateClaimUseCase"/>）で検証する。
/// </summary>
/// <remarks>
/// 対象月は2024-06（処遇改善(Ⅴ)＝option6が有効な最初の月。effectiveTo=2025-03）。実seedの
/// 処遇改善(Ⅴ)行23件はすべて<c>capability-treatment-improvement-v</c>（option6）と
/// <c>capability-treatment-improvement-v-band-{n}</c>（band）の両方を<c>conditionSelectors</c>に
/// 要求するため、option6だけを宣言してbandを宣言していない事業所はどの行にも一致せず、
/// 加算が無音で0円になる（brief記載の実例そのもの）。
/// </remarks>
public sealed class CapabilityDeclarationSatisfiabilityProductionWiringTests
{
    private static readonly Guid OfficeId = Guid.NewGuid();
    private static readonly Guid RecipientId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2024, 6, 15, 1, 2, 3, TimeSpan.Zero);
    private static readonly ServiceMonth Month = new(2024, 6);

    private const string CertificateNumber = "1234567890";
    private const int BilledDays = 10;

    private const string TreatmentImprovementOption6 = "mhlw.b46.capability.treatment-improvement.6";
    private const string TreatmentImprovementOption2 = "mhlw.b46.capability.treatment-improvement.2";
    private const string TreatmentImprovementVBand3 = "mhlw.b46.capability.treatment-improvement-v-band.3";

    // NOTE(teeth, RED confirmed): option6のみを宣言すると、(Ⅴ)行23件のどれもband条件を満たせず
    // 1つも成立しない。既存のCapabilityCoverageWarnings（失効・未施行）はoption6が当月に有効な
    // ため沈黙し、代わりに本タスクの新しい警告が単独で出る（原因が異なる別枠であることの
    // 実seed上の証拠）。
    [Fact]
    public async Task
        Declaring_only_the_v_option_without_a_band_is_reported_as_an_incomplete_capability_declaration()
    {
        var useCase = CreateUseCase(BuildSnapshot(
            officeCapabilities:
            [
                Capability(new Dictionary<string, bool> { [TreatmentImprovementOption6] = true }),
            ]));

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(OfficeId, Month), CancellationToken.None);

        dto.IsReady.Should().BeTrue("宣言不完全は確定を止めない（ADR 0049と同じ非ブロッキング契約）");
        dto.IncompleteCapabilityDeclarationWarnings.Should().ContainSingle()
            .Which.Should().Be(TreatmentImprovementOption6);
        dto.CapabilityCoverageWarnings.Should().BeEmpty(
            "option6自体は2024-06に有効なため、失効・未施行の既存警告は出ない（排反性の実証）");
    }

    // NOTE(teeth, RED confirmed): bandも併せて宣言すれば、該当band行（465126: v + v-band-3のみ、
    // facility-classification条件なし）が充足可能になり、警告は出ない。
    [Fact]
    public async Task Declaring_the_v_option_together_with_its_band_is_not_reported()
    {
        var useCase = CreateUseCase(BuildSnapshot(
            officeCapabilities:
            [
                Capability(new Dictionary<string, bool>
                {
                    [TreatmentImprovementOption6] = true,
                    [TreatmentImprovementVBand3] = true,
                }),
            ]));

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(OfficeId, Month), CancellationToken.None);

        dto.IsReady.Should().BeTrue();
        dto.IncompleteCapabilityDeclarationWarnings.Should().BeEmpty();
        dto.CapabilityCoverageWarnings.Should().BeEmpty();
    }

    // NOTE(teeth, RED confirmed): レビュー指摘（orphan band）: band だけを宣言しoption6を
    // 宣言していない向き。以前は`OfficeCapabilityViewModel`が(Ⅴ)から他区分へ切り替えた後も
    // band選択を無条件に書いていたため、実運用でこの宣言集合（band単独）が実際に永続化され
    // 得た。band.3自体は2024-06に有効な条件定義を持つ（`CapabilityCoverageWarnings`は沈黙する）
    // が、band.3を要求する行（465126）は同じ行でoption6も要求するため、option6が無いと
    // 1行も一致しない。判定関数は宣言キーの役割（主/companion）を区別しないため、この向きも
    // 実seedで同じ判定になることを固定する。
    [Fact]
    public async Task Declaring_only_the_band_without_the_v_option_is_reported_as_an_incomplete_capability_declaration()
    {
        var useCase = CreateUseCase(BuildSnapshot(
            officeCapabilities:
            [
                Capability(new Dictionary<string, bool> { [TreatmentImprovementVBand3] = true }),
            ]));

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(OfficeId, Month), CancellationToken.None);

        dto.IsReady.Should().BeTrue("宣言不完全は確定を止めない（ADR 0049と同じ非ブロッキング契約）");
        dto.IncompleteCapabilityDeclarationWarnings.Should().ContainSingle()
            .Which.Should().Be(TreatmentImprovementVBand3);
        dto.CapabilityCoverageWarnings.Should().BeEmpty(
            "band.3自体は2024-06に有効なため、失効・未施行の既存警告は出ない（排反性の実証）");
    }

    // NOTE(teeth, RED confirmed): 処遇改善(Ⅰ)＝option2を要求する行(465120/465138)は、実seedでは
    // どちらも同じ行にfacility-classification条件（general/designated-support-facility）を
    // 併記する。capability種別だけを見ないと、この施設条件が偽陽性の原因になる
    // （施設条件の値は宣言集合に決して含まれないため、フィルタを誤ると常に「不成立」に見える）。
    // 本テストはこの偽陽性が起きないことを実seedで証明する。
    [Fact]
    public async Task
        Declaring_the_i_option_alone_is_not_reported_even_though_its_rows_also_carry_a_facility_classification_condition()
    {
        var useCase = CreateUseCase(BuildSnapshot(
            officeCapabilities:
            [
                Capability(new Dictionary<string, bool> { [TreatmentImprovementOption2] = true }),
            ]));

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(OfficeId, Month), CancellationToken.None);

        dto.IsReady.Should().BeTrue();
        dto.IncompleteCapabilityDeclarationWarnings.Should().BeEmpty(
            "facility-classification条件はcapability種別ではないため、行の充足可否に含めてはならない");
        dto.CapabilityCoverageWarnings.Should().BeEmpty();
    }

    private static OfficeCapability Capability(IReadOnlyDictionary<string, bool> flags) => OfficeCapability.Create(
        Guid.NewGuid(),
        OfficeId,
        new DateRange(new DateOnly(2024, 6, 1), null),
        flags,
        "tester",
        Now,
        Guid.NewGuid());

    private static CalculateClaimUseCase CreateUseCase(ClaimCalculationSnapshot snapshot) => new(
        new FakeSnapshotReader(snapshot),
        JsonClaimMasterProvider.LoadEmbedded(),
        new FakeOfficeRepository(BuildOffice()),
        new OfficeClaimBillingTokenProvider(),
        new ClaimPreparationReadiness(new EmptyRequirementProvider()),
        CsvGenericFieldCatalog.LoadEmbedded(CsvSpecificationRegistry.LoadEmbedded()),
        CsvSpecificationRegistry.LoadEmbedded());

    private static Office BuildOffice() => Office.Create(
        OfficeId,
        "1310000001",
        "テスト事業所",
        ServiceCategory.TypeB,
        RegionGrade.Grade1,
        "tester",
        Now,
        Guid.NewGuid(),
        postalCode: "100-0001",
        address: "東京都千代田区1-1",
        phoneNumber: "03-0000-0000",
        representativeTitleAndName: "施設長 テスト");

    /// <summary>
    /// <c>ClaimPreviewProductionWiringTests.BuildSnapshot</c>と同じ「フル入力済みの事業所・月」の
    /// 最小変形（対象月2024-06、<see cref="FacilityClassification.General"/>を明示）。
    /// FacilityClassificationを与えるのは、処遇改善(Ⅰ)行(465120/465138)がfacility-classification
    /// 条件を併記するため（ADR 0047）。一般事業所として解決させることで、施設区分未入力による
    /// フェイルクローズ（<see cref="ServiceCodeResolutionErrorCode.FacilityClassificationUnresolved"/>）
    /// を避け、本テストの対象（capability充足可否）だけを検査できるようにする。
    /// </summary>
    private static ClaimCalculationSnapshot BuildSnapshot(IReadOnlyList<OfficeCapability>? officeCapabilities)
    {
        var profileId = Guid.NewGuid();
        var profile = new OfficeClaimProfile
        {
            Id = profileId,
            OfficeId = OfficeId,
            EffectiveFrom = new DateOnly(2024, 6, 1),
            EffectiveTo = new DateOnly(2026, 5, 31),
            RootId = profileId,
            Revision = 1,
            Kind = RecordKind.New,
            MasterVersion = new ClaimMasterVersion("claim-master-r6-06"),
            ReformStatus = R8ReformStatus.NotApplicableBeforeR8,
            AverageWageBandOption = new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 5),
            FacilityClassification = FacilityClassification.General,
            EvidenceDocumentId = "profile-doc",
            ConfirmedAt = Now,
            ConfirmedBy = "admin",
            ConfirmationReason = "台帳確認",
            CapacityHeadcount = 15,
            StaffingKey = "staff-6-1",
            RegionKey = "region-grade-1",
            CreatedAt = Now,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };

        var inputId = Guid.NewGuid();
        var claimInput = new ClaimInput
        {
            Id = inputId,
            OfficeId = OfficeId,
            RecipientId = RecipientId,
            ServiceMonth = Month,
            RootId = inputId,
            Revision = 1,
            Kind = RecordKind.New,
            UpperLimitManagementResult = UpperLimitManagementResult.Result1,
            UpperLimitManagedAmountYen = 0,
            MunicipalSubsidyAmountYen = 0,
            SpecialVisitSupportBilledCount = 1,
            OffsiteSupportCumulativeDays = 20,
            CreatedAt = Now,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };

        var evidenceId = Guid.NewGuid();
        var evidence = new CertificateClaimEvidence
        {
            Id = evidenceId,
            CertificateId = Guid.NewGuid(),
            Validity = new DateRange(new DateOnly(2024, 4, 1), null),
            RootId = evidenceId,
            Revision = 1,
            Kind = RecordKind.New,
            MonthlyCostCap = new EnteredYen(true, 9300),
            UpperLimitManagementApplicability = UpperLimitManagementApplicability.NotApplicable,
            Article31Status = Article31SpecialBurdenStatus.NotApplicable,
            Article31AmountYen = new EnteredYen(false, null),
            Article31EffectivePeriod = null,
            OriginalDocumentReference = "cert-doc#1",
            ConfirmedAt = Now,
            ConfirmedBy = "admin",
            ConfirmationReason = "原本確認",
            CreatedAt = Now,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };

        var averageWageEvidenceId = Guid.NewGuid();
        var averageWageEvidence = new AverageWageAnnualEvidence
        {
            Id = averageWageEvidenceId,
            OfficeId = OfficeId,
            SourceFiscalYear = 2024,
            PeriodStart = new DateOnly(2024, 4, 1),
            PeriodEnd = new DateOnly(2025, 3, 31),
            RootId = averageWageEvidenceId,
            Revision = 1,
            Kind = RecordKind.New,
            CreatedAt = Now,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };

        var certificateId = Guid.NewGuid();
        var certificate = Certificate.Create(
            certificateId,
            RecipientId,
            CertificateNumber,
            new DateRange(new DateOnly(2024, 4, 1), null),
            supplyDays: 23,
            monthlyCostCap: 37_200,
            municipality: "テスト市",
            "tester",
            Now,
            Guid.NewGuid(),
            municipalityNumber: "131000",
            subsidyMunicipalityNumber: "132000",
            upperLimitManagementProviderNumber: "1310000099",
            paymentBurden: PaymentBurdenCategory.General1);

        var contractedProvider = ContractedProvider.Create(
            Guid.NewGuid(),
            certificateId,
            providerNumber: "1310000001",
            providerName: "テスト事業所",
            serviceCategory: "就労継続支援B型",
            contractedSupplyDays: 23,
            contractDate: new DateOnly(2024, 4, 1),
            "tester",
            Now,
            Guid.NewGuid(),
            certificateEntryNumber: 5,
            firstServiceDate: new DateOnly(2024, 4, 1));

        var dailyRecordAggregate = new ClaimDailyRecordAggregate(
            ServiceStartTime: new TimeOnly(9, 0),
            ServiceEndTime: new TimeOnly(15, 0),
            SpecialVisitSupportMinutesTotal: 30,
            OffsiteSupportApplied: true,
            MedicalCoordinationType: MedicalCoordinationType.TypeI,
            TrialUseSupportType: TrialUseSupportType.TypeI,
            RegionalCollaborationApplied: true,
            IntensiveSupportApplied: true,
            EmergencyAdmissionApplied: true,
            RecipientConfirmation: RecipientConfirmationStatus.Confirmed,
            SpecialVisitSupportBilledHoursTotal: 2);

        return new ClaimCalculationSnapshot(
            [RecipientId],
            profile,
            [claimInput],
            new Dictionary<Guid, CertificateClaimEvidence> { [RecipientId] = evidence },
            [averageWageEvidence],
            new Dictionary<Guid, int> { [RecipientId] = BilledDays },
            new Dictionary<Guid, int> { [RecipientId] = 1 },
            new Dictionary<Guid, Certificate> { [RecipientId] = certificate },
            new Dictionary<Guid, ContractedProvider> { [RecipientId] = contractedProvider },
            new Dictionary<Guid, ClaimDailyRecordAggregate> { [RecipientId] = dailyRecordAggregate },
            new Dictionary<Guid, DateOnly> { [RecipientId] = new DateOnly(2025, 1, 6) },
            officeCapabilities);
    }

    private sealed class FakeSnapshotReader(ClaimCalculationSnapshot snapshot)
        : IClaimCalculationSnapshotReader
    {
        public Task<ClaimCalculationSnapshot> ReadAsync(
            Guid officeId, ServiceMonth serviceMonth, CancellationToken ct)
            => Task.FromResult(snapshot);
    }

    private sealed class FakeOfficeRepository(Office office) : IOfficeRepository
    {
        public Task AddAsync(Office entity, CancellationToken ct) => throw new NotSupportedException();
        public Task<Office?> FindByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<Office?>(office);
        public Task<Office?> FindByNumberAsync(string officeNumber, CancellationToken ct)
            => throw new NotSupportedException();
        public Task UpdateAsync(Office entity, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Office>> ListAsync(CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class EmptyRequirementProvider : IClaimInputRequirementProvider
    {
        public IReadOnlyList<ClaimInputRequirement> GetRequirements(string specificationVersion) => [];
    }
}
