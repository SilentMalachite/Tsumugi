namespace Tsumugi.App.ViewModels;

/// <summary>入力欄の文字列を Application 層へ渡す前の共通処理。</summary>
internal static class InputText
{
    /// <summary>
    /// 任意項目は空白のみなら未入力として null で渡す。
    /// 空文字や空白を素通しすると「空白以外を指定してください」の検証に当たり、
    /// 触っていない欄でエラーが出る。
    /// </summary>
    public static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
