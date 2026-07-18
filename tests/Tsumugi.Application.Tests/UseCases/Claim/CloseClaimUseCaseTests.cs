using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Domain.Enums;
using Xunit;
using Kit = Tsumugi.Application.Tests.UseCases.Claim.ClaimPreparationTestKit;

namespace Tsumugi.Application.Tests.UseCases.Claim;

public sealed class CloseClaimUseCaseTests
{
    [Fact]
    public async Task Execute_rejects_mismatched_preview_hash_before_building_the_draft()
    {
        // (d) hash不一致 → InvalidOperationPayload。storeへは到達しない。
        var store = new Kit.FakeFinalizationStore();
        var useCase = CreateUseCase(Kit.Snapshot(), store, []);

        await FluentActions.Invoking(() => useCase.ExecuteAsync(
                new CloseClaimRequest(Kit.OfficeId, Kit.Month, new string('0', 64)),
                "closer",
                CancellationToken.None))
            .Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.InvalidOperationPayload);
        store.LastDraft.Should().BeNull();
    }

    [Fact]
    public async Task Execute_commits_new_draft_with_recomputed_totals_and_details()
    {
        // (e) 空履歴 → RecordKind.New。プレビューと同一入力なのでhash照合が成立する。
        var previewHash = await PreviewHashAsync();
        var store = new Kit.FakeFinalizationStore();
        var useCase = CreateUseCase(Kit.Snapshot(), store, []);

        var dto = await useCase.ExecuteAsync(
            new CloseClaimRequest(Kit.OfficeId, Kit.Month, previewHash), "closer", CancellationToken.None);

        dto.Should().Be(new ClaimBatchRevisionDto(store.Result.BatchId, store.Result.Revision, false));
        store.LastDraft.Should().NotBeNull();
        var draft = store.LastDraft!;
        draft.Kind.Should().Be(RecordKind.New);
        draft.OfficeId.Should().Be(Kit.OfficeId);
        draft.ServiceMonth.Should().Be(Kit.Month);
        draft.RootBatchId.Should().BeNull();
        draft.ExpectedHead.Should().BeNull();
        draft.CreatedBy.Should().Be("closer");
        draft.FinalizationOperationId.Should().NotBeEmpty();
        draft.ClaimMasterVersion.Should().Be("master-v1");
        draft.TotalUnits.Should().Be(1400);
        draft.TotalCostYen.Should().Be(14000);
        draft.TotalBenefitYen.Should().Be(12600);
        draft.TotalBurdenYen.Should().Be(1400);
        var detail = draft.Details.Should().ContainSingle().Subject;
        detail.RecipientId.Should().Be(Kit.RecipientId);
        detail.SnapshotSchemaVersion.Should().Be(ClaimSnapshotValidationCodecV1.SchemaVersionValue);
        detail.InputSnapshotEnvelope.ValidationCodecId
            .Should().Be(ClaimSnapshotValidationCodecV1.ValidationCodecIdValue);
        detail.CalculationSnapshotEnvelope.ValidationCodecId
            .Should().Be(ClaimSnapshotValidationCodecV1.ValidationCodecIdValue);
        detail.TotalUnits.Should().Be(1400);
    }

    [Fact]
    public async Task Execute_commits_correction_against_the_current_head()
    {
        var previewHash = await PreviewHashAsync();
        var head = Kit.Batch();
        var store = new Kit.FakeFinalizationStore();
        var useCase = CreateUseCase(Kit.Snapshot(), store, [Kit.Aggregate(head, Kit.RecipientId)]);

        await useCase.ExecuteAsync(
            new CloseClaimRequest(Kit.OfficeId, Kit.Month, previewHash), "closer", CancellationToken.None);

        store.LastDraft.Should().NotBeNull();
        var draft = store.LastDraft!;
        draft.Kind.Should().Be(RecordKind.Correct);
        draft.RootBatchId.Should().Be(head.Id);
        draft.ExpectedHead.Should().Be(new ClaimExpectedHead(head.Id, 1));
    }

    [Fact]
    public async Task Execute_preview_and_close_share_the_same_hash_for_identical_input()
    {
        // PreviewHash契約: プレビューと確定は同一入力→同一hash（照合成立が唯一の確定経路）。
        var previewHash = await PreviewHashAsync();
        var store = new Kit.FakeFinalizationStore();
        var useCase = CreateUseCase(Kit.Snapshot(), store, []);

        await FluentActions.Invoking(() => useCase.ExecuteAsync(
                new CloseClaimRequest(Kit.OfficeId, Kit.Month, previewHash),
                "closer",
                CancellationToken.None))
            .Should().NotThrowAsync();
        store.LastDraft.Should().NotBeNull();
    }

    [Fact]
    public async Task Execute_rejects_close_when_not_ready()
    {
        var store = new Kit.FakeFinalizationStore();
        var useCase = CreateUseCase(Kit.Snapshot(includeProfile: false), store, []);

        await FluentActions.Invoking(() => useCase.ExecuteAsync(
                new CloseClaimRequest(Kit.OfficeId, Kit.Month, new string('0', 64)),
                "closer",
                CancellationToken.None))
            .Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.InvalidOperationPayload);
        store.LastDraft.Should().BeNull();
    }

    private static async Task<string> PreviewHashAsync()
    {
        var preview = await CalculateClaimUseCaseTests.CreateUseCase(Kit.Snapshot()).ExecuteAsync(
            new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);
        preview.IsReady.Should().BeTrue();
        return preview.PreviewHash;
    }

    private static CloseClaimUseCase CreateUseCase(
        ClaimCalculationSnapshot snapshot,
        Kit.FakeFinalizationStore store,
        IReadOnlyList<ClaimBatchAggregate> history)
        => new(
            new Kit.FakeSnapshotReader(snapshot),
            new Kit.FakeMasterProvider(Kit.Release(), Kit.SyntheticMasters()),
            new Kit.FakeOfficeRepository(Kit.Office()),
            new Kit.FakeTokenProvider(Kit.Tokens()),
            Kit.Readiness(),
            new Kit.FakeBatchRepository(history),
            store);
}
