using System.Globalization;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Csv.Generation;
using Tsumugi.Infrastructure.Csv.Specifications;

namespace Tsumugi.Infrastructure.Csv.Writer;

/// <summary>
/// 外側 3 レコード（<c>common:outer:control</c> = 1 / <c>common:outer:data</c> = 2 が n 行 /
/// <c>common:outer:end</c> = 3）を組み立てる。各行は spec の項目順にカンマで連結し、
/// 末尾の「ブランク」項目（quoteRule=crlf）が行終端の CRLF になる。
/// </summary>
/// <remarks>
/// データレコードの「データ」項目（<c>common:outer:data:003</c>, 822 バイト）は、
/// 内側 provider レコード 1 件をそのまま格納する。spec 内で
/// <c>provider:J611:01</c> の sum(maxBytes)+区切りカンマ数 = 822 が一致することが、
/// この構造とバイト幅の意味を裏づけている。
/// </remarks>
public static class ClaimCsvWriter
{
    private const string ControlRecordId = "common:outer:control";
    private const string DataRecordId = "common:outer:data";
    private const string EndRecordId = "common:outer:end";

    /// <summary>レコード番号（連番）は制御レコードを 1 とし、データ n 行、終端が n+2 になる。</summary>
    private const int ControlSequenceNumber = 1;

    /// <param name="innerRecords">内側 provider レコードの符号化済みバイト列（CRLF を含まない）。</param>
    /// <param name="dataKind">
    /// データ種別。spec の <c>lookup(firstThreeCharactersOfInnerExchangeInformationId)</c> に従い、
    /// 先頭の内側レコードの交換情報識別番号の先頭 3 文字を渡す。
    /// </param>
    public static byte[] WriteAll(
        CsvSpecificationCatalog catalog,
        ProcessingMonth processingMonth,
        string officeNumber,
        string dataKind,
        IReadOnlyList<ReadOnlyMemory<byte>> innerRecords)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(innerRecords);
        ArgumentException.ThrowIfNullOrWhiteSpace(officeNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataKind);
        if (innerRecords.Count == 0)
        {
            throw new ClaimCsvGenerationException(
                DataRecordId,
                ClaimCsvGenerationReason.MissingRow,
                "at least one inner provider record is required");
        }

        var control = Record(catalog, ControlRecordId);
        var data = Record(catalog, DataRecordId);
        var end = Record(catalog, EndRecordId);

        using var buffer = new MemoryStream();
        buffer.Write(WriteControl(control, processingMonth, officeNumber, dataKind, innerRecords.Count).Span);
        for (var index = 0; index < innerRecords.Count; index++)
        {
            WriteDataRecord(buffer, data, index, innerRecords[index]);
        }

        buffer.Write(WriteEnd(end, innerRecords.Count).Span);
        return buffer.ToArray();
    }

    private static CsvRecordSpecification Record(CsvSpecificationCatalog catalog, string recordId) =>
        catalog.CommonRecords.SingleOrDefault(record =>
            string.Equals(record.RecordId, recordId, StringComparison.Ordinal))
        ?? throw new ClaimCsvGenerationException(
            recordId,
            ClaimCsvGenerationReason.UnresolvableFieldReference,
            "the outer record is missing from the specification");

    private static ReadOnlyMemory<byte> WriteControl(
        CsvRecordSpecification control,
        ProcessingMonth processingMonth,
        string officeNumber,
        string dataKind,
        int dataRecordCount)
    {
        var cells = control.Fields
            .Select(field => new CsvCell(
                field.FieldId,
                FrameValue(
                    field,
                    sequenceNumber: ControlSequenceNumber,
                    dataRecordCount,
                    processingMonth,
                    officeNumber,
                    dataKind)))
            .ToArray();
        return CsvCellEncoder.EncodeFields(cells, control.Fields);
    }

    private static ReadOnlyMemory<byte> WriteEnd(CsvRecordSpecification end, int dataRecordCount)
    {
        var cells = end.Fields
            .Select(field => new CsvCell(
                field.FieldId,
                FrameValue(
                    field,
                    sequenceNumber: dataRecordCount + 2,
                    dataRecordCount,
                    processingMonth: null,
                    officeNumber: null,
                    dataKind: null)))
            .ToArray();
        return CsvCellEncoder.EncodeFields(cells, end.Fields);
    }

    /// <summary>
    /// データレコードは「データ」項目に内側レコードのバイト列をそのまま載せるため、
    /// 引用規則を通さず直接書き込む（内側レコード自体が既にカンマ区切りで符号化済み）。
    /// </summary>
    private static void WriteDataRecord(
        Stream sink,
        CsvRecordSpecification data,
        int dataRecordIndex,
        ReadOnlyMemory<byte> innerRecord)
    {
        for (var index = 0; index < data.Fields.Count; index++)
        {
            var field = data.Fields[index];
            var isPayload = IsPayloadField(field);
            var isTerminator = string.Equals(
                field.QuoteRule, CsvCellEncoder.CrlfQuoteRule, StringComparison.Ordinal);

            if (index > 0 && !isTerminator) sink.WriteByte((byte)',');

            if (isPayload)
            {
                if (innerRecord.Length > field.MaxBytes)
                {
                    throw new CsvEncodingException(
                        field.FieldId,
                        CsvEncodingReason.OverByteWidth,
                        $"inner record byte length {innerRecord.Length} exceeds max {field.MaxBytes}");
                }

                sink.Write(innerRecord.Span);
                continue;
            }

            var raw = FrameValue(
                field,
                sequenceNumber: dataRecordIndex + 2,
                dataRecordCount: 0,
                processingMonth: null,
                officeNumber: null,
                dataKind: null);
            sink.Write(CsvCellEncoder.EncodeCell(new CsvCell(field.FieldId, raw), field).Span);
        }
    }

    private static bool IsPayloadField(CsvFieldSpecification field) =>
        field.FieldId.EndsWith(":003", StringComparison.Ordinal)
        && field.FieldId.StartsWith(DataRecordId, StringComparison.Ordinal);

    /// <summary>
    /// 外側レコードの 1 項目を、mapping の generatorRule / modelPath / inputContract から決める。
    /// フレーム固有の規則（sequence / recordCount / lookup / payload）だけをここで解釈する。
    /// </summary>
    private static string FrameValue(
        CsvFieldSpecification field,
        int sequenceNumber,
        int dataRecordCount,
        ProcessingMonth? processingMonth,
        string? officeNumber,
        string? dataKind)
    {
        if (string.Equals(field.QuoteRule, CsvCellEncoder.CrlfQuoteRule, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var mapping = OuterMapping(field);
        return mapping.Status switch
        {
            "explicitInput" when string.Equals(mapping.InputContract, "ProcessingMonth", StringComparison.Ordinal)
                => processingMonth is { } month
                    ? $"{month.Year:D4}{month.Month:D2}"
                    : throw Unresolvable(field, "the processing month is not available in this record"),
            "existing" when string.Equals(mapping.ModelPath, "Office.OfficeNumber", StringComparison.Ordinal)
                => officeNumber
                   ?? throw Unresolvable(field, "the office number is not available in this record"),
            "generated" => GeneratedFrameValue(
                field, mapping.GeneratorRule!, sequenceNumber, dataRecordCount, dataKind),
            _ => throw Unresolvable(field, $"mapping status '{mapping.Status}' is not supported in the frame"),
        };
    }

    private static string GeneratedFrameValue(
        CsvFieldSpecification field,
        string generatorRule,
        int sequenceNumber,
        int dataRecordCount,
        string? dataKind)
    {
        var rule = CsvGeneratorRuleParser.Parse(generatorRule);
        return rule.Head switch
        {
            "const" => rule.Require("value") is "CRLF" ? string.Empty : rule.Require("value"),
            "constEmpty" => string.Empty,
            "sequence" => sequenceNumber.ToString(CultureInfo.InvariantCulture),
            "recordCount" => dataRecordCount.ToString(CultureInfo.InvariantCulture),
            "lookup" => dataKind ?? throw Unresolvable(field, "the data kind is not available in this record"),
            _ => throw Unresolvable(field, $"generator rule '{rule.Head}' is not supported in the frame"),
        };
    }

    private static CsvFieldMapping OuterMapping(CsvFieldSpecification field) =>
        OuterMappings.Value.TryGetValue(field.FieldId, out var mapping)
            ? mapping
            : throw Unresolvable(field, "the outer field has no mapping");

    private static readonly Lazy<IReadOnlyDictionary<string, CsvFieldMapping>> OuterMappings =
        new(() => CsvSpecificationLoader.LoadEmbedded().MappingByFieldId);

    private static ClaimCsvGenerationException Unresolvable(CsvFieldSpecification field, string detail) =>
        new(field.FieldId, ClaimCsvGenerationReason.UnresolvableRule, detail);
}
