using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.UseCases.Office;

/// <summary>
/// 初回起動ウィザード専用の事業所登録入口。
/// 永続化は既存の <see cref="RegisterOfficeUseCase"/> に委譲する。
/// </summary>
public sealed class RegisterFirstRunUseCase(RegisterOfficeUseCase registerOffice)
{
    public Task<OfficeDto> ExecuteAsync(
        RegisterFirstRunInput input, string actor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        // 地域区分未選択の拒否は RegisterOfficeUseCase 側に置く。ここに持つと
        // 事業所管理画面からの登録・更新に同じ規則が掛からず抜け道が残る。
        return registerOffice.ExecuteAsync(
            input.OfficeNumber,
            input.Name,
            input.ServiceCategory,
            input.RegionGrade,
            input.PostalCode,
            input.Address,
            input.PhoneNumber,
            input.RepresentativeTitleAndName,
            actor,
            ct);
    }
}
