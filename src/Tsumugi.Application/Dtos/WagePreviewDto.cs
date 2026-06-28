using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.Dtos;

public sealed record WagePreviewLineDto(Guid RecipientId, int AmountYen, string BasisSummary);

public sealed record WagePreviewDto(
    Guid OfficeId,
    int Year,
    int Month,
    WageMethod Method,
    int TotalFundYen,
    int TotalAllocatedYen,
    IReadOnlyList<WagePreviewLineDto> Lines);
