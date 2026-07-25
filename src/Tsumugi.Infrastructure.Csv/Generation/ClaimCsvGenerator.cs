using Tsumugi.Application.Abstractions;
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
    private readonly CsvSpecificationCatalog _catalog =
        catalog ?? throw new ArgumentNullException(nameof(catalog));

    public byte[] Generate(ClaimCsvDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var records = _catalog.ProviderRecords.OrderBy(record => record.Order).ToArray();
        var rows = ClaimCsvRowPlanner.Plan(dto, [.. records.Select(record => record.RecordId)]);
        var resolver = new ClaimCsvFieldResolver(dto, _catalog, rows);
        var byRecordId = records.ToDictionary(record => record.RecordId, StringComparer.Ordinal);

        var innerRecords = rows
            .Select(row => EncodeInnerRecord(resolver, byRecordId[row.RecordId], row))
            .ToArray();

        return ClaimCsvWriter.WriteAll(
            _catalog,
            dto.ProcessingMonth,
            dto.Office.OfficeNumber,
            DataKind(rows, byRecordId),
            innerRecords);
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
