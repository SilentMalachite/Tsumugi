using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim;

namespace Tsumugi.Application.UseCases.Claim;

public sealed class QueryIntensiveSupportEpisodeUseCase(
    IIntensiveSupportEpisodeRepository repository)
{
    public async Task<IntensiveSupportEpisodeHistoryDto> ExecuteAsync(
        QueryIntensiveSupportEpisodeRequest request,
        CancellationToken ct)
    {
        ClaimInputQueryGuard.Validate(request);
        var items = await repository.ListHistoryAsync(
            request.OfficeId, request.RecipientId, ct);
        if (items.Count == 0)
        {
            return new IntensiveSupportEpisodeHistoryDto(null, null, null, []);
        }

        var groups = items.GroupBy(item => item.RootId).ToArray();
        ClaimInputQueryGuard.RequireSingleRoot(groups.Length);
        var history = groups[0].ToArray();
        ClaimInputQueryGuard.ValidateHistory(
            () => IntensiveSupportEpisodePolicy.ValidateHistory(history));
        var ordered = history.OrderBy(item => item.Revision).ToArray();
        var head = ordered[^1];
        return new IntensiveSupportEpisodeHistoryDto(
            head.RootId,
            head.Id,
            head.Kind == RecordKind.Cancel ? null : head.Id,
            ordered.Select(Map).ToArray());
    }

    private static IntensiveSupportEpisodeQueryRevisionDto Map(
        IntensiveSupportEpisode item) =>
        new(
            item.Id,
            item.OfficeId,
            item.RecipientId,
            item.RootId,
            item.Revision,
            item.Kind,
            item.ExpectedHeadId,
            item.StartDate,
            item.CreatedAt,
            item.CreatedBy);
}
