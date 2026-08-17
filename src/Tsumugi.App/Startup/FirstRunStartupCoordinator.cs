using Tsumugi.Application.UseCases.Office;
using Tsumugi.Domain.Logic;

namespace Tsumugi.App.Startup;

/// <summary>
/// 事業所件数から初回ウィザード／メインの行き先を決める。
/// migration・Window・DI には依存しない。
/// </summary>
public sealed class FirstRunStartupCoordinator(ListOfficesUseCase listOffices)
{
    public async Task<FirstRunStartupDestination> DecideAsync(
        CancellationToken ct = default)
    {
        var offices = await listOffices.ExecuteAsync(ct);
        return FirstRunPolicy.NeedsFirstRun(offices.Count)
            ? FirstRunStartupDestination.Wizard
            : FirstRunStartupDestination.Main;
    }
}
