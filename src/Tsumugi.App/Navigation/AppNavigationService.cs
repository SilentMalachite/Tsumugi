namespace Tsumugi.App.Navigation;

/// <summary>
/// scoped なMainViewModel coordinatorへrequestを中継する。
/// 対象ViewModel自体は保持しない。
/// </summary>
public sealed class AppNavigationService : IAppNavigationService
{
    private AppNavigationHandler? _handler;

    public void RegisterHandler(AppNavigationHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (_handler is not null)
            throw new InvalidOperationException("Navigation handler is already registered.");

        _handler = handler;
    }

    public async Task<NavigationResult> NavigateAsync(
        NavigationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_handler is null)
        {
            return NavigationResult.Failure(
                request,
                NavigationErrorCode.NavigationTargetUnavailable);
        }

        try
        {
            return await _handler(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return NavigationResult.Failure(
                request,
                NavigationErrorCode.InvalidNavigationContext);
        }
    }
}
