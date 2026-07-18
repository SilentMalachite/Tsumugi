using FluentAssertions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Domain.Enums;
using Xunit;
using Kit = Tsumugi.Application.Tests.UseCases.Claim.ClaimPreparationTestKit;

namespace Tsumugi.Application.Tests.UseCases.Claim;

public sealed class QueryClaimUseCaseTests
{
    [Fact]
    public async Task Execute_maps_history_aggregates_ordered_by_revision()
    {
        // (g) 履歴（New→Correct）をrevision順のDTOへ写像する。
        var root = Kit.Batch();
        var correction = Kit.Batch(
            revision: 2,
            kind: RecordKind.Correct,
            originId: root.Id,
            expectedHeadBatchId: root.Id,
            expectedHeadRevision: 1,
            totalUnits: 2100,
            totalCostYen: 21000,
            totalBenefitYen: 18900,
            totalBurdenYen: 2100);
        var useCase = new QueryClaimUseCase(new Kit.FakeBatchRepository(
        [
            Kit.Aggregate(correction, Kit.RecipientId),
            Kit.Aggregate(root, Kit.RecipientId),
        ]));

        var history = await useCase.ExecuteAsync(
            new QueryClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        history.Should().HaveCount(2);
        history[0].BatchId.Should().Be(root.Id);
        history[0].Revision.Should().Be(1);
        history[0].Kind.Should().Be(RecordKind.New);
        history[0].TotalUnits.Should().Be(1400);
        history[0].CreatedBy.Should().Be("tester");
        history[0].Details.Should().ContainSingle(detail =>
            detail.RecipientId == Kit.RecipientId
            && detail.TotalUnits == 1400
            && detail.TotalCostYen == 14000
            && detail.BenefitYen == 12600
            && detail.BurdenYen == 1400);
        history[1].BatchId.Should().Be(correction.Id);
        history[1].Revision.Should().Be(2);
        history[1].Kind.Should().Be(RecordKind.Correct);
        history[1].OriginId.Should().Be(root.Id);
        history[1].TotalUnits.Should().Be(2100);
    }

    [Fact]
    public async Task Execute_returns_empty_history_as_empty_list()
    {
        var useCase = new QueryClaimUseCase(new Kit.FakeBatchRepository([]));

        var history = await useCase.ExecuteAsync(
            new QueryClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        history.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_rejects_invalid_request()
    {
        var useCase = new QueryClaimUseCase(new Kit.FakeBatchRepository([]));

        await FluentActions.Invoking(() => useCase.ExecuteAsync(
                new QueryClaimRequest(Guid.Empty, Kit.Month), CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>();
    }
}
