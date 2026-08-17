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

        // 初回ウィザードでは地域区分未選択を拒否する（既存 OfficeView の挙動は変えない）。
        if (input.RegionGrade == RegionGrade.None)
            throw new ArgumentException("地域区分を選択してください。", nameof(input));

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
