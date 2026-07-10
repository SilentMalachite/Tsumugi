namespace Tsumugi.Infrastructure.Csv.Specifications;

public sealed record CsvRecordSpecification(
    string RecordId,
    string ExchangeInformationId,
    string InnerRecordType,
    int Order,
    string SourceDocumentId,
    int SourcePage,
    IReadOnlyList<CsvFieldSpecification> Fields);
