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
            ?? throw new InvalidOperationException(
                $"汎用請求入力 '{name}' はCSV仕様に宣言されていません。");

        if (CsvCellEncoder.Cp932.GetByteCount(value) > declaration.MaxBytes)
        {
            throw new InvalidOperationException(
                $"{declaration.Label}は{declaration.MaxBytes}バイト以内で入力してください。");
        }

        // 数字のみ属性（numeric / date / yearMonth）は encoder と同じ規則で文字種を先に弾く。
        // 出力時にも encoder が再検証するが、確定してから分かるのでは遅い。
        var valid = declaration.DataType switch
        {
            "numeric" => value.All(char.IsAsciiDigit),
            "date" => IsExactDate(value, "yyyy-MM-dd"),
            "yearMonth" => IsExactDate($"{value}-01", "yyyy-MM-dd"),
            "code" or "text" => true,
            _ => throw new InvalidOperationException(
                $"{declaration.Label}の型 '{declaration.DataType}' は扱えません。"),
        };
        if (!valid)
        {
            throw new InvalidOperationException(
                $"{declaration.Label}の書式が正しくありません（{declaration.DataType}）。");
        }
    }

    private static bool IsExactDate(string value, string format) =>
        DateOnly.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    /// <summary>単一版の宣言（テスト・診断が catalog を直接与えるための入口）。</summary>
    internal static IReadOnlyList<ClaimGenericFieldDeclaration> DeclarationsOf(
        CsvSpecificationCatalog catalog)
        => [.. catalog.MappingByFieldId.Values
            .Where(CsvSpecificationCatalog.IsGenericStorage)
            .Select(mapping => new ClaimGenericFieldDeclaration(
                mapping.TargetProperty!,
                mapping.FieldId,
                mapping.GenericInput!.Label,
                mapping.GenericInput.Help,
                mapping.GenericInput.DataType,
                mapping.GenericInput.MaxBytes,
                mapping.UiSurface!))
            .OrderBy(declaration => declaration.Name, StringComparer.Ordinal)];
}
