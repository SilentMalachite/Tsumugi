using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class AppendOnlyGuardPhase2Tests
{
    [Theory]
    [InlineData(typeof(WorkRecord))]
    [InlineData(typeof(WageFund))]
    [InlineData(typeof(WageSettings))]
    [InlineData(typeof(WageStatement))]
    [InlineData(typeof(AuditEntry))]
    public void Append_only_types_include_phase2_entities(Type t)
    {
        AppendOnlyGuard.GetAppendOnlyTypesForTests().Should().Contain(t);
    }
}
