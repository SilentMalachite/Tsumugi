namespace Tsumugi.Application.Dtos;

public sealed record RecipientDto(Guid Id, string KanjiName, string KanaName, DateOnly DateOfBirth);
