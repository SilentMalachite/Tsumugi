using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Tsumugi.App.Navigation;

/// <summary>
/// scoped messengerへrequestを送り、弱参照recipientのMainViewModel coordinatorへ中継する。
/// MainViewModelや対象ViewModel自体は保持しない。
/// </summary>
public sealed class AppNavigationService : IAppNavigationService
{
    private readonly IMessenger _messenger;

    public AppNavigationService(IMessenger messenger)
    {
        ArgumentNullException.ThrowIfNull(messenger);
        _messenger = messenger;
    }

    public async Task<NavigationResult> NavigateAsync(
        NavigationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var message = _messenger.Send(
                new AppNavigationMessage(request, cancellationToken));
            if (!message.HasReceivedResponse)
            {
                return NavigationResult.Failure(
                    request,
                    NavigationErrorCode.NavigationTargetUnavailable);
            }

            return await message.Response;
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

internal sealed class AppNavigationMessage(
    NavigationRequest request,
    CancellationToken cancellationToken) : AsyncRequestMessage<NavigationResult>
{
    public NavigationRequest Request { get; } = request;
    public CancellationToken CancellationToken { get; } = cancellationToken;
}
