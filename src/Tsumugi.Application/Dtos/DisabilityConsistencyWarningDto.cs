using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;

namespace Tsumugi.Application.Dtos;

/// <summary>
/// 受給者証と現行手帳の種別不整合1件。表示日本語は App が組み立てるため Message は持たない。
/// </summary>
public sealed record DisabilityConsistencyWarningDto(
    Guid RecipientId,
    DisabilityCertificateType Type,
    DisabilityConsistencyDirection Direction);
