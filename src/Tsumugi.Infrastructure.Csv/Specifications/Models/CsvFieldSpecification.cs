namespace Tsumugi.Infrastructure.Csv.Specifications;

public sealed record CsvFieldSpecification(
    string FieldId,
    int Position,
    string OfficialName,
    string RequiredWhen,
    string DataType,
    int MaxBytes,
    string QuoteRule,
    IReadOnlyList<string> AllowedCodes,
    int SourcePage,
    string RequiredWhenSource,
    string? AllowedCodesSource = null);
