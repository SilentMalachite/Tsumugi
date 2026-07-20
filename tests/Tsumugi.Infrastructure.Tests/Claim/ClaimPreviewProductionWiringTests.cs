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
/// <para>
/// Task 9b時点では、<see cref="ClaimInputRequirementProvider.LoadEmbedded"/>（CompositionRootが実際に
/// 使うtyped requirement catalog）を字義通りそのまま<see cref="ClaimPreparationReadiness"/>へ渡すと
/// 常にNotReadyだった。catalogに含まれる<c>Certificate.MunicipalityNumber</c>(always) /
/// <c>Certificate.SubsidyMunicipalityNumber</c> / <c>Certificate.UpperLimitManagementProviderNumber</c>
/// (自己参照modelPresent) / <c>ContractedProvider.CertificateEntryNumber</c>(always) /
/// <c>DailyRecord.*</c> 9項目 / <c>IntensiveSupportEpisode.StartDate</c> の計14 target pathを、
/// <see cref="ClaimPreparationContextBuilder"/>が一切写像していなかったためである
/// （Task 11の既知ギャップ、Phase 3-1計画Task 9b修正ラウンドで発見）。
/// </para>
/// <para>
/// Task 9cでこの14 pathすべてを<c>ClaimCalculationSnapshot</c>の追加フィールド
/// （<c>EffectiveCertificateByRecipient</c> / <c>EffectiveContractedProviderByRecipient</c> /
/// <c>DailyRecordAggregateByRecipient</c> / <c>IntensiveSupportEpisodeStartDateByRecipient</c>）から
/// 写像するようになった。自己参照条件（modelPresent/modelNonZero/modelTrue/modelIn）を持つ12 pathは
/// 未入力ならNotApplicableとして扱われ問題にならない（元々任意項目）。常時必須（always）の
/// <c>Certificate.MunicipalityNumber</c>と<c>ContractedProvider.CertificateEntryNumber</c>の2 pathだけが
/// 実データを要求する。
/// <see cref="Real_embedded_requirement_provider_reaches_ready_preview_when_snapshot_is_fully_entered"/>
/// でReady到達を、
/// <see cref="Real_embedded_requirement_provider_reports_missing_certificate_municipality_number"/>
/// で該当欠落時の単一issueを検証する。
/// </para>
/// <para>
/// 「Ready担当パス」の最初の2テストは、他の全Task 9テストと同じ規約（<c>EmptyRequirementProvider</c>）で
/// typed requirementそのものをスコープ外に置き、マスタ・トークン・算定という
/// 「Task 9bが実際に配線した本番の継ぎ目」だけを実データで検証する。
/// </para>
/// <para>
/// Phase 3-2 Task 4 fix round: <c>DailyRecord.RecipientConfirmation</c>
/// （<c>report:service-performance:daily:016</c>）は当時<c>ClaimPreparationContextBuilder</c>が一切
/// 写像しておらず、かつ<c>rowScopes</c>が常に空集合だったため、<c>DailyRecord.*</c>の
/// <c>rowPresent(service-performance.daily)</c>系条件（daily:004/005/016）がすべて恒久的に
/// NotApplicableへ縮退していた（fail-closedのはずが実質fail-open）。本ラウンドで
/// <c>rowScopes</c>をbilledDays由来で母集団化し、<c>RecipientConfirmation</c>の縮約値写像を追加し、
/// daily:004/005の<c>requiredCondition</c>から自己参照<c>modelPresent</c>を除去した結果、
/// <c>DailyRecord.*</c>のtarget pathは10項目（うちRecipientConfirmationは自己参照ではなくbilledDays
/// 依存の新規必須path）となり、計15 target pathとなった。
/// </para>
/// <para>
/// Phase 3-2 Task 5: <c>Certificate.SubsidyMunicipalityNumber</c> /
/// <c>Certificate.UpperLimitManagementProviderNumber</c>自身の自己参照条件は、Task 4 fix roundの
/// レビューで指摘された通り恒久的にfail-open（deliberately weak、spec §10の「optional・null許容」に
/// 一致するため放置）。一方、<c>Certificate.UpperLimitManagementProviderNumber</c>が非nullのとき
/// <c>ClaimInput.UpperLimitManagementResult</c> / <c>UpperLimitManagedAmountYen</c>を必須化する
/// spec §10のクロスフィールド規則は、field-mapping-r7-10.json側の自己参照レグと
/// report-field-mapping-r8-06.json側のクロスフィールドレグが同一TargetPathへ合流し
/// <c>ClaimInputRequirementProvider.CreateRequirement</c>が<c>Any(...)</c>へラップすることで、
/// 既に実効fail-closedになっていた（未テストだったため本ラウンドで
/// <c>Real_embedded_requirement_provider_requires_upper_limit_management_result_when_provider_number_is_entered</c>
/// 等4テストで固定）。
/// </para>
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
    public async Task Real_production_wiring_bills_a_seeded_addition_when_the_capability_is_held()
    {
        // Task 11: 実seed（additions.json / service-codes.json / ADR 0028）と実provider経由で、
        // 体制届（ADR 0021の正式one-hotキー）を持つ事業所に福祉専門職員配置等加算(Ⅰ)
        // （466037、15単位/日）が加算されることを検証する。体制届が無い上のReadyテストは
        // 基本報酬のみ（totalsが変わらないこと）の対照実験になっている。
        // 選択番号5=(Ⅰ)（Task 11 fix round 1・2026-07-19、r6-capability-202404/202406/
        // r8-capability-202606を一次資料で検証。ADR 0028補記参照。旧値`.3`は実際には(Ⅱ)だった）。
        var capability = OfficeCapability.Create(
            Guid.NewGuid(),
            OfficeId,
            new DateRange(new DateOnly(2024, 4, 1), null),
            new Dictionary<string, bool>
            {
                ["mhlw.b46.capability.welfare-professional-staffing.5"] = true,
            },
            "tester",
            Now,
            Guid.NewGuid());
        var useCase = CreateUseCase(
            BuildSnapshot(staffingKey: "staff-6-1", officeCapabilities: [capability]),
            new EmptyRequirementProvider());

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(OfficeId, Month), CancellationToken.None);

        dto.IsReady.Should().BeTrue();
        dto.Issues.Should().BeEmpty();
        var detail = dto.Details.Should().ContainSingle().Subject;
        detail.TotalUnits.Should().Be((ExpectedBaseUnitsPerDay + 15) * BilledDays);
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
    public async Task Real_embedded_requirement_provider_reaches_ready_preview_when_snapshot_is_fully_entered()
    {
        // Task 9c: CompositionRootが実際に使う埋め込みcatalogをそのまま使い、Certificate.* /
        // ContractedProvider.* / DailyRecord.* / IntensiveSupportEpisode.StartDateの15 target path
        // すべてに実データを与えた「フル入力済みの事業所・月」がReadyへ到達することを検証する
        // （本ファイル冒頭remarks参照。以前はこの14 pathが未写像で常にNotReadyだった。Task 4 fix roundで
        // DailyRecord.RecipientConfirmationが加わり15 pathになった）。
        var useCase = CreateUseCase(
            BuildSnapshot(staffingKey: "staff-6-1"), ClaimInputRequirementProvider.LoadEmbedded());

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(OfficeId, Month), CancellationToken.None);

        dto.IsReady.Should().BeTrue();
        dto.Issues.Should().BeEmpty();
        dto.Details.Should().ContainSingle().Which.RecipientId.Should().Be(RecipientId);
    }

    [Fact]
    public async Task Real_embedded_requirement_provider_reports_missing_certificate_municipality_number()
    {
        // Task 9c: Certificate.MunicipalityNumberはcatalog上always必須（自己参照条件を持つ他の
        // 13 pathと違い、未入力でもNotApplicableへ逃げられない）。この1項目だけを欠落させると、
        // 他は全てフル入力のままでもその1件だけがissueとして残ることを検証する。
        var useCase = CreateUseCase(
            BuildSnapshot(staffingKey: "staff-6-1", certificateMunicipalityNumber: null),
            ClaimInputRequirementProvider.LoadEmbedded());

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(OfficeId, Month), CancellationToken.None);

        dto.IsReady.Should().BeFalse();
        dto.Details.Should().BeEmpty();
        dto.Issues.Should().ContainSingle(issue =>
            issue.Code == ClaimPreparationIssueCode.MissingRequiredField
            && issue.RecipientId == RecipientId
            && issue.FieldCode == "Certificate.MunicipalityNumber"
            && issue.Destination == ClaimInputDestination.Certificate);
    }

    [Fact]
    public async Task Real_embedded_requirement_provider_does_not_require_subsidy_municipality_number_when_absent()
    {
        // Phase 3-2 Task 5: SubsidyMunicipalityNumberはspec §10でoptional（null許容）。
        // 自己参照条件はfail-openのまま（deliberately weak）なので、他はフル入力のまま
        // これだけを欠落させてもReadyを維持することを実embedded catalogで確認する。
        var useCase = CreateUseCase(
            BuildSnapshot(staffingKey: "staff-6-1", certificateSubsidyMunicipalityNumber: null),
            ClaimInputRequirementProvider.LoadEmbedded());

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(OfficeId, Month), CancellationToken.None);

        dto.IsReady.Should().BeTrue();
        dto.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task
        Real_embedded_requirement_provider_does_not_require_upper_limit_management_fields_when_provider_number_is_absent()
    {
        // Phase 3-2 Task 5: UpperLimitManagementProviderNumberが未入力（上限額管理なし）なら、
        // ClaimInput.UpperLimitManagementResult / UpperLimitManagedAmountYenも未入力のままで
        // Readyを維持する（spec §10の必須化はProviderNumberが非nullのときだけ発火する）。
        var useCase = CreateUseCase(
            BuildSnapshot(
                staffingKey: "staff-6-1",
                certificateUpperLimitManagementProviderNumber: null,
                claimInputUpperLimitManagementResult: null,
                claimInputUpperLimitManagedAmountYen: null),
            ClaimInputRequirementProvider.LoadEmbedded());

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(OfficeId, Month), CancellationToken.None);

        dto.IsReady.Should().BeTrue();
        dto.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task
        Real_embedded_requirement_provider_requires_upper_limit_management_result_when_provider_number_is_entered()
    {
        // Phase 3-2 Task 5: UpperLimitManagementProviderNumberが入力済みなのに
        // ClaimInput.UpperLimitManagementResultが未入力だと、そのクロスフィールド規則
        // （report-field-mapping-r8-06.jsonのupper-limit-management:003）がfail-closedすることを
        // 実embedded catalogで検証する。自己参照レグ（provider:J121:01:016）単体では検知できない
        // ケースであり、Any(...)合流の実効性を証明する（brief記載のCRITICAL cascade risk対応）。
        var useCase = CreateUseCase(
            BuildSnapshot(staffingKey: "staff-6-1", claimInputUpperLimitManagementResult: null),
            ClaimInputRequirementProvider.LoadEmbedded());

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(OfficeId, Month), CancellationToken.None);

        dto.IsReady.Should().BeFalse();
        dto.Details.Should().BeEmpty();
        dto.Issues.Should().ContainSingle(issue =>
            issue.Code == ClaimPreparationIssueCode.MissingRequiredField
            && issue.RecipientId == RecipientId
            && issue.FieldCode == "ClaimInput.UpperLimitManagementResult"
            && issue.Destination == ClaimInputDestination.ClaimInput);
    }

    [Fact]
    public async Task
        Real_embedded_requirement_provider_requires_upper_limit_managed_amount_when_provider_number_is_entered()
    {
        // Phase 3-2 Task 5: 同上のクロスフィールド規則（upper-limit-management:004）を
        // UpperLimitManagedAmountYen側で検証する。
        var useCase = CreateUseCase(
            BuildSnapshot(staffingKey: "staff-6-1", claimInputUpperLimitManagedAmountYen: null),
            ClaimInputRequirementProvider.LoadEmbedded());

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(OfficeId, Month), CancellationToken.None);

        dto.IsReady.Should().BeFalse();
        dto.Details.Should().BeEmpty();
        dto.Issues.Should().ContainSingle(issue =>
            issue.Code == ClaimPreparationIssueCode.MissingRequiredField
            && issue.RecipientId == RecipientId
            && issue.FieldCode == "ClaimInput.UpperLimitManagedAmountYen"
            && issue.Destination == ClaimInputDestination.ClaimInput);
    }

    [Theory]
    [InlineData("DailyRecord.ServiceStartTime")]
    [InlineData("DailyRecord.ServiceEndTime")]
    [InlineData("DailyRecord.RecipientConfirmation")]
    public async Task Real_embedded_requirement_provider_reports_missing_daily_record_field_on_present_day(
        string expectedFieldCode)
    {
        // Phase 3-2 Task 4 fix round: ServiceStartTime/ServiceEndTime/RecipientConfirmationは
        // 当月にAttendance.Presentの日（billedDays>0、本フィクスチャの既定BilledDays=10）が
        // あれば必須（fail-closed）。他はフル入力のまま対象1項目だけを欠落させ、その1件だけが
        // issueとして残ることを検証する（rowScopes母集団化前は3項目とも恒久的にNotApplicableへ
        // 縮退し、この欠落を検出できなかった）。
        var dailyRecordAggregate = new ClaimDailyRecordAggregate(
            ServiceStartTime: expectedFieldCode == "DailyRecord.ServiceStartTime" ? null : new TimeOnly(9, 0),
            ServiceEndTime: expectedFieldCode == "DailyRecord.ServiceEndTime" ? null : new TimeOnly(15, 0),
            SpecialVisitSupportMinutesTotal: 30,
            OffsiteSupportApplied: true,
            MedicalCoordinationType: MedicalCoordinationType.TypeI,
            TrialUseSupportType: TrialUseSupportType.TypeI,
            RegionalCollaborationApplied: true,
            IntensiveSupportApplied: true,
            EmergencyAdmissionApplied: true,
            RecipientConfirmation: expectedFieldCode == "DailyRecord.RecipientConfirmation"
                ? RecipientConfirmationStatus.Unspecified
                : RecipientConfirmationStatus.Confirmed);
        var useCase = CreateUseCase(
            BuildSnapshot(staffingKey: "staff-6-1", dailyRecordAggregateOverride: dailyRecordAggregate),
            ClaimInputRequirementProvider.LoadEmbedded());

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(OfficeId, Month), CancellationToken.None);

        dto.IsReady.Should().BeFalse();
        dto.Details.Should().BeEmpty();
        dto.Issues.Should().ContainSingle(issue =>
            issue.Code == ClaimPreparationIssueCode.MissingRequiredField
            && issue.RecipientId == RecipientId
            && issue.FieldCode == expectedFieldCode
            && issue.Destination == ClaimInputDestination.DailyRecord);
    }

    [Fact]
    public async Task Real_embedded_requirement_provider_does_not_require_daily_record_fields_without_a_present_day()
    {
        // 対照実験: billedDays=0（当月にPresent日が無い）なら、ServiceStartTime等が未入力でも
        // rowPresent(service-performance.daily)がNotApplicableのままissueにならないこと
        // （必須になるのはAttendance.Presentの日がある月だけ）。
        var emptyDailyRecordAggregate = ClaimDailyRecordAggregate.Empty;
        var useCase = CreateUseCase(
            BuildSnapshot(
                staffingKey: "staff-6-1",
                dailyRecordAggregateOverride: emptyDailyRecordAggregate,
                billedDaysOverride: 0),
            ClaimInputRequirementProvider.LoadEmbedded());

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(OfficeId, Month), CancellationToken.None);

        dto.Issues.Should().NotContain(issue =>
            issue.Destination == ClaimInputDestination.DailyRecord);
    }

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

    private static ClaimCalculationSnapshot BuildSnapshot(
        string? staffingKey,
        string? certificateMunicipalityNumber = "131000",
        string? certificateSubsidyMunicipalityNumber = "132000",
        string? certificateUpperLimitManagementProviderNumber = "1310000099",
        UpperLimitManagementResult? claimInputUpperLimitManagementResult =
            UpperLimitManagementResult.Result1,
        int? claimInputUpperLimitManagedAmountYen = 0,
        IReadOnlyList<OfficeCapability>? officeCapabilities = null,
        ClaimDailyRecordAggregate? dailyRecordAggregateOverride = null,
        int? billedDaysOverride = null)
    {
        var profileId = Guid.NewGuid();
        var profile = new OfficeClaimProfile
        {
            Id = profileId,
            OfficeId = OfficeId,
            // Task 13（ADR 0023）: profileのMasterVersionは体制届optionのedition版
            // （claim-master-r6-06 = 2024-06〜2026-05の対象月をcover）であり、2025-04の
            // transition-rules行（経過措置guard）と一致させる。期間もedition窓へ合わせる。
            EffectiveFrom = new DateOnly(2024, 6, 1),
            EffectiveTo = new DateOnly(2026, 5, 31),
            RootId = profileId,
            Revision = 1,
            Kind = RecordKind.New,
            MasterVersion = new ClaimMasterVersion("claim-master-r6-06"),
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
            // 既定値は「上限額管理なし・自治体助成なし」を表す0/Result1で、Task 9b/9cの
            // 検証対象ではない（ClaimInput.UpperLimitManagementResult等はTask 9で既に写像済み）。
            // Phase 3-2 Task 5: certificate.UpperLimitManagementProviderNumberが非nullのときの
            // クロスフィールド必須化を検証するため外から差し替え可能にした。
            UpperLimitManagementResult = claimInputUpperLimitManagementResult,
            UpperLimitManagedAmountYen = claimInputUpperLimitManagedAmountYen,
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

        // Task 9c: Certificate.* / ContractedProvider.CertificateEntryNumber / DailyRecord.* /
        // IntensiveSupportEpisode.StartDateの14 target pathへ、「フル入力済みの事業所・月」を模した
        // 実データを与える。certificateMunicipalityNumberだけを外から差し替え可能にし、
        // always必須path単独の欠落テストに使う。
        var certificateId = Guid.NewGuid();
        var certificate = Certificate.Create(
            certificateId,
            RecipientId,
            "certificate-no-1",
            new DateRange(new DateOnly(2024, 4, 1), null),
            supplyDays: 23,
            monthlyCostCap: 37_200,
            municipality: "テスト市",
            "tester",
            Now,
            Guid.NewGuid(),
            municipalityNumber: certificateMunicipalityNumber,
            subsidyMunicipalityNumber: certificateSubsidyMunicipalityNumber,
            upperLimitManagementProviderNumber: certificateUpperLimitManagementProviderNumber,
            // Task 12（ADR 0022）: 負担区分の唯一の権威ソース。evidence.MonthlyCostCap(9300)と
            // 整合する一般1（区分上限9,300円）を用いる。
            paymentBurden: PaymentBurdenCategory.General1);

        var contractedProvider = ContractedProvider.Create(
            Guid.NewGuid(),
            certificateId,
            providerNumber: "1310000001", // BuildOffice()のOfficeNumberと一致（本事業所行）
            providerName: "テスト事業所",
            serviceCategory: "就労継続支援B型",
            contractedSupplyDays: 23,
            contractDate: new DateOnly(2024, 4, 1),
            "tester",
            Now,
            Guid.NewGuid(),
            certificateEntryNumber: 5);

        var dailyRecordAggregate = dailyRecordAggregateOverride ?? new ClaimDailyRecordAggregate(
            ServiceStartTime: new TimeOnly(9, 0),
            ServiceEndTime: new TimeOnly(15, 0),
            SpecialVisitSupportMinutesTotal: 30,
            OffsiteSupportApplied: true,
            MedicalCoordinationType: MedicalCoordinationType.TypeI,
            TrialUseSupportType: TrialUseSupportType.TypeI,
            RegionalCollaborationApplied: true,
            IntensiveSupportApplied: true,
            EmergencyAdmissionApplied: true,
            RecipientConfirmation: RecipientConfirmationStatus.Confirmed);

        return new ClaimCalculationSnapshot(
            [RecipientId],
            profile,
            [claimInput],
            new Dictionary<Guid, CertificateClaimEvidence> { [RecipientId] = evidence },
            [averageWageEvidence],
            new Dictionary<Guid, int> { [RecipientId] = billedDaysOverride ?? BilledDays },
            new Dictionary<Guid, int> { [RecipientId] = 1 },
            new Dictionary<Guid, Certificate> { [RecipientId] = certificate },
            new Dictionary<Guid, ContractedProvider> { [RecipientId] = contractedProvider },
            new Dictionary<Guid, ClaimDailyRecordAggregate> { [RecipientId] = dailyRecordAggregate },
            new Dictionary<Guid, DateOnly> { [RecipientId] = new DateOnly(2025, 1, 6) },
            officeCapabilities);
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
