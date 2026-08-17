namespace Tsumugi.App.Startup;

/// <summary>
/// 初回ウィザードの寿命状態。登録完了と終了要求をそれぞれ一度だけ通し、
/// Closing を許可してよいかを答える。
///
/// Avalonia に依存しないので、Window を組み立てずに検証できる。
/// 状態遷移をコードビハインドへ直接書くと、テスト手段がソース文字列の
/// 走査しか残らず、順序も再入も証明できない。
/// </summary>
public sealed class FirstRunWizardLifecycle(
    Action onRegistrationCompleted,
    Action onCancellationRequested)
{
    private bool _registrationCompleted;
    private bool _cancellationRequested;

    /// <summary>
    /// Closing を素通ししてよいか。登録が終わるまでは false で、
    /// 呼び出し側は Close をキャンセルして <see cref="RequestCancellation"/> へ回す
    /// （最後の Window を閉じて OnLastWindowClose から二重の終了要求が出るのを防ぐ）。
    /// </summary>
    public bool ShouldAllowClose => _registrationCompleted;

    /// <summary>
    /// 登録が永続化された。Window 差し替えを一度だけ要求する。
    /// 完了フラグは callback より先に立てる — 差し替え callback の中で
    /// Close されるため、その時点で <see cref="ShouldAllowClose"/> が true でないと
    /// Closing がキャンセルされて閉じられない。
    /// </summary>
    public void NotifyRegistered()
    {
        if (_registrationCompleted)
            return;

        _registrationCompleted = true;
        onRegistrationCompleted();
    }

    /// <summary>キャンセルまたは未完了の Close。終了要求を一度だけ出す。</summary>
    public void RequestCancellation()
    {
        if (_cancellationRequested)
            return;

        _cancellationRequested = true;
        onCancellationRequested();
    }
}
