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
    string? UiSurface = null,
    /// <summary>
    /// 「いずれか1つでも入力されたら同じ組の全項目を必須にする」項目群の識別子。
    /// 公式の requiredWhen（一次資料に紐づく単項条件）を書き換えずに、
    /// 組としての入力要件だけを追加宣言するためのキー。
    /// </summary>
    string? CrossFieldGroup = null);
