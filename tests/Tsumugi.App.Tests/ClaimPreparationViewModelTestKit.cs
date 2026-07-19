using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.App.Tests;

/// <summary>
/// ClaimPreparationViewModelTests専用の合成入力。tests/Tsumugi.Application.Tests の
/// ClaimPreparationTestKit と同じ合成語彙（"b-type" / "staff-a" / "region-a"）を使う
/// （internalは別アセンブリを越えて共有できないため、この層専用に複製する）。
/// VMはCalculate/Close/Cancel/QueryClaimUseCaseを実インスタンスとして構成し、
/// 各use caseの外部依存（snapshot reader・master provider・office repository・
/// token provider・batch repository/finalization store）だけをフェイクへ差し替える。
/// </summary>
internal static class ClaimPreparationViewModelTestKit
{
    internal static readonly Guid OfficeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    internal static readonly Guid RecipientId = Guid.Parse("22222222-2222-2222-2222-222222222222");
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

    internal static OfficeClaimProfile Profile()
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
            ReformStatus = R8ReformStatus.NotApplicableBeforeR8,
            AverageWageBandOption = new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 5),
            EvidenceDocumentId = "profile-doc",
            ConfirmedAt = Now,
            ConfirmedBy = "admin",
            ConfirmationReason = "台帳確認",
            CreatedAt = Now,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };
    }

    internal static ClaimInput Input()
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
            CreatedAt = Now,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };
    }

    internal static CertificateClaimEvidence Evidence()
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
    }

    internal static Certificate Certificate() => Domain.Entities.Certificate.Create(
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
        paymentBurden: PaymentBurdenCategory.General2);

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

    internal static ClaimCalculationSnapshot Snapshot(int billedDays = 2) => new(
        [RecipientId],
        Profile(),
        [Input()],
        new Dictionary<Guid, CertificateClaimEvidence> { [RecipientId] = Evidence() },
        [AverageWageEvidence()],
        new Dictionary<Guid, int> { [RecipientId] = billedDays },
        new Dictionary<Guid, int> { [RecipientId] = 1 },
        new Dictionary<Guid, Certificate> { [RecipientId] = Certificate() });

    /// <summary>
    /// Task 12（ADR 0022）: <see cref="PaymentBurdenCategory"/>→burden-caps.json正準keyの対応。
    /// productionと同じ完全一致表を合成語彙として複製する。
    /// </summary>
    private static readonly IReadOnlyDictionary<PaymentBurdenCategory, string> BurdenCategoryTokens =
        new Dictionary<PaymentBurdenCategory, string>
        {
            [PaymentBurdenCategory.Welfare] = "welfare",
            [PaymentBurdenCategory.LowIncome] = "low-income",
            [PaymentBurdenCategory.General1] = "general-1",
            [PaymentBurdenCategory.General2] = "general-2",
        };

    internal static ClaimBillingConditionTokens Tokens() =>
        new("b-type", "region-a", "b-type", 20, "staff-a", BurdenCategoryTokens: BurdenCategoryTokens);

    internal static ClaimMasterRelease Release() =>
        new(new ClaimMasterVersion("master-v1"), new ServiceMonth(2024, 4), null, ["doc-1"]);

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
        // Task 13（ADR 0023）: 経過措置guard（ClaimPreviewPipeline）が要求する版付き許可
        // option集合の合成行。Profile()の宣言（master-v1 / NotApplicableBeforeR8 / Numeric(5)）
        // と一致する。
        TransitionRules:
        [
            new OfficeClaimProfileTransitionRuleMasterRow(
                "transition-a",
                new ClaimMasterVersion("master-v1"),
                [new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 5)],
                new Dictionary<R8ReformStatus, IReadOnlyCollection<AverageWageBandOption>>
                {
                    [R8ReformStatus.NotApplicableBeforeR8] =
                        [new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 5)],
                },
                new DateOnly(2026, 6, 1),
                FiledTransitionExclusiveEndRule.AddYearsExclusive,
                1,
                new ServiceMonth(2024, 4),
                null,
                [SourceRef()]),
        ],
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

    internal sealed class MutableSnapshotReader(ClaimCalculationSnapshot snapshot)
        : IClaimCalculationSnapshotReader
    {
        public ClaimCalculationSnapshot Snapshot { get; set; } = snapshot;

        public Task<ClaimCalculationSnapshot> ReadAsync(
            Guid officeId, ServiceMonth serviceMonth, CancellationToken ct)
            => Task.FromResult(Snapshot);
    }

    internal sealed class MutableMasterProvider(
        ClaimMasterRelease? release, ClaimCalculationMasterBundle? masters) : IClaimMasterProvider
    {
        public ClaimMasterRelease? Release { get; set; } = release;
        public ClaimCalculationMasterBundle? Masters { get; set; } = masters;

        public ClaimMasterRelease ResolveVersion(ServiceMonth serviceMonth)
            => Release ?? throw new ClaimMasterPolicyUnavailableException(
                ClaimMasterPolicyUnavailableCode.Unavailable);

        public ClaimCalculationMasterBundle ResolveCalculationMasters(ServiceMonth serviceMonth)
            => Masters ?? throw new ClaimMasterPolicyUnavailableException(
                ClaimMasterPolicyUnavailableCode.Unavailable);
    }

    internal sealed class FakeOfficeRepository(Office office) : IOfficeRepository
    {
        public Task AddAsync(Office entity, CancellationToken ct) => throw new NotSupportedException();

        public Task<Office?> FindByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(id == office.Id ? office : null);

        public Task<Office?> FindByNumberAsync(string officeNumber, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task UpdateAsync(Office entity, CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<Office>> ListAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Office>>([office]);
    }

    internal sealed class FakeTokenProvider(ClaimBillingConditionTokens tokens) : IClaimBillingTokenProvider
    {
        public ClaimBillingConditionTokens Resolve(
            Office office, OfficeClaimProfile? profile, ServiceMonth serviceMonth) => tokens;
    }

    internal sealed class EmptyRequirementProvider : IClaimInputRequirementProvider
    {
        public IReadOnlyList<ClaimInputRequirement> GetRequirements() => [];
    }

    /// <summary>
    /// IClaimBatchRepository と IClaimFinalizationStore を同時に受け持つ結合フェイク。
    /// CommitAsync で受け取ったdraftをそのままheader/detailへ写像して蓄積することで、
    /// 「確定→履歴照会」がVM経由で一貫することを検証できるようにする。
    /// </summary>
    internal sealed class FakeClaimBatchStore : IClaimBatchRepository, IClaimFinalizationStore
    {
        private readonly List<ClaimBatchAggregate> _aggregates = [];

        public IReadOnlyList<ClaimBatchAggregate> Aggregates => _aggregates;

        public Task<IReadOnlyList<ClaimBatchAggregate>> ListHistoryAggregatesAsync(
            Guid officeId, ServiceMonth serviceMonth, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ClaimBatchAggregate>>(_aggregates
                .Where(aggregate => aggregate.Header.OfficeId == officeId
                    && aggregate.Header.ServiceMonth == serviceMonth)
                .ToArray());

        public Task<ClaimBatchAggregate?> FindByOperationIdAsync(
            Guid finalizationOperationId, CancellationToken ct)
            => Task.FromResult(_aggregates.FirstOrDefault(
                aggregate => aggregate.Header.FinalizationOperationId == finalizationOperationId));

        public Task<ClaimFinalizationResult> CommitAsync(ClaimFinalizationDraft draft, CancellationToken ct)
        {
            var revision = _aggregates.Count == 0 ? 1 : _aggregates[^1].Header.Revision + 1;
            var header = new ClaimBatch
            {
                Id = Guid.NewGuid(),
                OfficeId = draft.OfficeId,
                ServiceMonth = draft.ServiceMonth,
                Revision = revision,
                Kind = draft.Kind,
                OriginId = draft.RootBatchId,
                ExpectedHeadBatchId = draft.ExpectedHead?.BatchId,
                ExpectedHeadRevision = draft.ExpectedHead?.Revision,
                TotalUnits = draft.TotalUnits,
                TotalCostYen = draft.TotalCostYen,
                TotalBenefitYen = draft.TotalBenefitYen,
                TotalBurdenYen = draft.TotalBurdenYen,
                ClaimMasterVersion = draft.ClaimMasterVersion,
                CsvSpecificationVersion = draft.CsvSpecificationVersion,
                ReportSpecificationVersion = draft.ReportSpecificationVersion,
                SnapshotApplicationVersion = draft.SnapshotApplicationVersion,
                OperationApplicationVersion = draft.OperationApplicationVersion,
                FinalizationOperationId = draft.FinalizationOperationId,
                OperationPayloadSchemaVersion = ClaimBatch.CurrentOperationPayloadSchemaVersion,
                OperationPayloadSha256 = new string('0', 64),
                CreatedAt = Now,
                CreatedBy = draft.CreatedBy,
                ConcurrencyToken = Guid.NewGuid(),
            };
            var details = draft.Details.Select(detail => new ClaimDetail
            {
                Id = Guid.NewGuid(),
                ClaimBatchId = header.Id,
                RecipientId = detail.RecipientId,
                SnapshotSchemaVersion = detail.SnapshotSchemaVersion,
                ClaimMasterVersion = detail.ClaimMasterVersion,
                CsvSpecificationVersion = detail.CsvSpecificationVersion,
                ReportSpecificationVersion = detail.ReportSpecificationVersion,
                SnapshotApplicationVersion = detail.SnapshotApplicationVersion,
                InputSnapshotJson = "{}",
                CalculationSnapshotJson = "{}",
                TotalUnits = detail.TotalUnits,
                TotalCostYen = detail.TotalCostYen,
                BenefitYen = detail.BenefitYen,
                BurdenYen = detail.BurdenYen,
                CreatedAt = header.CreatedAt,
                CreatedBy = header.CreatedBy,
                ConcurrencyToken = Guid.NewGuid(),
            }).ToArray();
            _aggregates.Add(new ClaimBatchAggregate(header, details));
            return Task.FromResult(new ClaimFinalizationResult(header.Id, header.Revision, IsReplay: false));
        }
    }
}
