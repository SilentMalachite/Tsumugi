using Tsumugi.Application.UseCases.Office;
using Tsumugi.Domain.Logic;

namespace Tsumugi.App.Startup;

/// <summary>
/// 事業所件数から初回ウィザード／メインの行き先を決める。
/// migration・Window・DI には依存しない。
/// </summary>
public sealed class FirstRunStartupCoordinator(CountOfficesUseCase countOffices)
{
    public async Task<FirstRunStartupDestination> DecideAsync(
        CancellationToken ct = default)
    {
        var officeCount = await countOffices.ExecuteAsync(ct);
        return FirstRunPolicy.NeedsFirstRun(officeCount)
            ? FirstRunStartupDestination.Wizard
            : FirstRunStartupDestination.Main;
    }
}
