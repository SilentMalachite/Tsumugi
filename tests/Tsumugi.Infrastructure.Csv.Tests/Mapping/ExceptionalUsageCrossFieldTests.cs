using FluentAssertions;
using Tsumugi.Application.Claim;
using Tsumugi.Infrastructure.Csv.Mapping;
using Tsumugi.Infrastructure.Csv.Specifications;

namespace Tsumugi.Infrastructure.Csv.Tests.Mapping;

/// <summary>
/// 例外利用日の 4 項目（<c>provider:J121:04:030-033</c>）は、いずれか 1 つでも入力されたら
/// 残り 3 つも必須になる。Phase 3-2 で「孤立 4 フィールド」として残っていた既知の限界を
/// crossFieldGroup 宣言で閉じたことを固定する。
/// </summary>
public sealed class ExceptionalUsageCrossFieldTests
{
    private const string GroupName = "exceptional-usage";

    private static readonly string[] ExceptionalUsageFieldIds =
    [
        "provider:J121:04:030",
        "provider:J121:04:031",
        "provider:J121:04:032",
        "provider:J121:04:033",
    ];

    [Fact]
    public void The_specification_declares_exactly_the_four_exceptional_usage_fields_in_one_group()
    {
        var catalog = CsvSpecificationLoader.LoadEmbedded();

        var grouped = catalog.MappingByFieldId.Values
            .Where(mapping => mapping.CrossFieldGroup is not null)
            .ToArray();

        grouped.Select(mapping => mapping.FieldId).Order(StringComparer.Ordinal)
            .Should().Equal(ExceptionalUsageFieldIds);
        grouped.Should().OnlyContain(mapping => mapping.CrossFieldGroup == GroupName);
        grouped.Should().OnlyContain(mapping => mapping.Status == "missing");
    }

    // 公式の requiredWhen（一次資料に紐づく単項条件）は書き換えていないことを固定する。
    [Fact]
    public void The_official_required_when_of_each_field_stays_a_single_model_condition()
    {
        var catalog = CsvSpecificationLoader.LoadEmbedded();

        foreach (var fieldId in ExceptionalUsageFieldIds)
        {
            var mapping = catalog.MappingByFieldId[fieldId];
            mapping.RequiredCondition.Should().MatchRegex("^model(Present|NonZero)\\(ClaimInput\\.[A-Za-z]+\\)$");
            mapping.RequiredCondition.Should().NotContain("any(");
        }
    }

    [Fact]
    public void Every_exceptional_usage_requirement_uses_an_any_merge_of_all_four_conditions()
    {
        var provider = ClaimInputRequirementProvider.LoadEmbedded();

        var requirements = provider.GetRequirements()
            .Where(requirement => requirement.FieldIds.Any(id =>
                ExceptionalUsageFieldIds.Contains(id, StringComparer.Ordinal)))
            .ToArray();

        requirements.Should().HaveCount(4);
        foreach (var requirement in requirements)
        {
            requirement.Condition.Should().BeOfType<ClaimRequirementCondition.Any>();
            ((ClaimRequirementCondition.Any)requirement.Condition).Conditions.Should().HaveCount(4);
        }
    }

    // NOTE(teeth): 組の外の項目まで Any-merge に巻き込んでいないことを確かめる。
    // （組外の要件が Any を持つこと自体は、CSV と帳票の両マッピングに同じ target が現れる
    //   既存の合成によるもので、本組の条件が混ざっていないことだけを固定する。）
    [Fact]
    public void Requirements_outside_the_group_do_not_inherit_the_group_conditions()
    {
        var provider = ClaimInputRequirementProvider.LoadEmbedded();
        var groupTargetPaths = provider.GetRequirements()
            .Where(requirement => requirement.FieldIds.Any(id =>
                ExceptionalUsageFieldIds.Contains(id, StringComparer.Ordinal)))
            .Select(requirement => requirement.TargetPath)
            .ToHashSet(StringComparer.Ordinal);

        var outside = provider.GetRequirements()
            .Where(requirement => !groupTargetPaths.Contains(requirement.TargetPath))
            .ToArray();

        outside.Should().NotBeEmpty();
        foreach (var requirement in outside)
        {
            var modelPaths = Flatten(requirement.Condition)
                .OfType<ClaimRequirementCondition.ModelPresent>()
                .Select(present => present.ModelPath)
                .ToArray();
            modelPaths.Should().NotContain("ClaimInput.ExceptionalUsageStartMonth");
        }
    }

    private static IEnumerable<ClaimRequirementCondition> Flatten(ClaimRequirementCondition condition) =>
        condition switch
        {
            ClaimRequirementCondition.Any any => any.Conditions.SelectMany(Flatten),
            ClaimRequirementCondition.All all => all.Conditions.SelectMany(Flatten),
            _ => [condition],
        };

    [Fact]
    public void All_four_exceptional_usage_fields_are_surfaced_for_input()
    {
        var provider = ClaimInputRequirementProvider.LoadEmbedded();

        var destinations = provider.GetRequirements()
            .Where(requirement => requirement.FieldIds.Any(id =>
                ExceptionalUsageFieldIds.Contains(id, StringComparer.Ordinal)))
            .Select(requirement => requirement.Destination)
            .Distinct()
            .ToArray();

        destinations.Should().ContainSingle();
    }
}
