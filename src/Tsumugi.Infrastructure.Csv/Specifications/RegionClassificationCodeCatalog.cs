using System.Collections.ObjectModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Infrastructure.Csv.Specifications;

/// <summary>
/// 地域区分コード（<c>provider:J121:01:010</c>）の公式コード表。
/// </summary>
/// <remarks>
/// コードは級地番号のゼロ詰めではない（一級地=11、七級地=17、その他=23）。
/// 出典は共通編（<c>common-r7-10</c>）の物理21頁のコード一覧で、値は
/// <c>region-classification-codes-r7-10.json</c> に外部化している（CLAUDE.md §ハード制約3）。
/// </remarks>
public sealed class RegionClassificationCodeCatalog
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private static readonly Lazy<RegionClassificationCodeCatalog> Embedded =
        new(LoadEmbedded, LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly ReadOnlyDictionary<RegionGrade, string> _codeByGrade;

    private RegionClassificationCodeCatalog(
        string specificationVersion,
        IReadOnlyList<string> allCodes,
        IDictionary<RegionGrade, string> codeByGrade)
    {
        SpecificationVersion = specificationVersion;
        AllCodes = allCodes;
        _codeByGrade = new ReadOnlyDictionary<RegionGrade, string>(codeByGrade);
    }

    public static RegionClassificationCodeCatalog Instance => Embedded.Value;

    public string SpecificationVersion { get; }

    /// <summary>公式コード表に載る全コード（対応する <see cref="RegionGrade"/> が無いものも含む）。</summary>
    public IReadOnlyList<string> AllCodes { get; }

    /// <summary>
    /// 地域区分の公式コードを返す。対応するコードが表に無い区分（未設定等）は
    /// <c>false</c> を返し、呼び出し側で fail-close させる。
    /// </summary>
    public bool TryResolve(RegionGrade regionGrade, out string code) =>
        _codeByGrade.TryGetValue(regionGrade, out code!);

    private static RegionClassificationCodeCatalog LoadEmbedded()
    {
        var assembly = typeof(RegionClassificationCodeCatalog).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith(
                ".Specifications.region-classification-codes-r7-10.json", StringComparison.Ordinal))
            ?? throw new InvalidDataException("Embedded region classification code list was not found.");
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidDataException("Embedded region classification code list could not be opened.");

        var file = JsonSerializer.Deserialize<RegionClassificationCodeFile>(stream, Options)
            ?? throw new InvalidDataException("Region classification code list is empty.");
        if (file.SchemaVersion != 1)
        {
            throw new InvalidDataException(
                $"Region classification code list has unsupported schemaVersion {file.SchemaVersion}.");
        }

        if (string.IsNullOrWhiteSpace(file.SourceDocumentId) || file.SourcePage <= 0)
        {
            throw new InvalidDataException("Region classification code list is missing its provenance.");
        }

        var codeByGrade = new Dictionary<RegionGrade, string>();
        foreach (var entry in file.Codes)
        {
            if (entry.Code.Length != 2 || !entry.Code.All(char.IsAsciiDigit))
            {
                throw new InvalidDataException($"Region classification code '{entry.Code}' is not two digits.");
            }

            if (entry.RegionGrade is null) continue;
            if (!Enum.TryParse<RegionGrade>(entry.RegionGrade, ignoreCase: false, out var grade)) // CultureInfo: 非該当
            {
                throw new InvalidDataException(
                    $"Region classification code '{entry.Code}' names an unknown region grade.");
            }

            if (!codeByGrade.TryAdd(grade, entry.Code))
            {
                throw new InvalidDataException($"Region grade '{grade}' is mapped to more than one code.");
            }
        }

        return new RegionClassificationCodeCatalog(
            file.SpecificationVersion,
            [.. file.Codes.Select(entry => entry.Code)],
            codeByGrade);
    }

    private sealed record RegionClassificationCodeFile(
        int SchemaVersion,
        string SpecificationVersion,
        string SourceDocumentId,
        int SourcePage,
        string Note,
        IReadOnlyList<RegionClassificationCodeEntry> Codes);

    private sealed record RegionClassificationCodeEntry(
        string Code,
        string OfficialName,
        string? RegionGrade);
}
