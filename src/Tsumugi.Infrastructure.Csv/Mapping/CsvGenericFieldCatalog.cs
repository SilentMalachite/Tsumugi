using System.Globalization;
using Tsumugi.Application.Abstractions;
using Tsumugi.Infrastructure.Csv.Specifications;
using Tsumugi.Infrastructure.Csv.Writer;

namespace Tsumugi.Infrastructure.Csv.Mapping;

/// <summary>
/// CSV 仕様の <c>storage: "generic"</c> 宣言から、版ごとの汎用入力宣言を供給する（ADR 0042）。
/// </summary>
public sealed class CsvGenericFieldCatalog : IClaimGenericFieldCatalog
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ClaimGenericFieldDeclaration>> _byVersion;

    private CsvGenericFieldCatalog(
        IReadOnlyDictionary<string, IReadOnlyList<ClaimGenericFieldDeclaration>> byVersion)
        => _byVersion = byVersion;

    public static CsvGenericFieldCatalog LoadEmbedded(CsvSpecificationRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return new CsvGenericFieldCatalog(registry.Versions.ToDictionary(
            entry => entry.Version,
            entry => DeclarationsOf(registry.ResolveByVersion(entry.Version)),
            StringComparer.Ordinal));
    }

    /// <summary>単一版の catalog から作る（テスト・診断が catalog を直接与えるための入口）。</summary>
    internal static CsvGenericFieldCatalog ForCatalog(CsvSpecificationCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var byVersion = new Dictionary<string, IReadOnlyList<ClaimGenericFieldDeclaration>>(
            StringComparer.Ordinal)
        {
            [catalog.Version] = DeclarationsOf(catalog),
        };
        return new CsvGenericFieldCatalog(byVersion);
    }

    public IReadOnlyList<ClaimGenericFieldDeclaration> GetDeclarations(string specificationVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(specificationVersion);
        return _byVersion.TryGetValue(specificationVersion, out var declarations)
            ? declarations
            : throw new InvalidOperationException(
                $"CSV仕様版 '{specificationVersion}' の汎用入力宣言が登録されていません。"
                + $"（登録済み: {string.Join(", ", _byVersion.Keys)}）");
    }

    /// <inheritdoc/>
    public void ValidateValue(string specificationVersion, string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var declaration = GetDeclarations(specificationVersion)
            .FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal))
            ?? throw new ClaimGenericValueInvalidException(
                $"この項目（{name}）は現在のCSV仕様版では入力できません。");

        if (CsvCellEncoder.Cp932.GetByteCount(value) > declaration.MaxBytes)
        {
            throw new ClaimGenericValueInvalidException(
                $"{declaration.Label}は{declaration.MaxBytes}バイト以内で入力してください。");
        }

        // 数字のみ属性（numeric / date / yearMonth）は encoder と同じ規則で文字種を先に弾く。
        // 出力時にも encoder が再検証するが、確定してから分かるのでは遅い。
        // 書式は<b>CSV へ出る形</b>で受ける。区切り記号つき（yyyy-MM-dd 等）で受けると、項目定義の
        // 桁数（日付8・年月6）を必ず超え、超えなかったとしても encoder の「数字のみ」検査で落ちる。
        var valid = declaration.DataType switch
        {
            "numeric" => value.All(char.IsAsciiDigit),
            "date" => IsExactDate(value, "yyyyMMdd"),
            "yearMonth" => IsExactDate($"{value}01", "yyyyMMdd"),
            "code" or "text" => true,
            _ => throw new InvalidOperationException(
                $"{declaration.Label}の型 '{declaration.DataType}' は扱えません。"),
        };
        if (!valid)
        {
            throw new ClaimGenericValueInvalidException(
                $"{declaration.Label}の書式が正しくありません（{DataTypeHint(declaration.DataType)}）。");
        }
    }

    private static string DataTypeHint(string dataType) => dataType switch
    {
        "numeric" => "半角数字",
        "date" => "年月日8桁（例: 20260401）",
        "yearMonth" => "年月6桁（例: 202604）",
        _ => dataType,
    };

    private static bool IsExactDate(string value, string format) =>
        DateOnly.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    /// <summary>単一版の宣言（テスト・診断が catalog を直接与えるための入口）。</summary>
    internal static IReadOnlyList<ClaimGenericFieldDeclaration> DeclarationsOf(
        CsvSpecificationCatalog catalog)
        // 同じ値を複数の項目が運ぶ宣言は1つの入力欄にまとめる（欄が2つ並ぶと保存時に重複キーで落ちる）。
        // 見せ方・型・桁数が食い違う宣言は catalog の読み込みが既に拒否している。
        => [.. catalog.MappingByFieldId.Values
            .Where(CsvSpecificationCatalog.IsGenericStorage)
            .GroupBy(mapping => mapping.TargetProperty!, StringComparer.Ordinal)
            .Select(group => new ClaimGenericFieldDeclaration(
                group.Key,
                [.. group.Select(mapping => mapping.FieldId).OrderBy(id => id, StringComparer.Ordinal)],
                group.First().GenericInput!.Label,
                group.First().GenericInput!.Help,
                group.First().GenericInput!.DataType,
                group.First().GenericInput!.MaxBytes,
                group.First().UiSurface!))
            .OrderBy(declaration => declaration.Name, StringComparer.Ordinal)];
}
