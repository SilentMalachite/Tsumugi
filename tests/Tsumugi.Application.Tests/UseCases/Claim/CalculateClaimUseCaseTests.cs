using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Logic.Claim.Models;
using Xunit;
using Kit = Tsumugi.Application.Tests.UseCases.Claim.ClaimPreparationTestKit;

namespace Tsumugi.Application.Tests.UseCases.Claim;

public sealed class CalculateClaimUseCaseTests
{
    [Fact]
    public async Task Execute_returns_issues_and_skips_calculation_when_not_ready()
    {
        // (a) profileなし → readiness不成立。算定マスタの解決自体が呼ばれないことを確認する。
        var masterProvider = new Kit.FakeMasterProvider(Kit.Release(), Kit.SyntheticMasters());
        var useCase = CreateUseCase(
            Kit.Snapshot(includeProfile: false), masterProvider: masterProvider);

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        dto.IsReady.Should().BeFalse();
        dto.PreviewHash.Should().BeEmpty();
        dto.Details.Should().BeEmpty();
        dto.TotalUnits.Should().Be(0);
        dto.Issues.Should().Contain(issue =>
            issue.Code == ClaimPreparationIssueCode.MissingRequiredEvidence
            && issue.FieldCode == "OfficeClaimProfile.Effective");
        masterProvider.CalculationMasterResolutions.Should().Be(0);
    }

    [Fact]
    public async Task Execute_maps_calculator_result_when_ready()
    {
        // (b) 700単位/日 × 2日 = 1,400単位 → ×10.00円 = 14,000円 → 1割相当1,400円(上限9,300円未満)
        var useCase = CreateUseCase(Kit.Snapshot());

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        dto.IsReady.Should().BeTrue();
        dto.Issues.Should().BeEmpty();
        dto.ServiceMonth.Should().Be(Kit.Month);
        dto.ClaimMasterVersion.Should().Be("master-v1");
        dto.PreviewHash.Should().MatchRegex("^[0-9a-f]{64}$");
        var detail = dto.Details.Should().ContainSingle().Subject;
        detail.RecipientId.Should().Be(Kit.RecipientId);
        detail.ServiceCode.Should().Be("610000");
        detail.BilledDays.Should().Be(2);
        detail.TotalUnits.Should().Be(1400);
        detail.TotalCostYen.Should().Be(14000);
        detail.BurdenYen.Should().Be(1400);
        detail.BenefitYen.Should().Be(12600);
        dto.TotalUnits.Should().Be(1400);
        dto.TotalCostYen.Should().Be(14000);
        dto.TotalBenefitYen.Should().Be(12600);
        dto.TotalBurdenYen.Should().Be(1400);
    }

    [Fact]
    public async Task Execute_fails_closed_when_the_month_has_no_transition_rule_for_the_profile_version()
    {
        // Task 13 (ADR 0023): 対象月のマスタ束にprofileのmaster版へ対応する経過措置rule
        // （transition-rules行）が無ければ算定しない（例: R8施行後にR6版profileが残留し
        // snapshot readerが期限切れprofileを返した場合の無検証単価請求を遮断する）。
        var useCase = CreateUseCase(
            Kit.Snapshot(),
            masterProvider: new Kit.FakeMasterProvider(
                Kit.Release(), Kit.SyntheticMasters(includeTransitionRule: false)));

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        dto.IsReady.Should().BeFalse();
        dto.Details.Should().BeEmpty();
        dto.PreviewHash.Should().BeEmpty();
        dto.Issues.Should().ContainSingle(issue =>
            issue.Code == ClaimPreparationIssueCode.ReformTransitionMismatch
            && issue.FieldCode == "OfficeClaimProfile.MasterVersion");
    }

    [Fact]
    public async Task Execute_fails_closed_when_reform_status_and_band_option_disagree_with_the_month_rule()
    {
        // Task 13 (ADR 0023): R8ReformStatusと版付き許可option集合の不一致はフェイルクローズ
        // （合成ruleはNotApplicableBeforeR8だけへoptionを対応付けており、ReformExemptの
        // 宣言は経過措置検証で算定不能になる）。
        var useCase = CreateUseCase(
            Kit.Snapshot(profile: Kit.Profile(reformStatus: R8ReformStatus.ReformExempt)));

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        dto.IsReady.Should().BeFalse();
        dto.Details.Should().BeEmpty();
        dto.Issues.Should().ContainSingle(issue =>
            issue.Code == ClaimPreparationIssueCode.ReformTransitionMismatch
            && issue.FieldCode == "OfficeClaimProfile.AverageWageBandOption");
    }

    // NOTE(teeth): 事前登録した将来版の不足は「警告」であって確定を止めない（ADR 0041）。
    // IsReady を将来版で落とす実装にすると、まだ施行前の版のせいで今月の請求が確定できなくなる。
    [Fact]
    public async Task Execute_warns_about_upcoming_versions_without_blocking_readiness()
    {
        var futureRequirement = new ClaimInputRequirement(
            "ContractedProvider.FirstServiceDate",
            ["provider:J121:02:008"],
            new ClaimRequirementCondition.Always(),
            ClaimInputDestination.Certificate);
        var readiness = new ClaimPreparationReadiness(
            new VersionedRequirementProvider(current: [], upcoming: [futureRequirement]));
        var versions = new Kit.FakeCsvSpecificationVersions { UpcomingVersions = ["r9-04"] };

        var dto = await new CalculateClaimUseCase(
                new Kit.FakeSnapshotReader(Kit.Snapshot()),
                new Kit.FakeMasterProvider(Kit.Release(), Kit.SyntheticMasters()),
                new Kit.FakeOfficeRepository(Kit.Office()),
                new Kit.FakeTokenProvider(Kit.Tokens()),
                readiness,
                new Kit.FakeGenericFieldCatalog(),
                versions)
            .ExecuteAsync(new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        dto.IsReady.Should().BeTrue("将来版の不足で今月の確定は止めない");
        dto.Issues.Should().BeEmpty();
        var warning = dto.UpcomingSpecificationIssues.Should().NotBeNull()
            .And.ContainSingle().Subject;
        warning.SpecificationVersion.Should().Be("r9-04");
        warning.Change.Should().Be(ClaimUpcomingSpecificationChange.BecomesRequired);
        warning.Issue.FieldCode.Should().Be("ContractedProvider.FirstServiceDate");
    }

    // NOTE(teeth): 条件が緩む方向（現行版では必須だが次の施行分では不要）も示す。伏せると運用者は
    // 「次の施行分まで待てば入力不要な項目」のために入力させられていることに気付けない。
    // ただし**今月の確定は止め続ける**（現行版の適用期間内に提出するなら必要な項目のため。
    // 自動で緩めると提出先で弾かれる）。
    [Fact]
    public async Task Execute_reports_requirements_that_the_upcoming_version_drops()
    {
        var currentOnlyRequirement = new ClaimInputRequirement(
            "ContractedProvider.FirstServiceDate",
            ["provider:J121:02:008"],
            new ClaimRequirementCondition.Always(),
            ClaimInputDestination.Certificate);
        var readiness = new ClaimPreparationReadiness(
            new VersionedRequirementProvider(current: [currentOnlyRequirement], upcoming: []));
        var versions = new Kit.FakeCsvSpecificationVersions { UpcomingVersions = ["r9-04"] };

        var dto = await new CalculateClaimUseCase(
                new Kit.FakeSnapshotReader(Kit.Snapshot()),
                new Kit.FakeMasterProvider(Kit.Release(), Kit.SyntheticMasters()),
                new Kit.FakeOfficeRepository(Kit.Office()),
                new Kit.FakeTokenProvider(Kit.Tokens()),
                readiness,
                new Kit.FakeGenericFieldCatalog(),
                versions)
            .ExecuteAsync(new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        dto.IsReady.Should().BeFalse("現行版で必須な項目が欠けている間は確定させない");
        dto.Issues.Should().Contain(issue =>
            issue.FieldCode == "ContractedProvider.FirstServiceDate");
        var change = dto.UpcomingSpecificationIssues.Should().NotBeNull()
            .And.ContainSingle().Subject;
        change.SpecificationVersion.Should().Be("r9-04");
        change.Change.Should().Be(ClaimUpcomingSpecificationChange.BecomesOptional);
        change.Issue.FieldCode.Should().Be("ContractedProvider.FirstServiceDate");
    }

    [Fact]
    public async Task Execute_reports_both_directions_of_an_upcoming_change()
    {
        // 同じ月で「新版で必要になる項目」と「新版では不要になる項目」が同時に起きる形。
        var droppedByUpcoming = new ClaimInputRequirement(
            "ContractedProvider.FirstServiceDate",
            ["provider:J121:02:008"],
            new ClaimRequirementCondition.Always(),
            ClaimInputDestination.Certificate);
        var addedByUpcoming = new ClaimInputRequirement(
            "ClaimInput.OffsiteSupportCumulativeDays",
            ["provider:J611:01:054"],
            new ClaimRequirementCondition.Always(),
            ClaimInputDestination.ClaimInput);
        var readiness = new ClaimPreparationReadiness(
            new VersionedRequirementProvider(current: [droppedByUpcoming], upcoming: [addedByUpcoming]));
        var versions = new Kit.FakeCsvSpecificationVersions { UpcomingVersions = ["r9-04"] };

        var dto = await new CalculateClaimUseCase(
                new Kit.FakeSnapshotReader(Kit.Snapshot()),
                new Kit.FakeMasterProvider(Kit.Release(), Kit.SyntheticMasters()),
                new Kit.FakeOfficeRepository(Kit.Office()),
                new Kit.FakeTokenProvider(Kit.Tokens()),
                readiness,
                new Kit.FakeGenericFieldCatalog(),
                versions)
            .ExecuteAsync(new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        dto.UpcomingSpecificationIssues.Should().NotBeNull();
        dto.UpcomingSpecificationIssues!.Select(change => (change.Change, change.Issue.FieldCode))
            .Should().BeEquivalentTo(new[]
            {
                (ClaimUpcomingSpecificationChange.BecomesRequired, "ClaimInput.OffsiteSupportCumulativeDays"),
                (ClaimUpcomingSpecificationChange.BecomesOptional, "ContractedProvider.FirstServiceDate"),
            });
    }

    // NOTE(teeth): 同じ項目で issue code だけが変わる場合は「変化なし」として扱う（両方向に出さない）。
    [Fact]
    public async Task Execute_does_not_report_a_change_when_only_the_issue_code_differs()
    {
        var sameTargetDifferentCondition = new ClaimInputRequirement(
            "ContractedProvider.FirstServiceDate",
            ["provider:J121:02:008"],
            // 条件の参照先が値辞書に無い path なので Unresolved 側の code になる。
            new ClaimRequirementCondition.ModelPresent("ClaimInput.NotAPath"),
            ClaimInputDestination.Certificate);
        var alwaysRequired = new ClaimInputRequirement(
            "ContractedProvider.FirstServiceDate",
            ["provider:J121:02:008"],
            new ClaimRequirementCondition.Always(),
            ClaimInputDestination.Certificate);
        var readiness = new ClaimPreparationReadiness(new VersionedRequirementProvider(
            current: [alwaysRequired], upcoming: [sameTargetDifferentCondition]));
        var versions = new Kit.FakeCsvSpecificationVersions { UpcomingVersions = ["r9-04"] };

        var dto = await new CalculateClaimUseCase(
                new Kit.FakeSnapshotReader(Kit.Snapshot()),
                new Kit.FakeMasterProvider(Kit.Release(), Kit.SyntheticMasters()),
                new Kit.FakeOfficeRepository(Kit.Office()),
                new Kit.FakeTokenProvider(Kit.Tokens()),
                readiness,
                new Kit.FakeGenericFieldCatalog(),
                versions)
            .ExecuteAsync(new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        dto.UpcomingSpecificationIssues.Should().BeEmpty();
    }

    /// <summary>現行版は要件なし、将来版だけ要件を返すフェイク。</summary>
    private sealed class VersionedRequirementProvider(
        IReadOnlyList<ClaimInputRequirement> current,
        IReadOnlyList<ClaimInputRequirement> upcoming) : IClaimInputRequirementProvider
    {
        public IReadOnlyList<ClaimInputRequirement> GetRequirements(string specificationVersion) =>
            string.Equals(specificationVersion, "r9-04", StringComparison.Ordinal) ? upcoming : current;
    }

    [Fact]
    public async Task Execute_produces_stable_preview_hash_for_identical_input()
    {
        // (c) 同一入力 → 同一hash（インスタンスをまたいでも決定論的）。
        var first = await CreateUseCase(Kit.Snapshot()).ExecuteAsync(
            new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);
        var second = await CreateUseCase(Kit.Snapshot()).ExecuteAsync(
            new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        second.PreviewHash.Should().Be(first.PreviewHash);
    }

    [Fact]
    public async Task Execute_changes_preview_hash_when_input_changes()
    {
        var baseline = await CreateUseCase(Kit.Snapshot()).ExecuteAsync(
            new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);
        var changed = await CreateUseCase(Kit.Snapshot(
                billedDays: new Dictionary<Guid, int> { [Kit.RecipientId] = 3 }))
            .ExecuteAsync(new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        changed.PreviewHash.Should().NotBe(baseline.PreviewHash);
    }

    // NOTE(teeth): 算定に効かない請求入力（訪問支援特別加算の算定回数・施設外支援の累計日数）も
    // PreviewHash に含める。含めないと、プレビュー後にこれらを書き換えても同じ hash で確定でき、
    // 確定 snapshot が「プレビューで見た内容」と食い違う。
    [Fact]
    public async Task Execute_changes_preview_hash_when_a_group_b_explicit_addition_input_changes()
    {
        var baseline = await CreateUseCase(Kit.Snapshot(inputs: [Kit.Input()])).ExecuteAsync(
            new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);
        var changed = await CreateUseCase(Kit.Snapshot(
                inputs: [Kit.Input() with { SpecialVisitSupportBilledCount = 2 }]))
            .ExecuteAsync(new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);
        var alsoChanged = await CreateUseCase(Kit.Snapshot(
                inputs: [Kit.Input() with { OffsiteSupportCumulativeDays = 17 }]))
            .ExecuteAsync(new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        baseline.PreviewHash.Should().MatchRegex("^[0-9a-f]{64}$");
        changed.PreviewHash.Should().NotBe(baseline.PreviewHash);
        alsoChanged.PreviewHash.Should().NotBe(baseline.PreviewHash);
        alsoChanged.PreviewHash.Should().NotBe(changed.PreviewHash);
    }

    [Fact]
    public async Task Execute_does_not_block_on_zero_activity_recipient_with_nothing_else()
    {
        // Task 9b: 契約により対象者集合には残るが、実績0日・ClaimInput未入力・証未登録の
        // 利用者は、readinessのブロック評価から除外されるため月全体は依然としてready。
        var snapshot = Kit.Snapshot(
            recipientIds: [Kit.RecipientId, Kit.SecondRecipientId],
            inputs: [Kit.Input()],
            evidenceByRecipient: new Dictionary<Guid, CertificateClaimEvidence>
            {
                [Kit.RecipientId] = Kit.Evidence(),
            },
            billedDays: new Dictionary<Guid, int>
            {
                [Kit.RecipientId] = 2,
                [Kit.SecondRecipientId] = 0,
            },
            certificateCounts: new Dictionary<Guid, int>
            {
                [Kit.RecipientId] = 1,
                [Kit.SecondRecipientId] = 0,
            });
        var useCase = CreateUseCase(snapshot);

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        dto.IsReady.Should().BeTrue();
        dto.Issues.Should().BeEmpty();
        dto.Details.Should().ContainSingle(detail => detail.RecipientId == Kit.RecipientId);
    }

    [Fact]
    public async Task Execute_still_blocks_when_the_same_recipient_has_billed_days_and_missing_evidence()
    {
        // 同じ利用者でも実績日数が1日以上ある月は除外対象外に戻り、証や入力の欠落が
        // そのままブロック要因として可視化される。
        var snapshot = Kit.Snapshot(
            recipientIds: [Kit.RecipientId, Kit.SecondRecipientId],
            inputs: [Kit.Input()],
            evidenceByRecipient: new Dictionary<Guid, CertificateClaimEvidence>
            {
                [Kit.RecipientId] = Kit.Evidence(),
            },
            billedDays: new Dictionary<Guid, int>
            {
                [Kit.RecipientId] = 2,
                [Kit.SecondRecipientId] = 1,
            },
            certificateCounts: new Dictionary<Guid, int>
            {
                [Kit.RecipientId] = 1,
                [Kit.SecondRecipientId] = 0,
            });
        var useCase = CreateUseCase(snapshot);

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        dto.IsReady.Should().BeFalse();
        dto.Issues.Should().Contain(issue => issue.RecipientId == Kit.SecondRecipientId);
    }

    [Fact]
    public async Task Execute_is_not_ready_when_region_key_sources_conflict()
    {
        // controller decision 2026-07-19 (Task 9b fix round): 地域区分の二重ソース不一致は
        // プレビュー全体をNotReadyにする（Finding 1: OfficeClaimBillingTokenProvider fail-closed）。
        var useCase = CreateUseCase(
            Kit.Snapshot(),
            tokens: Kit.Tokens(regionKey: null, regionKeyConflict: true));

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        dto.IsReady.Should().BeFalse();
        dto.Issues.Should().Contain(issue =>
            issue.Code == ClaimPreparationIssueCode.RegionKeySourceConflict
            && issue.FieldCode == "OfficeClaimProfile.RegionKey");
    }

    [Fact]
    public async Task Execute_reports_master_version_unavailable_month()
    {
        var useCase = CreateUseCase(
            Kit.Snapshot(),
            masterProvider: new Kit.FakeMasterProvider(release: null, masters: null));

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        dto.IsReady.Should().BeFalse();
        dto.ClaimMasterVersion.Should().BeEmpty();
        dto.Issues.Should().Contain(issue =>
            issue.Code == ClaimPreparationIssueCode.MasterVersionUnavailable);
    }

    // NOTE(teeth): 届け出たoptionに当月の行が無いのは「無音で0円」になる経路である。
    // IsReady を落とす実装にすると、期間境界をまたぐ正当な体制届まで確定できなくなる（ADR 0049）。
    [Fact]
    public async Task Execute_warns_about_declared_capabilities_without_master_rows_this_month()
    {
        // Kit.Tokens() が宣言する体制届キーに、当月の条件定義に無いキーを1件混ぜる。
        // Kit.SyntheticMasters() 側の条件定義には含めず、AllOfficeCapabilityConditionValues
        // には含める（＝他の期間では使われているキー）。
        var dto = await CreateUseCaseWithExpiredCapability()
            .ExecuteAsync(new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        dto.IsReady.Should().BeTrue("体制届optionの不一致で確定は止めない");
        dto.CapabilityCoverageWarnings.Should().ContainSingle()
            .Which.Should().Be("mhlw.b46.capability.treatment-improvement.6");
    }

    // NOTE(teeth): 「他の期間でも一切使われていないキー」は偽陽性になるため無視する
    // （legacyな体制届項目、例: mealProvision、が毎月ノイズを出さないようにする2段構えの検証）。
    [Fact]
    public async Task Execute_does_not_warn_about_a_declared_capability_never_referenced_by_any_condition()
    {
        const string legacyKey = "mealProvision";
        var snapshot = Kit.Snapshot(
            officeCapabilities:
            [
                Kit.Capability(flags: new Dictionary<string, bool> { [legacyKey] = true }),
            ]);
        // AllOfficeCapabilityConditionValuesにも一切含めない＝算定に関与しないキー。
        var masterProvider = new Kit.FakeMasterProvider(Kit.Release(), Kit.SyntheticMasters());
        var useCase = CreateUseCase(snapshot, masterProvider: masterProvider);

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        dto.IsReady.Should().BeTrue();
        dto.CapabilityCoverageWarnings.Should().BeEmpty(
            "算定に関与しない体制届項目は毎月ノイズを出してはならない");
    }

    // NOTE(teeth, review Important 1): 当月の条件定義に宣言キーが実在する経路を直接ピン止めする。
    // 既存の2テストはどちらもmonthConditionValuesが空のSyntheticMasters()しか使わないため、
    // month側の抽出式（kind比較・SelectManyのoperand分岐）を丸ごと[]へ固定する粗いmutationでも
    // 検出できなかった（Important 1レビュー指摘）。Kit.SyntheticMasters(coveredOfficeCapabilityKey:)
    // で当月にkind: office-capabilityの条件定義を実際に1件持たせ、月側抽出が正しく動くことを
    // 直接検査する。
    [Fact]
    public async Task Execute_does_not_warn_when_the_declared_capability_is_covered_this_month()
    {
        const string coveredKey = "mhlw.b46.capability.treatment-improvement.2";
        var snapshot = Kit.Snapshot(
            officeCapabilities:
            [
                Kit.Capability(flags: new Dictionary<string, bool> { [coveredKey] = true }),
            ]);
        var masterProvider = new Kit.FakeMasterProvider(
            Kit.Release(),
            Kit.SyntheticMasters(coveredOfficeCapabilityKey: coveredKey),
            allOfficeCapabilityConditionValues: new HashSet<string>(StringComparer.Ordinal) { coveredKey });
        var useCase = CreateUseCase(snapshot, masterProvider: masterProvider);

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        dto.IsReady.Should().BeTrue();
        dto.CapabilityCoverageWarnings.Should().BeEmpty(
            "当月の条件定義に実在するキーは警告しない（偽陽性を作ってはならない）");
    }

    // NOTE(teeth, review Important 2): 無関係な理由（事業所請求設定=profile未登録）でnot-readyな
    // 月でも、体制届カバレッジ警告は消えてはならない。ADR 0041のUpcomingSpecificationIssuesと
    // 同じ理由で、警告の計算をrequest構築の成否に連動させると、この経路（readinessが他の理由で
    // 落ちている月）で無音になってしまう（Important 2レビュー指摘）。
    [Fact]
    public async Task Execute_still_surfaces_capability_coverage_warnings_when_not_ready_for_an_unrelated_reason()
    {
        const string declaredKey = "mhlw.b46.capability.treatment-improvement.6";
        var snapshot = Kit.Snapshot(
            includeProfile: false,
            officeCapabilities:
            [
                Kit.Capability(flags: new Dictionary<string, bool> { [declaredKey] = true }),
            ]);
        var masterProvider = new Kit.FakeMasterProvider(
            Kit.Release(),
            Kit.SyntheticMasters(),
            allOfficeCapabilityConditionValues: new HashSet<string>(StringComparer.Ordinal) { declaredKey });
        var useCase = CreateUseCase(snapshot, masterProvider: masterProvider);

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        dto.IsReady.Should().BeFalse("事業所請求設定(profile)未登録という無関係な理由でnot-ready");
        dto.Issues.Should().Contain(issue =>
            issue.Code == ClaimPreparationIssueCode.MissingRequiredEvidence
            && issue.FieldCode == "OfficeClaimProfile.Effective");
        dto.CapabilityCoverageWarnings.Should().ContainSingle()
            .Which.Should().Be(declaredKey);
    }

    /// <summary>
    /// 体制届キー"mhlw.b46.capability.treatment-improvement.6"を宣言済みだが、
    /// Kit.SyntheticMasters()の条件定義には含まれない（＝当月に無い）。
    /// fake providerのAllOfficeCapabilityConditionValuesには含める（＝他の期間では使われている）。
    /// </summary>
    private static CalculateClaimUseCase CreateUseCaseWithExpiredCapability()
    {
        const string declaredKey = "mhlw.b46.capability.treatment-improvement.6";
        var snapshot = Kit.Snapshot(
            officeCapabilities:
            [
                Kit.Capability(flags: new Dictionary<string, bool> { [declaredKey] = true }),
            ]);
        var masterProvider = new Kit.FakeMasterProvider(
            Kit.Release(),
            Kit.SyntheticMasters(),
            allOfficeCapabilityConditionValues: new HashSet<string>(StringComparer.Ordinal) { declaredKey });

        return CreateUseCase(snapshot, masterProvider: masterProvider);
    }

    [Fact]
    public async Task Execute_rejects_invalid_request()
    {
        var useCase = CreateUseCase(Kit.Snapshot());

        await FluentActions.Invoking(() => useCase.ExecuteAsync(
                new CalculateClaimRequest(Guid.Empty, Kit.Month), CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>();
    }

    internal static CalculateClaimUseCase CreateUseCase(
        ClaimCalculationSnapshot snapshot,
        Kit.FakeMasterProvider? masterProvider = null,
        ClaimBillingConditionTokens? tokens = null,
        Domain.Entities.Office? office = null,
        bool officeMissing = false)
        => new(
            new Kit.FakeSnapshotReader(snapshot),
            masterProvider ?? new Kit.FakeMasterProvider(Kit.Release(), Kit.SyntheticMasters()),
            new Kit.FakeOfficeRepository(officeMissing ? null : office ?? Kit.Office()),
            new Kit.FakeTokenProvider(tokens ?? Kit.Tokens()),
            Kit.Readiness(),
            new Kit.FakeGenericFieldCatalog(),
            new Kit.FakeCsvSpecificationVersions());
}
