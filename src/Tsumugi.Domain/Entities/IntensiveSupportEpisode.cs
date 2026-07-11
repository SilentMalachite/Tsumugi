using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Entities;

/// <summary>利用者の集中的支援開始日を保持する追記型入力。</summary>
public sealed record IntensiveSupportEpisode : Entity
{
    public required Guid OfficeId { get; init; }
    public required Guid RecipientId { get; init; }
    public required Guid RootId { get; init; }
    public required int Revision { get; init; }
    public required RecordKind Kind { get; init; }
    public Guid? ExpectedHeadId { get; init; }
    public required DateOnly? StartDate { get; init; }
}
