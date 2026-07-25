namespace Tsumugi.Application.Abstractions;

/// <summary>
/// 汎用 pass-through 入力の宣言（ADR 0042）。CSV 仕様側が正本で、Application は
/// fieldId やラベルをハードコードせずこのポート経由で参照する。
/// </summary>
/// <param name="Name">値の名前（readiness path は <c>ClaimGenericInput.{Name}</c>）。</param>
/// <param name="FieldIds">この値を運ぶ CSV 項目（同じ値を複数の項目が運ぶ宣言はまとめる）。</param>
/// <param name="Label">入力欄の表示名。</param>
/// <param name="Help">補助文。</param>
/// <param name="DataType">値の型（text / numeric / code / date / yearMonth）。</param>
/// <param name="MaxBytes">桁数（CP932 バイト数）。</param>
/// <param name="UiSurface">どの画面で入力するか。</param>
public sealed record ClaimGenericFieldDeclaration(
    string Name,
    IReadOnlyList<string> FieldIds,
    string Label,
    string Help,
    string DataType,
    int MaxBytes,
    string UiSurface);

/// <summary>
/// 仕様版ごとの汎用入力宣言。未登録の版・未宣言の名前は fail-close する
/// （宣言が引けないまま値を保存すると、検証も出力も根拠を失う）。
/// </summary>
public interface IClaimGenericFieldCatalog
{
    IReadOnlyList<ClaimGenericFieldDeclaration> GetDeclarations(string specificationVersion);

    /// <summary>
    /// 入力値が宣言（型・桁数）に適合するかを検証する。適合しなければ利用者向けの理由で例外。
    /// <b>型の語彙と桁数の尺度は CSV 仕様側の知識</b>なので、判定は実装（仕様を所有する層）に置く
    /// （Application に dataType のリテラルを持ち込まない。CLAUDE.md §ハード制約3）。
    /// </summary>
    void ValidateValue(string specificationVersion, string name, string value);
}

/// <summary>
/// 汎用入力の値が宣言（名前・型・桁数）に適合しないことを示す。<b>利用者に見せる理由</b>を運ぶので、
/// 呼び出し側は履歴競合（再読込）ではなく入力内容のエラーとして扱う。
/// </summary>
public sealed class ClaimGenericValueInvalidException(string message) : Exception(message);
