using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.ClaimMasters;
using Tsumugi.Infrastructure.Csv.Mapping;
using Xunit;

namespace Tsumugi.Infrastructure.Tests.Claim;

/// <summary>
/// Task 9b fix round (Finding 2): 既存のReady経路テストは全て<c>FakeTokenProvider</c>を使っており、
/// 本番の請求プレビュー配線（<see cref="JsonClaimMasterProvider.LoadEmbedded"/>の実R6マスタ・
/// 実<see cref="OfficeClaimBillingTokenProvider"/>・実<see cref="CalculateClaimUseCase"/>と
/// その内部pipeline・<see cref="ClaimPreparationReadiness"/>）を1本も通していなかった。
/// 本ファイルは、CompositionRootが組み立てる実装クラスをそのまま使い、
/// 「Readyになりうる」ことをtoken解決からサービスコード解決・金額算定まで一気通貫で証明する。
/// snapshot readerだけはfake（SQLite実装は<c>ClaimCalculationSnapshotReaderTests</c>で別途検証済み）。
/// </summary>
/// <remarks>
/// <see cref="ClaimInputRequirementProvider.LoadEmbedded"/>（CompositionRootが実際に使うtyped
/// requirement catalog）を字義通りそのまま<see cref="ClaimPreparationReadiness"/>へ渡すと、
/// このスライスでは<b>常に</b>NotReadyになる。catalogには<c>Certificate.MunicipalityNumber</c>
/// (always) / <c>Certificate.SubsidyMunicipalityNumber</c> / <c>Certificate.UpperLimitManagementProviderNumber</c>
/// (自己参照modelPresent) / <c>ContractedProvider.CertificateEntryNumber</c> (always) /
/// <c>DailyRecord.*</c> 8項目 / <c>IntensiveSupportEpisode.StartDate</c> の計13 target pathが含まれるが、
/// これらはCertificate・ContractedProvider・DailyRecord・IntensiveSupportEpisodeモデルのUI入力配線
/// （Phase 3-1計画Task 11、CLAUDE.md「現在地」に記載の通り本スライスでは未着手）に対応する値であり、
/// <see cref="ClaimPreparationContextBuilder"/>がこれらのモデルを一切写像しないため、
/// どんなsnapshotを与えても常にUnresolved/Missingとなり読み取り専用のfail-closedになる
/// （<see cref="Real_embedded_requirement_provider_currently_blocks_readiness_via_known_task11_gaps_only"/>で
/// この事実そのものを固定して検証する）。
/// これはTask 9b（region token二重ソース・トークン本番配線）のスコープではなく、Task 11の既知の
/// 未実装ギャップである。したがって「Ready担当パス」の2テストは、他の全Task 9テストと同じ規約
/// （<c>EmptyRequirementProvider</c>）でtyped requirementそのものをスコープ外に置き、
/// マスタ・トークン・算定という「Task 9bが実際に配線した本番の継ぎ目」だけを実データで検証する。
/// </remarks>
public sealed class ClaimPreviewProductionWiringTests
{
    private static readonly Guid OfficeId = Guid.NewGuid();
    private static readonly Guid RecipientId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2025, 4, 15, 1, 2, 3, TimeSpan.Zero);

    // R6報酬告示は3年間据え置き（次期改定はR9=令和9年）。2025-04はclaim-master-r7-01
    // (sources.json releases; effectiveFrom 2025-01〜2025-08)の対象月で、basic-rewards.json /
    // service-codes.json のR6報酬行（effectiveFrom 2024-04, effectiveTo未設定）がそのまま適用される。
    private static readonly ServiceMonth Month = new(2025, 4);

    // ADR 0027語彙・R6 service-codes.json実データ（就継ＢⅠ１５, workbook-order=38;row=31）:
    // conditionSelectors = [reward-system-employment-continuation-support-b, band-20000-25000,
    // cap-20-or-less, staff-6-1] は本テストのcapacity(15)/staffing(staff-6-1)/averageWageBand(5)の
    // 組み合わせに一意対応し、serviceCode=463004・baseUnits=726（b-basic.r6.cap-20-or-less
    // .band-20000-25000.staff-6-1）を持つ。region-unit-prices.jsonのregion-grade-1は
    // unitPriceYen=11.14。
    private const string ExpectedServiceCode = "463004";
    private const int ExpectedBaseUnitsPerDay = 726;
    private const decimal ExpectedRegionUnitPriceYen = 11.14m;
    private const int BilledDays = 10;

    [Fact]
    public async Task Real_production_wiring_reaches_ready_preview_with_the_expected_r6_service_code()
    {
        var useCase = CreateUseCase(BuildSnapshot(staffingKey: "staff-6-1"), new EmptyRequirementProvider());

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(OfficeId, Month), CancellationToken.None);

        dto.IsReady.Should().BeTrue();
        dto.Issues.Should().BeEmpty();
        dto.Details.Should().HaveCount(1);
        var detail = dto.Details[0];
        detail.RecipientId.Should().Be(RecipientId);
        detail.ServiceCode.Should().Be(ExpectedServiceCode);
        detail.TotalUnits.Should().Be(ExpectedBaseUnitsPerDay * BilledDays);
        detail.TotalUnits.Should().BeGreaterThan(0);
        detail.TotalCostYen.Should().Be(
            (int)decimal.Floor(detail.TotalUnits * ExpectedRegionUnitPriceYen));
        detail.TotalCostYen.Should().BeGreaterThan(0);
        // ADR 0025: 給付費＝総費用額－決定利用者負担額（別計算せず差分で検算する）。
        (detail.BenefitYen + detail.BurdenYen).Should().Be(detail.TotalCostYen);
        dto.TotalUnits.Should().Be(detail.TotalUnits);
        dto.TotalCostYen.Should().Be(detail.TotalCostYen);
        dto.TotalBenefitYen.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Real_production_wiring_flips_to_not_ready_when_staffing_key_is_blank()
    {
        // 対照実験: staffingKeyだけを空にする。実OfficeClaimBillingTokenProviderが本当に
        // profile.StaffingKeyを解決に使っている（スタブ固定値を返しているのではない）ことの証明。
        var useCase = CreateUseCase(BuildSnapshot(staffingKey: null), new EmptyRequirementProvider());

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(OfficeId, Month), CancellationToken.None);

        dto.IsReady.Should().BeFalse();
        dto.Details.Should().BeEmpty();
        dto.Issues.Should().Contain(issue =>
            issue.Code == ClaimPreparationIssueCode.MissingRequiredField
            && issue.FieldCode == "OfficeClaimProfile.StaffingClass"
            && issue.Destination == ClaimInputDestination.ClaimInput);
    }

    [Fact]
    public async Task Real_embedded_requirement_provider_currently_blocks_readiness_via_known_task11_gaps_only()
    {
        // CompositionRootが実際に使う埋め込みcatalogをそのまま使うテスト。Finding 2の
        // レビュー期待（IsReady==true）を字義通りには満たせないことをここで固定する
        // （本ファイル冒頭remarks参照）。Task 9bが配線した地域区分・定員・人員配置区分・
        // 平均工賃band・改定状況・証拠の各issueは1件も出ず、Task 11未着手分の13件だけが
        // 残ることを検証し、Task 9bの本番配線に新規の想定外issueが無いことを担保する。
        var useCase = CreateUseCase(BuildSnapshot(staffingKey: "staff-6-1"), ClaimInputRequirementProvider.LoadEmbedded());

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(OfficeId, Month), CancellationToken.None);

        dto.IsReady.Should().BeFalse();
        dto.Details.Should().BeEmpty();
        dto.Issues.Select(issue => issue.FieldCode).Should().BeEquivalentTo(
        [
            "Certificate.MunicipalityNumber",
            "Certificate.SubsidyMunicipalityNumber",
            "Certificate.UpperLimitManagementProviderNumber",
            "ContractedProvider.CertificateEntryNumber",
            "DailyRecord.EmergencyAdmissionApplied",
            "DailyRecord.IntensiveSupportApplied",
            "DailyRecord.MedicalCoordinationType",
            "DailyRecord.OffsiteSupportApplied",
            "DailyRecord.RegionalCollaborationApplied",
            "DailyRecord.ServiceEndTime",
            "DailyRecord.ServiceStartTime",
            "DailyRecord.SpecialVisitSupportMinutes",
            "DailyRecord.TrialUseSupportType",
            "IntensiveSupportEpisode.StartDate",
        ]);
        dto.Issues.Should().NotContain(issue =>
            issue.FieldCode == "OfficeClaimProfile.RegionKey"
            || issue.FieldCode == "OfficeClaimProfile.CapacityHeadcount"
            || issue.FieldCode == "OfficeClaimProfile.StaffingClass"
            || issue.FieldCode == "OfficeClaimProfile.AverageWageBandOption"
            || issue.FieldCode == "OfficeClaimProfile.ReformStatus"
            || issue.FieldCode == "Office.RegionGrade"
            || issue.FieldCode == "Office.ServiceCategory"
            || issue.FieldCode == ClaimPreparationReadiness_CertificateEvidenceField);
    }

    // ClaimPreparationReadinessの証拠系field codeはinternalのため、契約として同じ文字列をここに固定する。
    private const string ClaimPreparationReadiness_CertificateEvidenceField =
        "CertificateClaimEvidence.Effective";

    private static CalculateClaimUseCase CreateUseCase(
        ClaimCalculationSnapshot snapshot, IClaimInputRequirementProvider requirementProvider)
        => new(
            new FakeSnapshotReader(snapshot),
            JsonClaimMasterProvider.LoadEmbedded(),
            new FakeOfficeRepository(BuildOffice()),
            new OfficeClaimBillingTokenProvider(),
            new ClaimPreparationReadiness(requirementProvider));

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

    private static ClaimCalculationSnapshot BuildSnapshot(string? staffingKey)
    {
        var profileId = Guid.NewGuid();
        var profile = new OfficeClaimProfile
        {
            Id = profileId,
            OfficeId = OfficeId,
            EffectiveFrom = new DateOnly(2024, 4, 1),
            EffectiveTo = null,
            RootId = profileId,
            Revision = 1,
            Kind = RecordKind.New,
            MasterVersion = new ClaimMasterVersion("claim-master-r7-01"),
            ReformStatus = R8ReformStatus.NotApplicableBeforeR8,
            AverageWageBandOption = new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 5),
            EvidenceDocumentId = "profile-doc",
            ConfirmedAt = Now,
            ConfirmedBy = "admin",
            ConfirmationReason = "台帳確認",
            // ADR 0027語彙。region-grade-1はOffice.RegionGrade(Grade1)由来の既定と一致させ
            // （Finding 1のfail-closed対象外にする）、capacity/staffingはR6 service-codes.jsonの
            // cap-20-or-less・staff-6-1に対応する実データ。
            CapacityHeadcount = 15,
            StaffingKey = staffingKey,
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
            // Certificate.SubsidyMunicipalityNumber / Certificate.UpperLimitManagementProviderNumber
            // (Certificateモデルのフィールド。本スライスでは未配線) が常にUnresolvedになるぶん、
            // any(modelPresent(Certificate.*) | modelPresent(ClaimInput.*))の代替条件をClaimInput側の
            // 明示入力で満たす（本ファイル冒頭remarks参照）。値そのものは「上限額管理なし・
            // 自治体助成なし」を表す0/Result1で、Task 9bの検証対象ではない。
            UpperLimitManagementResult = Tsumugi.Domain.Logic.Claim.Models.UpperLimitManagementResult.Result1,
            UpperLimitManagedAmountYen = 0,
            MunicipalSubsidyAmountYen = 0,
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

        return new ClaimCalculationSnapshot(
            [RecipientId],
            profile,
            [claimInput],
            new Dictionary<Guid, CertificateClaimEvidence> { [RecipientId] = evidence },
            [averageWageEvidence],
            new Dictionary<Guid, int> { [RecipientId] = BilledDays },
            new Dictionary<Guid, int> { [RecipientId] = 1 });
    }

    /// <summary>
    /// snapshot readerだけはfake（SQLite実装は<c>ClaimCalculationSnapshotReaderTests</c>で別途
    /// 検証済み）。それ以外は全てCompositionRootと同じ実装クラス。
    /// </summary>
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

    /// <summary>
    /// Task 9系の全テストが使う規約と同じ「typed requirementは対象外」構成（本ファイル冒頭remarks参照）。
    /// </summary>
    private sealed class EmptyRequirementProvider : IClaimInputRequirementProvider
    {
        public IReadOnlyList<ClaimInputRequirement> GetRequirements() => [];
    }
}
