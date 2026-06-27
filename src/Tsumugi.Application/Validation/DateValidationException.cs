namespace Tsumugi.Application.Validation;

public sealed class DateValidationException(string message, string fieldName)
    : ArgumentException(message, fieldName)
{
    public string FieldName { get; } = fieldName;
}
