using System.Globalization;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Application.Dtos.Claim.Csv;
using Tsumugi.Infrastructure.Csv.Specifications;
using Tsumugi.Infrastructure.Csv.Writer;

namespace Tsumugi.Infrastructure.Csv.Generation;

/// <summary>
/// spec 駆動の国保連請求CSV生成。内側レコードは
/// <c>provider-claim-r7-10.json</c> の <c>order</c>（J111:01 → J111:02 → J121:01..05 →
/// J611:01 → J611:02）で並べ、各項目の値は <c>field-mapping-r7-10.json</c> のマッピングだけから決める。
/// </summary>
public sealed class ClaimCsvGenerator(CsvSpecificationCatalog catalog) : IClaimCsvGenerator
{
    /// <summary>共通編 1.2.1 が定める CSV 形式の拡張子。</summary>
    private const string FileNameExtension = ".CSV";

    private readonly CsvSpecificationCatalog _catalog =
        catalog ?? throw new ArgumentNullException(nameof(catalog));

    public string SpecificationVersion => _catalog.Version;

    public ClaimCsvDocument Generate(ClaimCsvDto dto)
    {
        // spec 側の fail-close 例外は Application が catch できる契約例外へ翻訳する
        // （Application は Tsumugi.Infrastructure.Csv を参照しないため）。
        try
        {
            return GenerateCore(dto);
        }
        catch (CsvEncodingException exception)
        {
            throw new ClaimCsvExportFailedException(
                exception.FieldId, exception.Reason.ToString(), exception.Detail);
        }
        catch (ClaimCsvGenerationException exception)
        {
            throw new ClaimCsvExportFailedException(
                exception.FieldId,
                exception.Reason.ToString(),
                exception.Detail,
                exception.RecipientReferenceCode);
        }
        catch (CsvGeneratorRuleException exception)
        {
            throw new ClaimCsvExportFailedException(
                exception.Target, "GeneratorRuleMalformed", exception.Detail);
        }
    }

    private ClaimCsvDocument GenerateCore(ClaimCsvDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var records = _catalog.ProviderRecords.OrderBy(record => record.Order).ToArray();
        var rows = ClaimCsvRowPlanner.Plan(dto, [.. records.Select(record => record.RecordId)]);
        var resolver = new ClaimCsvFieldResolver(dto, _catalog, rows);
        var byRecordId = records.ToDictionary(record => record.RecordId, StringComparer.Ordinal);

        var innerRecords = rows
            .Select(row => EncodeInnerRecord(resolver, byRecordId[row.RecordId], row))
            .ToArray();

        var dataKind = DataKind(rows, byRecordId);
        var bytes = ClaimCsvWriter.WriteAll(
            _catalog,
            dto.ProcessingMonth,
            dto.Office.OfficeNumber,
            dataKind,
            innerRecords);
        return new ClaimCsvDocument(bytes, BuildFileName(dataKind, dto.ProcessingMonth, bytes));
    }

    /// <summary>
    /// 共通編 1.2.1 の CSV 形式ファイル名規則（英字で始まる半角英数字 8 桁以内 ＋ ".CSV"）に従う。
    /// データ種別 3 桁 ＋ 処理対象年月の下 4 桁 ＋ 内容の SHA-256 先頭 1 桁で 8 桁にする。
    /// 同一月に内容の異なる交換情報を複数出しても名前が衝突しない（共通編 1.7.2 の要請）。
    /// </summary>
    private static string BuildFileName(
        string dataKind, Domain.ValueObjects.ProcessingMonth processingMonth, byte[] bytes)
    {
        var fingerprint = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes))[0];
        var stem = string.Concat(
            dataKind,
            (processingMonth.Year % 100).ToString("D2", CultureInfo.InvariantCulture),
            processingMonth.Month.ToString("D2", CultureInfo.InvariantCulture),
            fingerprint)
            .ToUpperInvariant();
        return $"{stem}{FileNameExtension}";
    }

    private static ReadOnlyMemory<byte> EncodeInnerRecord(
        ClaimCsvFieldResolver resolver,
        CsvRecordSpecification record,
        ClaimCsvRowPlan row)
    {
        var cells = record.Fields
            .Select(field => new CsvCell(field.FieldId, resolver.RenderCell(field.FieldId, row)))
            .ToArray();
        return CsvCellEncoder.EncodeFields(cells, record.Fields);
    }

    /// <summary>
    /// データ種別。spec の <c>lookup(firstThreeCharactersOfInnerExchangeInformationId)</c> に従い、
    /// 先頭の内側レコードの交換情報識別番号の先頭 3 文字を採る。
    /// </summary>
    private static string DataKind(
        IReadOnlyList<ClaimCsvRowPlan> rows,
        Dictionary<string, CsvRecordSpecification> byRecordId)
    {
        if (rows.Count == 0)
        {
            throw new ClaimCsvGenerationException(
                "common:outer:control:005",
                ClaimCsvGenerationReason.MissingRow,
                "the data kind cannot be derived without an inner record");
        }

        var exchangeInformationId = byRecordId[rows[0].RecordId].ExchangeInformationId;
        return exchangeInformationId.Length >= 3
            ? exchangeInformationId[..3]
            : throw new ClaimCsvGenerationException(
                "common:outer:control:005",
                ClaimCsvGenerationReason.UnresolvableRule,
                "the exchange information id is shorter than three characters");
    }
}
