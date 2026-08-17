namespace Tsumugi.Application.Validation;

/// <summary>
/// 日付入力の検証エラー。Message に paramName 装飾を付けない理由は
/// <see cref="InputValidationException"/> を参照。
/// </summary>
public sealed class DateValidationException(string message, string fieldName)
    : InputValidationException(message, fieldName);
