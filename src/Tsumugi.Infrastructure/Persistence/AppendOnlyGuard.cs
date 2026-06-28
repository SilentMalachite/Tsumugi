using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

/// <summary>
/// 追記不変ガード。取引記録（DailyRecord）と期間マスタ（Certificate/Contract/OfficeCapability）の
/// Modified/Deleted を SaveChanges 前に検出し例外で落とす。同一性マスタは対象外。
/// </summary>
public static class AppendOnlyGuard
{
    private static readonly HashSet<Type> AppendOnlyTypes = new()
    {
        typeof(DailyRecord),
        typeof(Certificate),
        typeof(Contract),
        typeof(OfficeCapability),
        typeof(DisabilityCertificate),
        typeof(FaceSheet),
        typeof(WorkRecord),
        typeof(WageFund),
        typeof(WageSettings),
        typeof(WageStatement),
        typeof(AuditEntry),
    };

    internal static IReadOnlySet<Type> GetAppendOnlyTypesForTests() => AppendOnlyTypes;

    public static void Inspect(ChangeTracker tracker)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        foreach (var entry in tracker.Entries())
        {
            if (!AppendOnlyTypes.Contains(entry.Entity.GetType())) continue;
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new AppendOnlyViolationException(entry.Entity.GetType().Name, entry.State);
        }
    }
}

/// <summary>追記不変違反。コードレビュー時に「破壊更新を意図したコード」が混入したと判断する。</summary>
public sealed class AppendOnlyViolationException(string entityName, EntityState state)
    : InvalidOperationException(
        $"{entityName} は追記専用エンティティのため、{state} 状態での保存はできません。" +
        " 訂正・取消は新レコードとして追加してください。")
{
    public string EntityName { get; } = entityName;
    public EntityState State { get; } = state;
}
