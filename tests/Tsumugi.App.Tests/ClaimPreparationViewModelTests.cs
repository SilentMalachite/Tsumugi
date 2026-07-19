using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.Claim;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Application.UseCases.Office;
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

        var calculateClaim = new CalculateClaimUseCase(
            snapshotReader, masterProvider, officeRepository, tokenProvider, readiness);
        var closeClaim = new CloseClaimUseCase(
            snapshotReader, masterProvider, officeRepository, tokenProvider, readiness,
            batchStore, batchStore);
        var cancelClaim = new CancelClaimUseCase(batchStore, batchStore);
        var queryClaim = new QueryClaimUseCase(batchStore);
        var listOffices = new ListOfficesUseCase(officeRepository);

        var sut = new ClaimPreparationViewModel(
            listOffices, calculateClaim, closeClaim, cancelClaim, queryClaim)
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
