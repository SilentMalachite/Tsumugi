using System.Text.Json;

namespace Tsumugi.Infrastructure.Csv.Specifications;

public sealed record CsvFieldMapping(
    string FieldId,
    string RequiredCondition,
    string Notes,
    string Status,
    string? GeneratorRule = null,
    string? InputContract = null,
    bool? MigrationRequired = null,
    string? ModelPath = null,
    IReadOnlyList<JsonElement>? SourceContracts = null,
    IReadOnlyList<string>? SourceFieldIds = null,
    string? TargetModel = null,
    string? TargetProperty = null,
    string? UiSurface = null);
