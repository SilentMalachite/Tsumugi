namespace Tsumugi.Application.Dtos;

/// <summary>
/// 利用者の DTO。編集画面で楽観的同時実行を成立させるため、画面が読んだ時点の
/// <see cref="ConcurrencyToken"/> を保持し、更新要求時にそのまま UseCase へ戻す。
/// </summary>
public sealed record RecipientDto(
    Guid Id,
    string KanjiName,
    string KanaName,
    DateOnly DateOfBirth,
    Guid ConcurrencyToken);
