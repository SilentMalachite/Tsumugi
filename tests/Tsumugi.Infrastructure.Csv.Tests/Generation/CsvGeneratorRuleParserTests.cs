using FluentAssertions;
using Tsumugi.Infrastructure.Csv.Generation;
using Tsumugi.Infrastructure.Csv.Specifications;

namespace Tsumugi.Infrastructure.Csv.Tests.Generation;

public sealed class CsvGeneratorRuleParserTests
{
    [Fact]
    public void Parse_reads_head_target_and_named_arguments()
    {
        var rule = CsvGeneratorRuleParser.Parse(
            "const(target=common:outer:control:001;value=1;source=common-r7-10:p6:item1)");

        rule.Head.Should().Be("const");
        rule.Target.Should().Be("common:outer:control:001");
        rule.Require("value").Should().Be("1");
        rule.Source.Should().Be("common-r7-10:p6:item1");
    }

    [Fact]
    public void Parse_keeps_nested_parentheses_inside_an_argument_value()
    {
        var rule = CsvGeneratorRuleParser.Parse(
            "conditional(target=provider:J121:04:021;"
            + "condition=modelPresent(ClaimInput.UpperLimitManagedAmountYen);"
            + "whenTrue=provider:J121:04:020;whenFalse=provider:J121:04:019;"
            + "source=provider-r7-10:p33:item21)");

        rule.Require("condition").Should().Be("modelPresent(ClaimInput.UpperLimitManagedAmountYen)");
        rule.Require("whenTrue").Should().Be("provider:J121:04:020");
        rule.Require("whenFalse").Should().Be("provider:J121:04:019");
    }

    [Fact]
    public void Parse_splits_comma_separated_list_arguments()
    {
        var rule = CsvGeneratorRuleParser.Parse(
            "sum(target=provider:J111:01:006;"
            + "fields=provider:J111:01:020,provider:J111:01:021,provider:J111:01:023;"
            + "source=provider-r7-10:p17:item6)");

        rule.RequireList("fields").Should().Equal(
            "provider:J111:01:020", "provider:J111:01:021", "provider:J111:01:023");
    }

    [Fact]
    public void Find_returns_null_for_an_absent_optional_argument()
    {
        var rule = CsvGeneratorRuleParser.Parse(
            "count(target=provider:J611:01:045;selector=DailyRecord.MealProvided;value=true;source=s)");

        rule.Find("window").Should().BeNull();
        rule.Find("value").Should().Be("true");
    }

    [Fact]
    public void Require_fails_when_the_argument_is_absent()
    {
        var rule = CsvGeneratorRuleParser.Parse("constEmpty(target=x;reason=r;source=s)");

        var act = () => rule.Require("value");

        act.Should().Throw<CsvGeneratorRuleException>().Which.Target.Should().Be("x");
    }

    [Theory]
    [InlineData("nope(target=x;source=s)")]
    [InlineData("const")]
    [InlineData("const(target=x;source=s")]
    [InlineData("const(target=x;value)")]
    [InlineData("const(value=1;source=s)")]
    [InlineData("const(target=x;value=1;value=2;source=s)")]
    [InlineData("const(target=x;value=modelPresent(a);source=s")]
    public void Parse_fails_closed_on_malformed_or_unknown_rules(string generatorRule)
    {
        var act = () => CsvGeneratorRuleParser.Parse(generatorRule);

        act.Should().Throw<CsvGeneratorRuleException>();
    }

    // NOTE(teeth): spec JSON の全 generatorRule が現行パーサで解析できることを固定する。
    // 新しい head や構文が spec に入ると、ここが RED になって気付ける。
    [Fact]
    public void Every_generator_rule_in_the_embedded_specification_parses_and_targets_its_own_field()
    {
        var catalog = CsvSpecificationLoader.LoadEmbedded();

        var rules = catalog.MappingByFieldId.Values
            .Where(mapping => mapping.GeneratorRule is not null)
            .ToArray();

        rules.Should().HaveCount(375);
        foreach (var mapping in rules)
        {
            var rule = CsvGeneratorRuleParser.Parse(mapping.GeneratorRule!);
            rule.Target.Should().Be(mapping.FieldId);
            rule.Source.Should().NotBeNullOrWhiteSpace();
        }
    }

    // NOTE(teeth): 語彙が spec 側で増減したら気付けるよう、head 集合そのものを固定する。
    [Fact]
    public void The_embedded_specification_uses_exactly_the_known_generator_rule_heads()
    {
        var catalog = CsvSpecificationLoader.LoadEmbedded();

        var heads = catalog.MappingByFieldId.Values
            .Where(mapping => mapping.GeneratorRule is not null)
            .Select(mapping => CsvGeneratorRuleParser.Parse(mapping.GeneratorRule!).Head)
            .ToHashSet(StringComparer.Ordinal);

        heads.Should().BeEquivalentTo(CsvGeneratorRuleParser.KnownHeads);
    }
}
