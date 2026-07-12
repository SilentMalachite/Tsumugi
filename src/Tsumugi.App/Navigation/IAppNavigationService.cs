namespace Tsumugi.App.Navigation;

public enum NavigationErrorCode
{
    NavigationTargetUnavailable = 1,
    InvalidNavigationContext = 2,
}

public sealed record NavigationResult
{
    private NavigationResult(
        NavigationRequest request,
        NavigationErrorCode? errorCode)
    {
        Request = request;
        ErrorCode = errorCode;
    }

    public NavigationRequest Request { get; }
    public NavigationErrorCode? ErrorCode { get; }
    public bool IsSuccess => ErrorCode is null;

    public static NavigationResult Success(NavigationRequest request) => new(request, null);

    public static NavigationResult Failure(
        NavigationRequest request,
        NavigationErrorCode errorCode) => new(request, errorCode);
}

public interface IAppNavigationService
{
    Task<NavigationResult> NavigateAsync(
        NavigationRequest request,
        CancellationToken cancellationToken = default);
}
