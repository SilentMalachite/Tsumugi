using System.Text.Json;

namespace Tsumugi.Infrastructure.Csv.Specifications;

public sealed record CsvSourceDocument(
    string SourceDocumentId,
    string Title,
    string Version,
    string RetrievedAt,
    string Url,
    string Sha256,
    long SizeBytes,
    int? PageCount = null,
    IReadOnlyList<string>? SourceSheets = null,
    IReadOnlyList<int>? ApplicablePages = null,
    IReadOnlyDictionary<string, string>? ApplicablePageTextSha256 = null,
    string? Availability = null,
    string? SupersededBy = null,
    JsonElement? LiveCheck = null,
    int? VerifiedDownloads = null,
    string? Supersedes = null,
    string? ChangeAssessment = null);
