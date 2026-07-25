using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Abstractions;

/// <param name="RegionClassificationCode">CSV へ書く地域区分コード。</param>
/// <param name="UnitPriceMilliYen">単位数単価を1/1000円単位で表した整数。</param>
public sealed record ClaimCsvOfficeContext(string RegionClassificationCode, int UnitPriceMilliYen);

/// <summary>
/// 請求CSVが要求する事業所レベルの制度値（地域区分コード・単位数単価）を制度マスタから解決する抽象。
/// 実値・マスタキーは Application/Domain に置かず、実装側（制度マスタを所有する層）に閉じ込める
/// （CLAUDE.md §ハード制約3）。
/// </summary>
public interface IClaimCsvOfficeContextProvider
{
    ClaimCsvOfficeContext Resolve(RegionGrade regionGrade, ServiceMonth serviceMonth);
}
