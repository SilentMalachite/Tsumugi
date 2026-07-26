using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.Claim;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Application.UseCases.Office;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Domain.Enums;
using Kit = Tsumugi.App.Tests.ClaimPreparationViewModelTestKit;

namespace Tsumugi.App.Tests;

public sealed class ClaimPreparationViewModelTests
{
    [Fact]
    public async Task PreviewAsync_calls_use_case_and_publishes_preview_and_issues()
    {
        var fixture = CreateFixture();

        await fixture.Sut.PreviewAsync();

        fixture.Sut.Preview.Should().NotBeNull();
        fixture.Sut.Preview!.IsReady.Should().BeTrue();
        fixture.Sut.Issues.Should().BeEmpty();
        fixture.Sut.Preview.Details.Should().ContainSingle(
            detail => detail.RecipientId == Kit.RecipientId);
        fixture.Sut.ErrorMessage.Should().BeNull();
    }

    // ADR 0049: 体制届で宣言されたが当月に有効なマスタ行が無いキーは、UpcomingSpecificationWarnings
    // (ADR 0041)と同じく確定を止めない情報としてViewModelへ公開される。
    [Fact]
    public async Task PreviewAsync_surfaces_capability_coverage_warnings_without_blocking_readiness()
    {
        const string declaredKey = "mhlw.b46.capability.treatment-improvement.6";
        var fixture = CreateFixture();
        fixture.SnapshotReader.Snapshot = fixture.SnapshotReader.Snapshot with
        {
            OfficeCapabilities = [Kit.Capability(new Dictionary<string, bool> { [declaredKey] = true })],
        };
        fixture.MasterProvider.AllCapabilityValues = new HashSet<string>(StringComparer.Ordinal) { declaredKey };

        await fixture.Sut.PreviewAsync();

        fixture.Sut.Preview!.IsReady.Should().BeTrue("体制届optionの不一致で確定は止めない");
        fixture.Sut.CapabilityCoverageWarnings.Should().ContainSingle()
            .Which.Should().Be(declaredKey);
    }

    // 本タスク（ADR 0049の一般化）: キーは当月に有効だが、それを要求する行がすべて他の
    // capabilityキー（companion）も要求していて宣言集合では1行も成立しない場合。
    // CapabilityCoverageWarnings（失効・未施行）とは別枠のIncompleteCapabilityDeclarationWarnings
    // として公開される。
    [Fact]
    public async Task PreviewAsync_surfaces_incomplete_capability_declaration_warnings_without_blocking_readiness()
    {
        const string requiredKey = "mhlw.b46.capability.treatment-improvement.6";
        const string companionKey = "mhlw.b46.capability.treatment-improvement-v-band.3";
        var fixture = CreateFixture();
        fixture.SnapshotReader.Snapshot = fixture.SnapshotReader.Snapshot with
        {
            OfficeCapabilities = [Kit.Capability(new Dictionary<string, bool> { [requiredKey] = true })],
        };
        fixture.MasterProvider.Masters =
            Kit.MastersRequiringCapabilityCompanion(requiredKey, companionKey);

        await fixture.Sut.PreviewAsync();

        fixture.Sut.Preview!.IsReady.Should().BeTrue("体制届の宣言不足で確定は止めない");
        fixture.Sut.IncompleteCapabilityDeclarationWarnings.Should().ContainSingle()
            .Which.Should().Be(requiredKey);
        // 排反性: 既存のCapabilityCoverageWarnings（失効・未施行）はこのキーが当月に
        // 有効なため空のまま。
        fixture.Sut.CapabilityCoverageWarnings.Should().BeEmpty();
    }

    /// <summary>
    /// C1: 施設区分が未入力のまま施設区分条件を持つ行へ到達すると
    /// <c>ServiceCodeResolver</c> が <c>FacilityClassificationUnresolved</c> を投げる。
    /// ViewModel の例外フィルタがこれを受けないと、<c>AsyncRelayCommand</c> は
    /// <c>FlowExceptionsToTaskScheduler</c> 無しで生成されており、グローバルハンドラも
    /// 無いため**アプリが終了する**。入力すべき欄（施設区分）を名指しする固定文言へ写像する。
    /// </summary>
    [Fact]
    public async Task PreviewAsync_maps_an_unresolved_facility_classification_to_a_message_naming_the_field()
    {
        var fixture = CreateFixture();
        fixture.MasterProvider.Masters = Kit.MastersRequiringFacilityClassification();

        var act = async () => await fixture.Sut.PreviewAsync();

        await act.Should().NotThrowAsync(
            "未捕捉例外はAsyncRelayCommandからアプリの終了になる");
        fixture.Sut.ErrorMessage.Should().Be(
            "施設区分が未入力です。事業所請求設定で施設区分を入力してから、もう一度実行してください。");
        fixture.Sut.Preview.Should().BeNull();
    }

    /// <summary>
    /// C1: 確定（<c>CloseAsync</c>）も同じ算定経路を通るため、同じ例外フィルタで受ける。
    /// </summary>
    [Fact]
    public async Task CloseAsync_maps_an_unresolved_facility_classification_instead_of_terminating()
    {
        var fixture = CreateFixture();
        await fixture.Sut.PreviewAsync();
        fixture.MasterProvider.Masters = Kit.MastersRequiringFacilityClassification();

        var act = async () => await fixture.Sut.CloseAsync();

        await act.Should().NotThrowAsync();
        fixture.Sut.ErrorMessage.Should().Be(
            "施設区分が未入力です。事業所請求設定で施設区分を入力してから、もう一度実行してください。");
    }

    [Fact]
    public async Task PreviewAsync_without_office_selected_sets_context_required_message()
    {
        var fixture = CreateFixture();
        fixture.Sut.OfficeId = Guid.Empty;

        await fixture.Sut.PreviewAsync();

        fixture.Sut.ErrorMessage.Should().Be("事業所と対象月を選択してください。");
        fixture.Sut.Preview.Should().BeNull();
    }

    [Fact]
    public async Task CloseCommand_is_disabled_when_preview_is_not_ready()
    {
        var fixture = CreateFixture();
        // 事業所請求設定(Profile)なし → readiness不成立でIsReady=falseになる。
        fixture.SnapshotReader.Snapshot = fixture.SnapshotReader.Snapshot with { Profile = null };

        await fixture.Sut.PreviewAsync();

        fixture.Sut.Preview!.IsReady.Should().BeFalse();
        fixture.Sut.CloseCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task CloseAsync_success_refreshes_history_and_clears_stale_preview()
    {
        var fixture = CreateFixture();
        await fixture.Sut.PreviewAsync();

        await fixture.Sut.CloseAsync();

        fixture.Sut.Preview.Should().BeNull();
        fixture.Sut.Issues.Should().BeEmpty();
        fixture.Sut.History.Should().ContainSingle(
            item => item.Revision == 1 && item.Kind == RecordKind.New);
        fixture.Sut.ErrorMessage.Should().BeNull();
        fixture.Sut.CancelCommand.CanExecute(null).Should().BeTrue();
        // Task 14: 確定によりReportSectionへ確定済revisionの有無と受給者候補が反映される。
        fixture.Sut.ReportSection.HasFinalizedRevision.Should().BeTrue();
        fixture.Sut.ReportSection.Recipients.Should().ContainSingle(
            option => option.RecipientId == Kit.RecipientId && option.KanjiName == "テスト利用者");
    }

    [Fact]
    public async Task PreviewAsync_failure_retains_previous_preview_and_reports_typed_error()
    {
        var fixture = CreateFixture();
        await fixture.Sut.PreviewAsync();
        var retainedPreview = fixture.Sut.Preview;
        // 版メタデータは解決できるが算定マスタ束が引けない状態（未整備）を再現する。
        fixture.MasterProvider.Masters = null;

        await fixture.Sut.PreviewAsync();

        fixture.Sut.Preview.Should().BeSameAs(retainedPreview);
        fixture.Sut.ErrorMessage.Should().Be("請求制度マスターを利用できません。");
    }

    [Fact]
    public async Task CancelAsync_without_active_history_sets_error_message_and_skips_use_case()
    {
        var fixture = CreateFixture();

        await fixture.Sut.CancelAsync();

        fixture.Sut.ErrorMessage.Should().Be("取下げ対象の確定請求がありません。");
        fixture.BatchStore.Aggregates.Should().BeEmpty();
    }

    [Fact]
    public async Task CancelAsync_after_close_succeeds_and_records_cancel_revision()
    {
        var fixture = CreateFixture();
        await fixture.Sut.PreviewAsync();
        await fixture.Sut.CloseAsync();

        await fixture.Sut.CancelAsync();

        fixture.Sut.History.Should().Contain(
            item => item.Revision == 2 && item.Kind == RecordKind.Cancel);
        fixture.Sut.CancelCommand.CanExecute(null).Should().BeFalse();
        fixture.Sut.ErrorMessage.Should().BeNull();
        // Task 14: 取下げ後は最新履歴がCancelになるため帳票出力コマンドも再び無効化される。
        fixture.Sut.ReportSection.HasFinalizedRevision.Should().BeFalse();
        fixture.Sut.ReportSection.Recipients.Should().BeEmpty();
    }

    [Fact]
    public async Task CloseAsync_hash_mismatch_sets_conflict_message_suggesting_re_preview()
    {
        var fixture = CreateFixture();
        await fixture.Sut.PreviewAsync();
        // プレビュー後に基となる実績が変わり、確定時の再算定hashが食い違う状況を再現する。
        fixture.SnapshotReader.Snapshot = Kit.Snapshot(billedDays: 3);

        await fixture.Sut.CloseAsync();

        fixture.Sut.ErrorMessage.Should().NotBeNull();
        fixture.Sut.ErrorMessage.Should().Contain("再度プレビュー");
        fixture.BatchStore.Aggregates.Should().BeEmpty();
    }

    [Fact]
    public async Task Changing_office_clears_preview_history_and_error()
    {
        var fixture = CreateFixture();
        await fixture.Sut.PreviewAsync();
        await fixture.Sut.CloseAsync();

        fixture.Sut.OfficeId = Guid.NewGuid();

        fixture.Sut.Preview.Should().BeNull();
        fixture.Sut.Issues.Should().BeEmpty();
        fixture.Sut.History.Should().BeEmpty();
        fixture.Sut.ErrorMessage.Should().BeNull();
        fixture.Sut.CancelCommand.CanExecute(null).Should().BeFalse();
        // Task 14: 事業所変更で帳票出力セクションも確定済revisionなし相当へ戻る。
        fixture.Sut.ReportSection.HasFinalizedRevision.Should().BeFalse();
        fixture.Sut.ReportSection.Recipients.Should().BeEmpty();
    }

    [Fact]
    public async Task InitializeAsync_loads_offices_from_use_case()
    {
        var fixture = CreateFixture();

        await fixture.Sut.InitializeAsync();

        fixture.Sut.Offices.Should().ContainSingle(office => office.Id == Kit.OfficeId);
    }

    private static Fixture CreateFixture()
    {
        var snapshotReader = new Kit.MutableSnapshotReader(Kit.Snapshot());
        var masterProvider = new Kit.MutableMasterProvider(Kit.Release(), Kit.SyntheticMasters());
        var officeRepository = new Kit.FakeOfficeRepository(Kit.Office());
        var tokenProvider = new Kit.FakeTokenProvider(Kit.Tokens());
        var readiness = new ClaimPreparationReadiness(new Kit.EmptyRequirementProvider());
        var batchStore = new Kit.FakeClaimBatchStore();
        var csvVersions = new Kit.FakeCsvSpecificationVersions();

        var genericFields = new Kit.FakeGenericFieldCatalog();
        var calculateClaim = new CalculateClaimUseCase(
            snapshotReader, masterProvider, officeRepository, tokenProvider, readiness,
            genericFields, csvVersions);
        var closeClaim = new CloseClaimUseCase(
            snapshotReader, masterProvider, officeRepository, tokenProvider, readiness,
            genericFields, batchStore, csvVersions, batchStore,
            new Kit.FakeOperationLocalSnapshotReader());
        var cancelClaim = new CancelClaimUseCase(batchStore, batchStore);
        var queryClaim = new QueryClaimUseCase(batchStore);
        var listOffices = new ListOfficesUseCase(officeRepository);
        var listRecipients = new ListRecipientsUseCase(new Kit.FakeRecipientRepository(Kit.Recipient()));
        var verifiedBatches = ClaimVerifiedBatchTestFactory.Provider(batchStore);
        var generateClaimReports = new GenerateClaimReportsUseCase(
            verifiedBatches, new Kit.NoOpClaimReportGenerator());
        var fileSaveService = new Kit.NoOpFileSaveService();
        var exportClaimCsv = new ExportClaimCsvUseCase(
            verifiedBatches,
            new Kit.FixedClaimCsvOfficeContextProvider(),
            csvVersions,
            new Kit.EmptyRequirementProvider(),
            new Kit.FakeGenericFieldCatalog(),
            new Kit.NoOpClaimCsvGenerator(),
            new Kit.NoOpClaimCsvExportRepository(),
            TimeProvider.System);

        var sut = new ClaimPreparationViewModel(
            listOffices, calculateClaim, closeClaim, cancelClaim, queryClaim,
            listRecipients, generateClaimReports, exportClaimCsv, fileSaveService)
        {
            OfficeId = Kit.OfficeId,
            Year = Kit.Month.Year,
            Month = Kit.Month.Month,
        };

        return new Fixture(sut, snapshotReader, masterProvider, batchStore);
    }

    private sealed record Fixture(
        ClaimPreparationViewModel Sut,
        Kit.MutableSnapshotReader SnapshotReader,
        Kit.MutableMasterProvider MasterProvider,
        Kit.FakeClaimBatchStore BatchStore);
}
