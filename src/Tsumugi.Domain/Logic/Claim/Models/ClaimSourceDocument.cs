namespace Tsumugi.Domain.Logic.Claim.Models;

/// <summary>請求マスタの根拠となる取得済み公式資料。</summary>
public sealed record ClaimSourceDocument
{
    public string DocumentId { get; }
    public string Title { get; }
    public string Publisher { get; }
    public DateOnly EffectiveAt { get; }
    public DateOnly RetrievedAt { get; }
    public string Url { get; }
    public string Sha256 { get; }
    public string? Supersedes { get; }
    public string? Notes { get; }

    public ClaimSourceDocument(
        string documentId,
        string title,
        string publisher,
        DateOnly effectiveAt,
        DateOnly retrievedAt,
        string url,
        string sha256,
        string? supersedes,
        string? notes)
    {
        ValidateRequiredText(documentId, nameof(documentId));
        ValidateRequiredText(title, nameof(title));
        ValidateRequiredText(publisher, nameof(publisher));

        if (effectiveAt == default)
        {
            throw new ArgumentOutOfRangeException(
                nameof(effectiveAt),
                effectiveAt,
                $"出典 '{documentId}' のeffectiveAtをdefaultにできません。");
        }

        if (retrievedAt == default)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retrievedAt),
                retrievedAt,
                $"出典 '{documentId}' のretrievedAtをdefaultにできません。");
        }

        if (string.IsNullOrWhiteSpace(url)
            || HasOuterWhitespace(url)
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"出典 '{documentId}' のURLはabsolute HTTPSで指定してください。",
                nameof(url));
        }

        if (string.IsNullOrWhiteSpace(sha256)
            || sha256.Length != 64
            || sha256.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                $"出典 '{documentId}' のSHA-256は64文字のlowercase hexで指定してください。",
                nameof(sha256));
        }

        if (supersedes is not null
            && (string.IsNullOrWhiteSpace(supersedes) || HasOuterWhitespace(supersedes)))
        {
            throw new ArgumentException(
                $"出典 '{documentId}' のsupersedesを空白にできません。",
                nameof(supersedes));
        }

        if (notes is not null && (string.IsNullOrWhiteSpace(notes) || HasOuterWhitespace(notes)))
            throw new ArgumentException($"出典 '{documentId}' のnotesを空白にできません。", nameof(notes));

        DocumentId = documentId;
        Title = title;
        Publisher = publisher;
        EffectiveAt = effectiveAt;
        RetrievedAt = retrievedAt;
        Url = url;
        Sha256 = sha256;
        Supersedes = supersedes;
        Notes = notes;
    }

    private static void ValidateRequiredText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || HasOuterWhitespace(value))
            throw new ArgumentException("値を空白にできず、前後に空白を含められません。", parameterName);
    }

    private static bool HasOuterWhitespace(string value)
        => value.Length != value.Trim().Length;
}
