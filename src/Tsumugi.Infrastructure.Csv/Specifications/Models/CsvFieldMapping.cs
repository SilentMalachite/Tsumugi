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
    string? CrossFieldGroup = null,
    /// <summary>
    /// 入力値の保存方式。<c>typed</c>（既定）は Domain の型付きプロパティ、
    /// <c>generic</c> は spec 宣言だけで増やせる汎用 pass-through 入力（ADR 0042）。
    /// <c>generic</c> は「算定に効かない転記項目」だけに許し、証跡台帳の
    /// <c>pass-through</c> claim を要求する。
    /// </summary>
    string? Storage = null,
    /// <summary>
    /// 汎用入力の宣言（<c>storage: "generic"</c> のときだけ必須）。ラベル・補助文・属性・桁数を
    /// spec 側に置き、UI と入力検証はここから駆動する（ハードコードしない）。
    /// </summary>
    CsvGenericInputDeclaration? GenericInput = null);

/// <param name="Label">入力欄の表示名（公式項目名に対応させる）。</param>
/// <param name="Help">補助文。運用者が何を入れるか判断できる説明。</param>
/// <param name="DataType">値の型（項目定義の dataType と同じ語彙）。</param>
/// <param name="MaxBytes">桁数（CP932 バイト数）。項目定義と一致させる。</param>
public sealed record CsvGenericInputDeclaration(
    string Label,
    string Help,
    string DataType,
    int MaxBytes);
