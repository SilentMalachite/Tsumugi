using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Tests.UseCases.Claim;

/// <summary>
/// Task 9 use case群の合成入力。マスタ束はDomainのClaimCalculatorTestsと同じ合成語彙
/// （"b-type" / "staff-a" / "region-a"）を使い、production seedの正準トークンへ依存しない。
/// 基本報酬の解決はaverage-wage-band整数条件（公式option code）で行う（Task 9再エンコード後の契約）。
/// </summary>
internal static class ClaimPreparationTestKit
{
    internal static readonly Guid OfficeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    internal static readonly Guid RecipientId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    internal static readonly Guid SecondRecipientId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    internal static readonly DateTimeOffset Now = new(2025, 6, 15, 1, 2, 3, TimeSpan.Zero);
    internal static readonly ServiceMonth Month = new(2025, 6);

    internal static Office Office() => Domain.Entities.Office.Create(
        OfficeId,
        "1310000001",
        "テスト事業所",
        ServiceCategory.TypeB,
        RegionGrade.Grade2,
        "tester",
        Now,
        Guid.NewGuid(),
        postalCode: "100-0001",
        address: "東京都千代田区1-1",
        phoneNumber: "03-0000-0000",
        representativeTitleAndName: "施設長 テスト");

    internal static OfficeClaimProfile Profile(
        AverageWageBandOption? bandOption = null,
        R8ReformStatus? reformStatus = R8ReformStatus.NotApplicableBeforeR8)
    {
        var id = Guid.NewGuid();
        return new OfficeClaimProfile
        {
            Id = id,
            OfficeId = OfficeId,
            EffectiveFrom = new DateOnly(2024, 4, 1),
            EffectiveTo = null,
            RootId = id,
            Revision = 1,
            Kind = RecordKind.New,
            MasterVersion = new ClaimMasterVersion("master-v1"),
            ReformStatus = reformStatus,
            AverageWageBandOption =
                bandOption ?? new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 5),
            EvidenceDocumentId = "profile-doc",
            ConfirmedAt = Now,
            ConfirmedBy = "admin",
            ConfirmationReason = "台帳確認",
            CreatedAt = Now,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };
    }

    internal static ClaimInput Input(Guid? recipientId = null)
    {
        var id = Guid.NewGuid();
        return new ClaimInput
        {
            Id = id,
            OfficeId = OfficeId,
            RecipientId = recipientId ?? RecipientId,
            ServiceMonth = Month,
            RootId = id,
            Revision = 1,
            Kind = RecordKind.New,
            CreatedAt = Now,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };
    }

    internal static CertificateClaimEvidence Evidence(
        int? capYen = 9300,
        DateTimeOffset? confirmedAt = null,
        string? originalDocumentReference = "cert-doc#1")
    {
        var id = Guid.NewGuid();
        return new CertificateClaimEvidence
        {
            Id = id,
            CertificateId = Guid.NewGuid(),
            Validity = new DateRange(new DateOnly(2024, 4, 1), null),
            RootId = id,
            Revision = 1,
            Kind = RecordKind.New,
            MonthlyCostCap = capYen is { } cap
                ? new EnteredYen(true, cap)
                : new EnteredYen(false, null),
            UpperLimitManagementApplicability = UpperLimitManagementApplicability.NotApplicable,
            Article31Status = Article31SpecialBurdenStatus.NotApplicable,
            Article31AmountYen = new EnteredYen(false, null),
            Article31EffectivePeriod = null,
            OriginalDocumentReference = originalDocumentReference,
            ConfirmedAt = confirmedAt ?? Now,
            ConfirmedBy = "admin",
            ConfirmationReason = "原本確認",
            CreatedAt = Now,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };
    }

    internal static Certificate Certificate(
        string? municipalityNumber = null,
        string? subsidyMunicipalityNumber = null,
        string? upperLimitManagementProviderNumber = null,
        bool mealProvisionApplicable = false,
        PaymentBurdenCategory paymentBurden = PaymentBurdenCategory.General2)
        => Domain.Entities.Certificate.Create(
            Guid.NewGuid(),
            RecipientId,
            "certificate-no-1",
            new DateRange(new DateOnly(2024, 4, 1), null),
            supplyDays: 23,
            monthlyCostCap: 37_200,
            municipality: "テスト市",
            "tester",
            Now,
            Guid.NewGuid(),
            municipalityNumber: municipalityNumber,
            subsidyMunicipalityNumber: subsidyMunicipalityNumber,
            upperLimitManagementProviderNumber: upperLimitManagementProviderNumber,
            mealProvisionApplicable: mealProvisionApplicable,
            paymentBurden: paymentBurden);

    internal static ContractedProvider ContractedProvider(int? certificateEntryNumber = null)
        => Domain.Entities.ContractedProvider.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            providerNumber: "1310000001",
            providerName: "テスト事業所",
            serviceCategory: "就労継続支援B型",
            contractedSupplyDays: 23,
            contractDate: new DateOnly(2024, 4, 1),
            "tester",
            Now,
            Guid.NewGuid(),
            certificateEntryNumber: certificateEntryNumber);

    internal static AverageWageAnnualEvidence AverageWageEvidence()
    {
        var id = Guid.NewGuid();
        return new AverageWageAnnualEvidence
        {
            Id = id,
            OfficeId = OfficeId,
            SourceFiscalYear = 2024,
            PeriodStart = new DateOnly(2024, 4, 1),
            PeriodEnd = new DateOnly(2025, 3, 31),
            RootId = id,
            Revision = 1,
            Kind = RecordKind.New,
            CreatedAt = Now,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };
    }

    internal static ClaimCalculationSnapshot Snapshot(
        OfficeClaimProfile? profile = null,
        IReadOnlyList<ClaimInput>? inputs = null,
        IReadOnlyDictionary<Guid, CertificateClaimEvidence>? evidenceByRecipient = null,
        IReadOnlyList<AverageWageAnnualEvidence>? averageWageEvidences = null,
        IReadOnlyList<Guid>? recipientIds = null,
        IReadOnlyDictionary<Guid, int>? billedDays = null,
        IReadOnlyDictionary<Guid, int>? certificateCounts = null,
        bool includeProfile = true,
        IReadOnlyDictionary<Guid, Certificate>? certificateByRecipient = null,
        IReadOnlyDictionary<Guid, ContractedProvider>? contractedProviderByRecipient = null,
        IReadOnlyDictionary<Guid, ClaimDailyRecordAggregate>? dailyRecordAggregateByRecipient = null,
        IReadOnlyDictionary<Guid, DateOnly>? intensiveSupportEpisodeStartDateByRecipient = null,
        IReadOnlyList<OfficeCapability>? officeCapabilities = null,
        IReadOnlyDictionary<Guid, ClaimAdditionDailyCounts>? additionDailyCountsByRecipient = null)
        => new(
            recipientIds ?? [RecipientId],
            includeProfile ? profile ?? Profile() : null,
            inputs ?? [Input()],
            evidenceByRecipient ?? new Dictionary<Guid, CertificateClaimEvidence> { [RecipientId] = Evidence() },
            averageWageEvidences ?? [AverageWageEvidence()],
            billedDays ?? new Dictionary<Guid, int> { [RecipientId] = 2 },
            certificateCounts ?? new Dictionary<Guid, int> { [RecipientId] = 1 },
            // Task 12（ADR 0022）: 負担区分の解決にCertificateが必須になったため、既定は
            // 証あり（PaymentBurden=General2）を前提とする。「証なし」を明示的に検証するテストは
            // 空dictionaryを渡す。
            certificateByRecipient ?? new Dictionary<Guid, Certificate> { [RecipientId] = Certificate() },
            contractedProviderByRecipient,
            dailyRecordAggregateByRecipient,
            intensiveSupportEpisodeStartDateByRecipient,
            officeCapabilities,
            additionDailyCountsByRecipient);

    internal static OfficeCapability Capability(
        IReadOnlyDictionary<string, bool>? flags = null,
        DateOnly? periodStart = null,
        DateTimeOffset? createdAt = null)
        => OfficeCapability.Create(
            Guid.NewGuid(),
            OfficeId,
            new DateRange(periodStart ?? new DateOnly(2024, 4, 1), null),
            flags ?? new Dictionary<string, bool> { ["cap.synthetic.a"] = true },
            "tester",
            createdAt ?? Now,
            Guid.NewGuid());

    /// <summary>
    /// Task 12（ADR 0022）: <see cref="PaymentBurdenCategory"/>→burden-caps.json正準keyの対応。
    /// production（<c>OfficeClaimBillingTokenProvider</c>）と同じ完全一致表を合成語彙として複製する。
    /// </summary>
    private static readonly IReadOnlyDictionary<PaymentBurdenCategory, string> DefaultBurdenCategoryTokens =
        new Dictionary<PaymentBurdenCategory, string>
        {
            [PaymentBurdenCategory.Welfare] = "welfare",
            [PaymentBurdenCategory.LowIncome] = "low-income",
            [PaymentBurdenCategory.General1] = "general-1",
            [PaymentBurdenCategory.General2] = "general-2",
        };

    internal static ClaimBillingConditionTokens Tokens(
        string? rewardSystem = "b-type",
        string? regionKey = "region-a",
        string? serviceKind = "b-type",
        int? capacityHeadcount = 20,
        string? staffingKey = "staff-a",
        bool regionKeyConflict = false,
        IReadOnlyDictionary<string, ClaimCountMetric>? countSelectorBindings = null,
        IReadOnlyDictionary<PaymentBurdenCategory, string>? burdenCategoryTokens = null)
        => new(
            rewardSystem, regionKey, serviceKind, capacityHeadcount, staffingKey, regionKeyConflict,
            countSelectorBindings,
            burdenCategoryTokens ?? DefaultBurdenCategoryTokens);

    internal static ClaimMasterRelease Release()
        => new(new ClaimMasterVersion("master-v1"), new ServiceMonth(2024, 4), null, ["doc-1"]);

    private static ClaimSourceRef SourceRef() => new(
        "doc-1",
        "0000000000000000000000000000000000000000000000000000000000000000",
        "loc",
        ClaimSourceEvidenceRole.Authoritative,
        [ClaimSourceSupport.Conditions, ClaimSourceSupport.EffectivePeriod]);

    private static ClaimConditionDefinition Condition(
        string key,
        ClaimConditionKind kind,
        ClaimConditionOperator @operator,
        ClaimConditionOperand operand)
        => new(key, new ServiceMonth(2024, 4), null, kind, @operator, operand, [SourceRef()]);

    internal static ClaimCalculationMasterBundle SyntheticMasters(int unitsPerDay = 700) => new(
        BasicRewards:
        [
            new BasicRewardMasterRow(
                "base-a", "band-a", "staff-a", "cap-a", "610000", unitsPerDay,
                new ServiceMonth(2024, 4), null, [SourceRef()]),
        ],
        UnitAdjustments: [],
        RegionUnitPrices:
        [
            new RegionUnitPriceMasterRow(
                "price-a", "region-a", "b-type", 10.00m, new ServiceMonth(2024, 4), null, [SourceRef()]),
        ],
        BurdenCaps:
        [
            // Task 12（ADR 0022）: 既定証（PaymentBurden=General2）→区分key"general-2"に対応する
            // 合成マスタ行（制度上の値ではない）。
            new BurdenCapMasterRow(
                "burden-cap-general-2", "general-2", 37_200, new ServiceMonth(2024, 4), null, [SourceRef()]),
        ],
        TransitionRules: [],
        ServiceCodes:
        [
            new ServiceCodeMasterRow(
                "sc-a",
                "610000",
                "B型基本(合成)",
                "b-type",
                [],
                ["cond-system-b", "cond-avg-5", "cond-cap-a", "cond-staff-a"],
                new BaseComponentPassThroughRule("base-a", "step-base", null, BillingUnit.PerDay),
                [new ClaimComponentRef(ClaimComponentMasterKind.BasicRewards, "base-a", ClaimComponentRole.Base)],
                new ServiceMonth(2024, 4),
                null,
                [SourceRef()]),
        ],
        ConditionDefinitions:
        [
            Condition(
                "cond-system-b", ClaimConditionKind.RewardSystem, ClaimConditionOperator.Equals,
                new ClaimConditionTokenOperand("b-type")),
            Condition(
                "cond-avg-5", ClaimConditionKind.AverageWageBand, ClaimConditionOperator.Equals,
                new ClaimConditionIntegerOperand(5)),
            Condition(
                "cond-cap-a", ClaimConditionKind.Capacity, ClaimConditionOperator.LessThanOrEqual,
                new ClaimConditionIntegerOperand(20)),
            Condition(
                "cond-staff-a", ClaimConditionKind.Staffing, ClaimConditionOperator.Equals,
                new ClaimConditionTokenOperand("staff-a")),
        ]);

    internal static ClaimBatch Batch(
        int revision = 1,
        RecordKind kind = RecordKind.New,
        Guid? originId = null,
        Guid? expectedHeadBatchId = null,
        int? expectedHeadRevision = null,
        int totalUnits = 1400,
        int totalCostYen = 14000,
        int totalBenefitYen = 12600,
        int totalBurdenYen = 1400)
        => new()
        {
            Id = Guid.NewGuid(),
            OfficeId = OfficeId,
            ServiceMonth = Month,
            Revision = revision,
            Kind = kind,
            OriginId = originId,
            ExpectedHeadBatchId = expectedHeadBatchId,
            ExpectedHeadRevision = expectedHeadRevision,
            TotalUnits = kind == RecordKind.Cancel ? 0 : totalUnits,
            TotalCostYen = kind == RecordKind.Cancel ? 0 : totalCostYen,
            TotalBenefitYen = kind == RecordKind.Cancel ? 0 : totalBenefitYen,
            TotalBurdenYen = kind == RecordKind.Cancel ? 0 : totalBurdenYen,
            ClaimMasterVersion = "master-v1",
            CsvSpecificationVersion = "csv-head-v1",
            ReportSpecificationVersion = "report-head-v1",
            SnapshotApplicationVersion = "snapshot-head-v1",
            OperationApplicationVersion = "operation-head-v1",
            FinalizationOperationId = Guid.NewGuid(),
            OperationPayloadSchemaVersion = ClaimBatch.CurrentOperationPayloadSchemaVersion,
            OperationPayloadSha256 = new string('0', 64),
            CreatedAt = Now,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };

    internal static ClaimBatchAggregate Aggregate(ClaimBatch header, params Guid[] recipientIds)
        => new(
            header,
            recipientIds.Select(recipientId => new ClaimDetail
            {
                Id = Guid.NewGuid(),
                ClaimBatchId = header.Id,
                RecipientId = recipientId,
                SnapshotSchemaVersion = "claim-snapshot-v1",
                ClaimMasterVersion = header.ClaimMasterVersion,
                CsvSpecificationVersion = header.CsvSpecificationVersion,
                ReportSpecificationVersion = header.ReportSpecificationVersion,
                SnapshotApplicationVersion = header.SnapshotApplicationVersion,
                InputSnapshotJson = "{}",
                CalculationSnapshotJson = "{}",
                TotalUnits = 1400,
                TotalCostYen = 14000,
                BenefitYen = 12600,
                BurdenYen = 1400,
                CreatedAt = Now,
                CreatedBy = "tester",
                ConcurrencyToken = Guid.NewGuid(),
            }));

    internal static ClaimPreparationReadiness Readiness()
        => new(new EmptyRequirementProvider());

    internal sealed class EmptyRequirementProvider : IClaimInputRequirementProvider
    {
        public IReadOnlyList<ClaimInputRequirement> GetRequirements() => [];
    }

    internal sealed class FakeSnapshotReader(ClaimCalculationSnapshot snapshot)
        : IClaimCalculationSnapshotReader
    {
        public Task<ClaimCalculationSnapshot> ReadAsync(
            Guid officeId, ServiceMonth serviceMonth, CancellationToken ct)
            => Task.FromResult(snapshot);
    }

    internal sealed class FakeMasterProvider(
        ClaimMasterRelease? release,
        ClaimCalculationMasterBundle? masters) : IClaimMasterProvider
    {
        public int CalculationMasterResolutions { get; private set; }

        public ClaimMasterRelease ResolveVersion(ServiceMonth serviceMonth)
            => release ?? throw new ClaimMasterPolicyUnavailableException(
                ClaimMasterPolicyUnavailableCode.Unavailable);

        public ClaimCalculationMasterBundle ResolveCalculationMasters(ServiceMonth serviceMonth)
        {
            CalculationMasterResolutions++;
            return masters ?? throw new ClaimMasterPolicyUnavailableException(
                ClaimMasterPolicyUnavailableCode.Unavailable);
        }
    }

    internal sealed class FakeOfficeRepository(Office? office) : IOfficeRepository
    {
        public Task AddAsync(Office entity, CancellationToken ct) => throw new NotSupportedException();
        public Task<Office?> FindByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(office);
        public Task<Office?> FindByNumberAsync(string officeNumber, CancellationToken ct)
            => throw new NotSupportedException();
        public Task UpdateAsync(Office entity, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Office>> ListAsync(CancellationToken ct) => throw new NotSupportedException();
    }

    internal sealed class FakeTokenProvider(ClaimBillingConditionTokens tokens) : IClaimBillingTokenProvider
    {
        public ClaimBillingConditionTokens Resolve(
            Office office, OfficeClaimProfile? profile, ServiceMonth serviceMonth) => tokens;
    }

    internal sealed class FakeFinalizationStore : IClaimFinalizationStore
    {
        public ClaimFinalizationDraft? LastDraft { get; private set; }
        public ClaimFinalizationResult Result { get; set; } = new(Guid.NewGuid(), 1, IsReplay: false);

        public Task<ClaimFinalizationResult> CommitAsync(ClaimFinalizationDraft draft, CancellationToken ct)
        {
            LastDraft = draft;
            return Task.FromResult(Result);
        }
    }

    internal sealed class FakeBatchRepository(IReadOnlyList<ClaimBatchAggregate> aggregates)
        : IClaimBatchRepository
    {
        public Task<IReadOnlyList<ClaimBatchAggregate>> ListHistoryAggregatesAsync(
            Guid officeId, ServiceMonth serviceMonth, CancellationToken ct)
            => Task.FromResult(aggregates);

        public Task<ClaimBatchAggregate?> FindByOperationIdAsync(
            Guid finalizationOperationId, CancellationToken ct)
            => Task.FromResult<ClaimBatchAggregate?>(null);
    }
}
