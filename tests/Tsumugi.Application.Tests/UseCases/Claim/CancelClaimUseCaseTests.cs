using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Domain.Enums;
using Xunit;
using Kit = Tsumugi.Application.Tests.UseCases.Claim.ClaimPreparationTestKit;

namespace Tsumugi.Application.Tests.UseCases.Claim;

public sealed class CancelClaimUseCaseTests
{
    [Fact]
    public async Task Execute_throws_invalid_history_without_a_head()
    {
        // (f) head無し → InvalidHistory。
        var store = new Kit.FakeFinalizationStore();
        var useCase = new CancelClaimUseCase(new Kit.FakeBatchRepository([]), store);

        await FluentActions.Invoking(() => useCase.ExecuteAsync(
                new CancelClaimRequest(Kit.OfficeId, Kit.Month), "canceller", CancellationToken.None))
            .Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.InvalidHistory);
        store.LastDraft.Should().BeNull();
    }

    [Fact]
    public async Task Execute_commits_zero_total_cancel_draft_against_the_head()
    {
        var head = Kit.Batch();
        var store = new Kit.FakeFinalizationStore();
        var useCase = new CancelClaimUseCase(
            new Kit.FakeBatchRepository([Kit.Aggregate(head, Kit.RecipientId)]), store);

        var dto = await useCase.ExecuteAsync(
            new CancelClaimRequest(Kit.OfficeId, Kit.Month), "canceller", CancellationToken.None);

        dto.Should().Be(new ClaimBatchRevisionDto(store.Result.BatchId, store.Result.Revision, false));
        store.LastDraft.Should().NotBeNull();
        var draft = store.LastDraft!;
        draft.Kind.Should().Be(RecordKind.Cancel);
        draft.RootBatchId.Should().Be(head.Id);
        draft.ExpectedHead.Should().Be(new ClaimExpectedHead(head.Id, 1));
        draft.CreatedBy.Should().Be("canceller");
        draft.TotalUnits.Should().Be(0);
        draft.TotalCostYen.Should().Be(0);
        draft.TotalBenefitYen.Should().Be(0);
        draft.TotalBurdenYen.Should().Be(0);
        draft.Details.Should().BeEmpty();
        draft.ClaimMasterVersion.Should().Be(head.ClaimMasterVersion);
        draft.CsvSpecificationVersion.Should().Be(head.CsvSpecificationVersion);
        draft.ReportSpecificationVersion.Should().Be(head.ReportSpecificationVersion);
        draft.SnapshotApplicationVersion.Should().Be(head.SnapshotApplicationVersion);
    }

    [Fact]
    public async Task Execute_rejects_cancel_when_head_is_already_cancelled()
    {
        var root = Kit.Batch();
        var cancel = Kit.Batch(
            revision: 2,
            kind: RecordKind.Cancel,
            originId: root.Id,
            expectedHeadBatchId: root.Id,
            expectedHeadRevision: 1);
        var useCase = new CancelClaimUseCase(
            new Kit.FakeBatchRepository([Kit.Aggregate(root, Kit.RecipientId), Kit.Aggregate(cancel)]),
            new Kit.FakeFinalizationStore());

        await FluentActions.Invoking(() => useCase.ExecuteAsync(
                new CancelClaimRequest(Kit.OfficeId, Kit.Month), "canceller", CancellationToken.None))
            .Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.InvalidHistory);
    }
}
