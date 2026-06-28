using Tsumugi.Domain.Entities;

namespace Tsumugi.Domain.Logic;

public static class WageSettingsPolicy
{
    public static WageSettings? Effective(IEnumerable<WageSettings> records, DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(records);
        return records
            .Where(s => s.Period.Contains(asOf))
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefault();
    }
}
