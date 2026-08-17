namespace Tsumugi.Application.Validation;

/// <summary>
/// 職員へそのまま提示できる入力検証エラー。
///
/// ViewModel は <c>ex.Message</c> を画面へそのまま出すため、
/// <c>ArgumentException(message, paramName)</c> を使うと
/// 「事業所番号は必須です。 (Parameter 'officeNumber')」のように
/// 英語のデバッグ装飾が利用者に見えてしまう。paramName を基底へ渡さず、
/// 項目名は <see cref="FieldName"/> に分けて持つ。
///
/// <see cref="ArgumentException"/> を継承するのは、既存の
/// <c>catch (ArgumentException)</c> 経路を壊さないため。
/// 引数の取り違えのようなプログラマ側の誤りには従来どおり
/// <see cref="ArgumentException"/>（paramName つき）を使う。
/// </summary>
public class InputValidationException(string message, string? fieldName = null)
    : ArgumentException(message)
{
    /// <summary>入力項目名。表示は呼び出し側の責務で、Message には含めない。</summary>
    public string? FieldName { get; } = fieldName;
}
