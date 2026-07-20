namespace Tsumugi.Application.Dtos.Claim.Reports;

/// <summary>集中支援加算エピソードの開始日（spec §7.4）。</summary>
public sealed record IntensiveSupportEpisodeDto(DateOnly StartDate);
