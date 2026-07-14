using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Infrastructure.ClaimMasters;

namespace Tsumugi.Infrastructure.Tests.ClaimMasters;

public sealed class ClaimMasterSchemaPhase31Tests
{
    private const string Sha256 =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string R6ServiceCodesSha256 =
        "4a191158daa7dbe8b29c10e966a01960ca8267a747546220aad666dfa5ef5a82";
    private const string R6FeeNoticeSha256 =
        "5d32a1fa54d928be5c91861ecf68490e820768a93b9923a5d8b342c267351d54";
    private const string R6CalculationNoteSha256 =
        "958f9868e4527c27fd050676879b8e6c88b477dbf71c01d5721b7af0bc4f35e1";
    private const string R8ServiceCodesSha256 =
        "307b631ed91a07d4fc9a77b090030b2819731aa018a0374544c1984bf2935049";
    private const string R8CalculationNoteSha256 =
        "0c4f357f4dfd20c71ec0ab8b688db4323d3a4f52c1545fdf46a996cb15485d99";
    private const string CurrentFeeNoticeHtmlSha256 =
        "0b5c75203f589701e8d0d3ba7cf192f4873b7aeae023da6e137882b225286768";
    private const string ProtectedFacilityAdministrativeExpenseStandardHtmlSha256 =
        "e6d94b5279ca33d60daa83f29e6fdb1f5c3d1ba08f076812cf2c0f64a37ba8a5";
    private const string H31FeeNoticeConsolidatedSha256 =
        "79054870b88b1ca97b3b31a811857ed8a614e59da0b6d14435df30bcb5bf4bc9";

    private static readonly string[] AllSupports =
    [
        "service-identity",
        "selectors",
        "unit-rule-kind",
        "unit-rule-value",
        "unit-rule-target",
        "unit-rule-step",
        "unit-rule-rounding",
        "conditions",
        "effective-period",
        "master-values",
    ];

    [Fact]
    public void Load_accepts_a_complete_v2_synthetic_bundle()
    {
        var action = () => Load(ValidMasters());

        action.Should().NotThrow();
    }

    [Fact]
    public void Load_rejects_a_v1_master()
    {
        var masters = ValidMasters();
        MutateRoot(masters, "basic-rewards.json", root => root["schemaVersion"] = "1");

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*basic-rewards.json*schemaVersion*");
    }

    [Fact]
    public void Load_rejects_mixed_v1_and_v2_masters()
    {
        var masters = ValidMasters();
        MutateRoot(masters, "additions.json", root => root["schemaVersion"] = "1");

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*additions.json*schemaVersion*");
    }

    [Fact]
    public void Load_keeps_source_catalog_at_v1()
    {
        var catalog = JsonNode.Parse(ValidCatalogJson)!.AsObject();
        catalog["schemaVersion"] = "2";

        var action = () => CreateProvider(ValidMasters(), catalog.ToJsonString());

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*sources.json*schemaVersion*");
    }

    [Fact]
    public void Load_rejects_unknown_entry_fields()
    {
        var masters = ValidMasters();
        MutateFirstEntry(masters, "basic-rewards.json", entry => entry["unknown"] = true);

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*basic-rewards.json*basic-1*unknown*");
    }

    [Fact]
    public void Load_rejects_unknown_unit_rule_kinds()
    {
        var masters = ValidMasters();
        MutateService(masters, "service-fixed", values =>
            values["unitRule"]!["kind"] = "unknown");

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*service-codes.json*service-fixed*unitRule*");
    }

    [Fact]
    public void Load_reports_union_shape_errors_before_source_reference_errors()
    {
        var masters = ValidMasters();
        MutateFirstEntry(masters, "basic-rewards.json", entry =>
            entry["sourceRefs"]![0]!["sha256"] =
                "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");
        MutateService(masters, "service-unit", values =>
            values["unitRule"]!["kind"] = "unknown-rule");

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*service-codes.json*service-unit*unitRule*");
    }

    [Fact]
    public void Load_reports_catalog_errors_before_cross_entry_semantic_errors()
    {
        var masters = ValidMasters();
        MutateService(masters, "service-factor", values =>
            values["unitRule"]!["factors"]![0]!["conditionSelectors"]!.AsArray()
                .Add("capacity-up-to-20"));

        var action = () => CreateProvider(masters, "{\"schemaVersion\":");

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*sources.json*");
    }

    [Theory]
    [InlineData("add-per-count", "countSelector")]
    [InlineData("add-prorated", "staffCountSelector")]
    [InlineData("add-prorated", "recipientCountSelector")]
    public void Load_reports_catalog_errors_before_runtime_selector_errors(
        string adjustmentKey,
        string selectorField)
    {
        var masters = ValidMasters();
        MutateEntryByKey(masters, "additions.json", adjustmentKey, entry =>
            entry["values"]!["amount"]![selectorField] = "unknown");

        var action = () => CreateProvider(masters, "{\"schemaVersion\":");

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*sources.json*");
    }

    [Theory]
    [InlineData("0.70", true)]
    [InlineData("1", true)]
    [InlineData("00.70", false)]
    [InlineData("01", false)]
    [InlineData("-0.70", false)]
    [InlineData("1.", false)]
    public void Load_enforces_canonical_positive_factor_rates(string rate, bool isValid)
    {
        var masters = ValidMasters();
        MutateService(masters, "service-factor", values =>
            values["unitRule"]!["factors"]![0]!["rate"] = rate);

        var action = () => Load(masters);

        if (isValid)
            action.Should().NotThrow();
        else
            action.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Load_rejects_numeric_json_for_decimal_fields()
    {
        var masters = ValidMasters();
        MutateService(masters, "service-factor", values =>
            values["unitRule"]!["factors"]![0]!["rate"] = 0.7);

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Load_rejects_missing_percentage_application_kind()
    {
        var masters = ValidMasters();
        MutateEntryByKey(masters, "additions.json", "add-percentage", entry =>
            entry["values"]!["amount"]!.AsObject().Remove("applicationKind"));

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*applicationKind*required*");
    }

    [Fact]
    public void Load_rejects_unknown_percentage_application_kind()
    {
        var masters = ValidMasters();
        MutateEntryByKey(masters, "additions.json", "add-percentage", entry =>
            entry["values"]!["amount"]!["applicationKind"] = "unknown");

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*applicationKind*unknown value 'unknown'*");
    }

    [Theory]
    [InlineData("add", PercentageApplicationKind.Add)]
    [InlineData("subtract", PercentageApplicationKind.Subtract)]
    [InlineData("replace", PercentageApplicationKind.Replace)]
    public void Load_accepts_known_percentage_application_kinds(
        string applicationKind,
        PercentageApplicationKind expectedApplicationKind)
    {
        var masters = ValidMasters();
        MutateEntryByKey(masters, "additions.json", "add-percentage", entry =>
            entry["values"]!["amount"]!["applicationKind"] = applicationKind);

        var bundle = LoadBundle(masters);
        UnitAdjustmentMasterRow adjustment = bundle.UnitAdjustments
            .Single(row => row.Key == "add-percentage");
        var amount = adjustment.Amount
            .Should().BeOfType<PercentageOfTargetAmount>().Subject;

        amount.ApplicationKind.Should().Be(expectedApplicationKind);
    }

    [Fact]
    public void Load_rejects_condition_kind_operator_mismatches()
    {
        var masters = ValidMasters();
        MutateCondition(masters, "capacity-up-to-20", condition =>
        {
            condition["operator"] = "in";
            condition.Remove("value");
            condition["values"] = new JsonArray(20);
        });

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*capacity-up-to-20*operator*");
    }

    [Fact]
    public void Load_rejects_missing_required_source_support()
    {
        var masters = ValidMasters();
        MutateService(masters, "service-fixed", values =>
        {
            var source = values.Parent!.AsObject()["sourceRefs"]![0]!.AsObject();
            source["supports"]!.AsArray().Remove(
                source["supports"]!.AsArray().Single(node =>
                    node!.GetValue<string>() == "unit-rule-value"));
        });

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*service-fixed*unit-rule-value*");
    }

    [Fact]
    public void Load_rejects_cross_check_only_provenance()
    {
        var masters = ValidMasters();
        MutateFirstEntry(masters, "basic-rewards.json", entry =>
            entry["sourceRefs"]![0]!["evidenceRole"] = "cross-check");

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*basic-1*has no authoritative source*");
    }

    [Fact]
    public void Load_accepts_a_correction_chain_with_omitted_intermediate_refs()
    {
        var masters = ValidMasters();
        SetAllSourceRefs(
            masters,
            SourceRef("base", "authoritative", AllSupports),
            SourceRef("latest", "correction", AllSupports));
        var catalog = CatalogJson(
            Source("base"),
            Source("middle", "base"),
            Source("latest", "middle"));

        var action = () => CreateProvider(masters, catalog);

        action.Should().NotThrow();
    }

    [Fact]
    public void Load_rejects_correction_branches_with_multiple_maximal_documents()
    {
        var masters = ValidMasters();
        SetAllSourceRefs(
            masters,
            SourceRef("base", "authoritative", AllSupports),
            SourceRef("branch-a", "correction", AllSupports),
            SourceRef("branch-b", "correction", AllSupports));
        var catalog = CatalogJson(
            Source("base"),
            Source("branch-a", "base"),
            Source("branch-b", "base"));

        var action = () => CreateProvider(masters, catalog);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*multiple*authoritative*");
    }

    [Fact]
    public void Load_accepts_one_correction_that_merges_multiple_authoritative_documents()
    {
        var masters = ValidMasters();
        SetAllSourceRefs(
            masters,
            SourceRef("base-a", "authoritative", AllSupports),
            SourceRef("base-b", "authoritative", AllSupports),
            SourceRef("merged", "correction", AllSupports));
        var catalog = CatalogJson(
            Source("base-a"),
            Source("base-b"),
            Source("merged", "base-a", "base-b"));

        var action = () => CreateProvider(masters, catalog);

        action.Should().NotThrow();
    }

    [Fact]
    public void Load_rejects_conflicting_authority_for_an_optional_declared_support()
    {
        var masters = ValidMasters();
        MutateServiceEntry(masters, "service-fixed", entry =>
            entry["sourceRefs"]!.AsArray().Add(
                SourceRef("doc-2", "authoritative", "unit-rule-rounding")));
        var catalog = CatalogJson(Source("doc-1"), Source("doc-2"));

        var action = () => CreateProvider(masters, catalog);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*service-fixed*unit-rule-rounding*multiple*authoritative*");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Load_rejects_mixed_evidence_roles_for_the_same_document_and_support(
        bool correctionFirst)
    {
        var masters = ValidMasters();
        MutateServiceEntry(masters, "service-fixed", entry =>
        {
            var correction = SourceRef("doc-1", "correction", "unit-rule-value");
            correction["locator"] = "source:doc-1:correction";
            if (correctionFirst)
                entry["sourceRefs"]!.AsArray().Insert(0, correction);
            else
                entry["sourceRefs"]!.AsArray().Add(correction);
        });

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*service-fixed*unit-rule-value*multiple evidence roles*");
    }

    [Fact]
    public void Load_rejects_cycles_in_the_reachable_correction_subgraph()
    {
        var masters = ValidMasters();
        SetAllSourceRefs(
            masters,
            SourceRef("base", "authoritative", AllSupports),
            SourceRef("latest", "correction", AllSupports));
        var catalog = CatalogJson(
            Source("base", "latest"),
            Source("latest", "base"));

        var action = () => CreateProvider(masters, catalog);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*cycle*");
    }

    [Fact]
    public void Load_rejects_overlapping_condition_periods()
    {
        var masters = ValidMasters();
        var root = MasterRoot(masters, "service-codes.json");
        var duplicate = ConditionByKey(root, "capacity-up-to-20").DeepClone();
        root["conditionDefinitions"]!.AsArray().Add(duplicate);
        SaveRoot(masters, "service-codes.json", root);

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*capacity-up-to-20*overlap*");
    }

    [Fact]
    public void Load_rejects_unused_condition_definitions()
    {
        var masters = ValidMasters();
        var root = MasterRoot(masters, "service-codes.json");
        root["conditionDefinitions"]!.AsArray()
            .Add(Condition("unused", "capacity", "equals", 1));
        SaveRoot(masters, "service-codes.json", root);

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*unused*");
    }

    [Fact]
    public void Load_rejects_condition_periods_that_do_not_cover_the_service_period()
    {
        var masters = ValidMasters();
        MutateCondition(masters, "capacity-up-to-20", condition =>
            condition["effectiveFrom"] = "2024-05");

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*service-unit*capacity-up-to-20*");
    }

    [Fact]
    public void Load_rejects_empty_condition_intersections()
    {
        var masters = ValidMasters();
        var root = MasterRoot(masters, "service-codes.json");
        root["conditionDefinitions"]!.AsArray().Add(
            Condition("plan-created", "plan-status", "equals", "created"));
        MutateService(root, "service-factor", values =>
            values["conditionSelectors"]!.AsArray().Add("plan-created"));
        SaveRoot(masters, "service-codes.json", root);

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*service-factor*conditionSelectors*");
    }

    [Fact]
    public void Load_accepts_explicit_service_and_condition_retirement()
    {
        var masters = ValidMasters();
        MutateServiceEntry(masters, "service-unit", entry => entry["effectiveTo"] = "2024-05");
        MutateCondition(masters, "capacity-up-to-20", condition =>
            condition["effectiveTo"] = "2024-05");

        var action = () => Load(masters);

        action.Should().NotThrow();
    }

    [Fact]
    public void Load_rejects_missing_component_keys()
    {
        var masters = ValidMasters();
        MutateService(masters, "service-factor", values =>
            values["unitRule"]!["baseComponentKey"] = "missing");

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*service-factor*baseComponentKey*missing*");
    }

    [Fact]
    public void Load_rejects_component_role_mismatches()
    {
        var masters = ValidMasters();
        MutateService(masters, "service-factor", values =>
            values["componentRefs"]![0]!["role"] = "adjustment");

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*service-factor*componentRefs*");
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("calculationStepId")]
    [InlineData("roundingRuleId")]
    [InlineData("billingUnit")]
    public void Load_rejects_each_unit_addition_component_shape_mismatch(string field)
    {
        var masters = ValidMasters();
        MutateService(masters, "service-unit", values =>
        {
            var unitRule = values["unitRule"]!;
            switch (field)
            {
                case "amount":
                    unitRule["amount"]!["unitsPerCount"] = 94;
                    break;
                case "calculationStepId":
                    unitRule[field] = "claim.step.units.service-code.fixed.v1";
                    break;
                case "roundingRuleId":
                    unitRule[field] = "claim.rounding.units.half-up.v1";
                    break;
                case "billingUnit":
                    unitRule[field] = "per-month";
                    break;
            }
        });

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*service-unit*");
    }

    [Fact]
    public void Load_rejects_unknown_runtime_count_selectors()
    {
        var masters = ValidMasters();
        MutateFirstValues(masters, "additions.json", values =>
            values["amount"]!["countSelector"] = "unknown");

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*add-per-count*countSelector*unknown*");
    }

    [Theory]
    [InlineData("staffCountSelector", "unknown")]
    [InlineData("recipientCountSelector", "unknown")]
    [InlineData("poolUnitsPerStaff", 0)]
    public void Load_rejects_each_invalid_proration_field(string field, object value)
    {
        var masters = ValidMasters();
        MutateEntryByKey(masters, "additions.json", "add-prorated", entry =>
            entry["values"]!["amount"]![field] = JsonValue.Create(value));

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>()
            .WithMessage($"*add-prorated*{field}*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData("8")]
    [InlineData(null)]
    public void Load_rejects_invalid_present_proration_maximum(object? value)
    {
        var masters = ValidMasters();
        MutateEntryByKey(masters, "additions.json", "add-prorated", entry =>
            entry["values"]!["amount"]!["maximumRecipientsPerStaff"] =
                JsonValue.Create(value));

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*add-prorated*maximumRecipientsPerStaff*");
    }

    [Theory]
    [InlineData("order", "factors.order")]
    [InlineData("rate", "rate")]
    [InlineData("condition-superset", "factor.conditionSelectors")]
    public void Load_rejects_each_invalid_factor_contract(string invalidCase, string field)
    {
        var masters = ValidMasters();
        MutateService(masters, "service-factor", values =>
        {
            var factor = values["unitRule"]!["factors"]![0]!;
            switch (invalidCase)
            {
                case "order":
                    factor["order"] = 2;
                    break;
                case "rate":
                    factor["rate"] = "1.1";
                    break;
                case "condition-superset":
                    factor["conditionSelectors"]!.AsArray().Add("capacity-up-to-20");
                    break;
            }
        });

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>()
            .WithMessage($"*service-factor*{field}*");
    }

    [Fact]
    public void Load_accepts_existing_factor_chain_with_out_of_sequence_contiguous_orders()
    {
        var masters = ValidMasters();
        MutateService(masters, "service-factor", values =>
        {
            var factors = values["unitRule"]!["factors"]!.AsArray();
            var second = factors[0]!.DeepClone().AsObject();
            second["order"] = 2;
            second["rate"] = "0.5";
            factors.Insert(0, second);
        });

        var action = () => Load(masters);

        action.Should().NotThrow();
    }

    [Fact]
    public void Load_rejects_formula_mode_specific_field_mixing()
    {
        var masters = ValidMasters();
        MutateService(masters, "service-factor", values =>
            values["unitRule"]!["calculationStepId"] =
                "claim.step.units.service-code.base-component-pass-through.v1");

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*service-factor*calculationStepId*not allowed*");
    }

    [Theory]
    [InlineData("mode", "\"other-mode\"", "unitRule.mode")]
    [InlineData("runtimeInputRequirement.key", "\"other-input\"", "unitRule.runtimeInputRequirement.key")]
    [InlineData("runtimeInputRequirement.valueKind", "\"calculated-yen\"", "unitRule.runtimeInputRequirement.valueKind")]
    [InlineData("runtimeInputRequirement.valueUnit", "\"yen-per-month\"", "unitRule.runtimeInputRequirement.valueUnit")]
    [InlineData("runtimeInputRequirement.scope", "\"facility-only\"", "unitRule.runtimeInputRequirement.scope")]
    [InlineData("runtimeInputRequirement.asOfPolicy", "\"latest\"", "unitRule.runtimeInputRequirement.asOfPolicy")]
    [InlineData("runtimeInputRequirement.provenancePolicyId", "\"claim.input.other.v1\"", "unitRule.runtimeInputRequirement.provenancePolicyId")]
    [InlineData("statutoryFormula.daysDivisor", "30", "unitRule.statutoryFormula.daysDivisor")]
    [InlineData("statutoryFormula.expenseAdjustmentDivisor", "\"0.946\"", "unitRule.statutoryFormula.expenseAdjustmentDivisor")]
    [InlineData("statutoryFormula.unitPriceDivisorYen", "11", "unitRule.statutoryFormula.unitPriceDivisorYen")]
    [InlineData("statutoryFormula.fixedAdditionUnits", "24", "unitRule.statutoryFormula.fixedAdditionUnits")]
    [InlineData("statutoryFormula.upliftRate", "\"1.047\"", "unitRule.statutoryFormula.upliftRate")]
    [InlineData("statutoryFormula.calculationStepId", "\"claim.step.other.v1\"", "unitRule.statutoryFormula.calculationStepId")]
    [InlineData("statutoryFormula.roundingRuleId", "\"claim.rounding.units.floor.v1\"", "unitRule.statutoryFormula.roundingRuleId")]
    [InlineData("benchmark.officialSection", "\"other-section\"", "unitRule.benchmark.officialSection")]
    [InlineData("benchmark.basicRewardStaffingKey", "\"other-staffing\"", "unitRule.benchmark.basicRewardStaffingKey")]
    [InlineData("benchmark.paymentBandMatch", "\"other-payment-band\"", "unitRule.benchmark.paymentBandMatch")]
    [InlineData("benchmark.capacityMatch", "\"other-capacity\"", "unitRule.benchmark.capacityMatch")]
    [InlineData("benchmark.localGovernmentAdjustment.conditionSelector", "\"municipality-ownership:other\"", "unitRule.benchmark.localGovernmentAdjustment.conditionSelector")]
    [InlineData("benchmark.localGovernmentAdjustment.rate", "\"0.966\"", "unitRule.benchmark.localGovernmentAdjustment.rate")]
    [InlineData("benchmark.localGovernmentAdjustment.target", "\"formula-wide\"", "unitRule.benchmark.localGovernmentAdjustment.target")]
    [InlineData("benchmark.localGovernmentAdjustment.calculationStepId", "\"claim.step.other.v1\"", "unitRule.benchmark.localGovernmentAdjustment.calculationStepId")]
    [InlineData("benchmark.localGovernmentAdjustment.roundingRuleId", "\"claim.rounding.units.floor.v1\"", "unitRule.benchmark.localGovernmentAdjustment.roundingRuleId")]
    [InlineData("selection.kind", "\"maximum\"", "unitRule.selection.kind")]
    [InlineData("selection.calculationStepId", "\"claim.step.other.v1\"", "unitRule.selection.calculationStepId")]
    [InlineData("selection.roundingRuleId", "\"claim.rounding.units.half-up.v1\"", "unitRule.selection.roundingRuleId")]
    [InlineData("billingUnit", "\"per-month\"", "unitRule.billingUnit")]
    public void Load_rejects_invalid_protected_facility_fixed_contract(
        string path,
        string invalidJson,
        string field)
    {
        var masters = ProtectedFacilityRepresentativeMasters();
        MutateService(masters, "service-pass-through", values =>
            SetJsonPath(values["unitRule"]!.AsObject(), path, invalidJson));

        var action = () => LoadBundle(masters, RepresentativeCatalogJson);

        action.Should().Throw<InvalidDataException>()
            .WithMessage($"*service-pass-through*{field}*");
    }

    [Theory]
    [InlineData("base-component-key")]
    [InlineData("component-ref")]
    public void Load_rejects_invalid_protected_facility_component_contract(string invalidCase)
    {
        var masters = ProtectedFacilityRepresentativeMasters();
        MutateService(masters, "service-pass-through", values =>
        {
            if (invalidCase == "base-component-key")
                values["unitRule"]!["baseComponentKey"] = "basic-1";
            else
                values["componentRefs"]!.AsArray().Add(new JsonObject
                {
                    ["masterKind"] = "basic-rewards",
                    ["key"] = "basic-1",
                    ["role"] = "base",
                });
        });

        var action = () => LoadBundle(masters, RepresentativeCatalogJson);

        action.Should().Throw<InvalidDataException>()
            .WithMessage(invalidCase == "base-component-key"
                ? "*service-pass-through*baseComponentKey*"
                : "*service-pass-through*componentRefs*");
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("period-insufficient")]
    public void Load_rejects_invalid_protected_facility_local_condition(string invalidCase)
    {
        var masters = ProtectedFacilityRepresentativeMasters();
        var root = MasterRoot(masters, "service-codes.json");
        var condition = ConditionByKey(root, "municipality-ownership:local-government");
        if (invalidCase == "missing")
            root["conditionDefinitions"]!.AsArray().Remove(condition);
        else
            condition["effectiveFrom"] = "2024-05";
        SaveRoot(masters, "service-codes.json", root);

        var action = () => LoadBundle(masters, RepresentativeCatalogJson);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*localGovernmentAdjustment.conditionSelector*");
    }

    [Theory]
    [InlineData("gap", 3)]
    [InlineData("duplicate", 1)]
    public void Load_rejects_invalid_protected_facility_factor_order(
        string _,
        int secondOrder)
    {
        var masters = ProtectedFacilityRepresentativeMasters();
        MutateService(masters, "service-two-factor", values =>
            values["unitRule"]!["factors"]![1]!["order"] = secondOrder);

        var action = () => LoadBundle(masters, RepresentativeCatalogJson);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*service-two-factor*factors.order*unique and contiguous*");
    }

    [Fact]
    public void Load_rejects_invalid_protected_facility_factor_array_order()
    {
        var masters = ProtectedFacilityRepresentativeMasters();
        MutateService(masters, "service-two-factor", values =>
        {
            var factors = values["unitRule"]!["factors"]!.AsArray();
            var first = factors[0]!.DeepClone();
            factors[0] = factors[1]!.DeepClone();
            factors[1] = first;
        });

        var action = () => LoadBundle(masters, RepresentativeCatalogJson);

        action.Should().Throw<InvalidDataException>()
            .WithMessage(
                "*service-two-factor*unitRule.factors.order*one-based array position*");
    }

    [Fact]
    public void Load_rejects_invalid_protected_facility_factor_condition_subset()
    {
        var masters = ProtectedFacilityRepresentativeMasters();
        MutateService(masters, "service-factor", values =>
            values["unitRule"]!["factors"]![0]!["conditionSelectors"]!.AsArray()
                .Add("capacity-up-to-20"));

        var action = () => LoadBundle(masters, RepresentativeCatalogJson);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*service-factor*factor.conditionSelectors*subset*");
    }

    [Theory]
    [InlineData("unit-rule-formula")]
    [InlineData("unit-rule-comparison")]
    [InlineData("unit-rule-local-government-adjustment")]
    [InlineData("unit-rule-runtime-input")]
    [InlineData("unit-rule-runtime-input-provenance")]
    [InlineData("unit-rule-step")]
    [InlineData("unit-rule-rounding")]
    public void Load_rejects_invalid_protected_facility_no_factor_support(string support)
    {
        var masters = ProtectedFacilityRepresentativeMasters();
        RemoveSourceSupport(masters, "service-pass-through", support);

        var action = () => LoadBundle(masters, RepresentativeCatalogJson);

        action.Should().Throw<InvalidDataException>()
            .WithMessage($"*service-pass-through*{support}*has no authoritative source*");
    }

    [Theory]
    [InlineData("unit-rule-value")]
    [InlineData("unit-rule-target")]
    [InlineData("unit-rule-step")]
    [InlineData("unit-rule-rounding")]
    public void Load_rejects_invalid_protected_facility_factor_support(string support)
    {
        var masters = ProtectedFacilityRepresentativeMasters();
        RemoveSourceSupport(masters, "service-factor", support);

        var action = () => LoadBundle(masters, RepresentativeCatalogJson);

        action.Should().Throw<InvalidDataException>()
            .WithMessage($"*service-factor*{support}*has no authoritative source*");
    }

    [Theory]
    [InlineData("calculationStepId", "claim.step.other.v1")]
    [InlineData("roundingRuleId", "claim.rounding.units.floor.v1")]
    public void Load_rejects_invalid_protected_facility_factor_step_contract(
        string field,
        string invalidValue)
    {
        var masters = ProtectedFacilityRepresentativeMasters();
        MutateService(masters, "service-factor", values =>
            values["unitRule"]!["factors"]![0]![field] = invalidValue);

        var action = () => LoadBundle(masters, RepresentativeCatalogJson);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*service-factor*factor*invalid step or rounding rule*");
    }

    [Fact]
    public void Load_rejects_invalid_protected_facility_cross_check_only_source()
    {
        var masters = ProtectedFacilityRepresentativeMasters();
        RemoveSourceSupport(masters, "service-pass-through", "unit-rule-formula");

        var action = () => LoadBundle(masters, RepresentativeCatalogJson);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*service-pass-through*unit-rule-formula*has no authoritative source*");
    }

    [Theory]
    [InlineData("service-pass-through", "unit-rule-formula", "current-fee-notice-html", "r6-calculation-note")]
    [InlineData("service-pass-through", "unit-rule-step", "r6-calculation-note", "current-fee-notice-html")]
    [InlineData("service-pass-through", "unit-rule-rounding", "r6-calculation-note", "current-fee-notice-html")]
    [InlineData("service-pass-through", "unit-rule-runtime-input-provenance", "protected-facility-administrative-expense-standard-html", "current-fee-notice-html")]
    [InlineData("service-pass-through", "service-identity", "r6-service-codes-2-xlsx", "current-fee-notice-html")]
    [InlineData("service-factor", "unit-rule-value", "r6-service-codes-2-xlsx", "current-fee-notice-html")]
    [InlineData("service-factor", "unit-rule-target", "r6-service-codes-2-xlsx", "current-fee-notice-html")]
    public void Load_rejects_invalid_protected_facility_source_authority_substitution(
        string serviceKey,
        string support,
        string expectedDocumentId,
        string substitutedDocumentId)
    {
        var masters = ProtectedFacilityRepresentativeMasters();
        MoveSourceSupport(
            masters,
            serviceKey,
            support,
            expectedDocumentId,
            substitutedDocumentId);

        var action = () => LoadBundle(masters, RepresentativeCatalogJson);

        action.Should().Throw<InvalidDataException>()
            .WithMessage($"*{serviceKey}*{support}*{expectedDocumentId}*");
    }

    [Fact]
    public void Load_accepts_protected_facility_r8_period_source_authority()
    {
        var bundle = LoadBundle(
            R8ProtectedFacilityRepresentativeMasters(),
            RepresentativeCatalogJson);

        var service = bundle.ServiceCodes.Single(row => row.Key == "service-pass-through");
        service.ServiceCode.Should().Be("462841");
        service.OfficialLabel.Should().Be("就継Ｂ基準該当");
        service.SourceRefs.Should().ContainSingle(source =>
            source.DocumentId == "r8-service-codes-2-xlsx"
            && source.Sha256 == R8ServiceCodesSha256
            && source.Locator == "workbook-order=38;row=1987"
            && source.EvidenceRole == ClaimSourceEvidenceRole.Authoritative);

        var localGovernment = bundle.ConditionDefinitions.Single(condition =>
            condition.Key == "municipality-ownership:local-government"
            && condition.EffectiveFrom.ToString() == "2026-06");
        localGovernment.SourceRefs.Should().ContainSingle(source =>
            source.DocumentId == "r8-service-codes-2-xlsx"
            && source.Sha256 == R8ServiceCodesSha256
            && source.Locator == "workbook-order=38;row=1987"
            && source.EvidenceRole == ClaimSourceEvidenceRole.Authoritative);
    }

    [Theory]
    [InlineData("r8-service-codes-2-xlsx", "r6-service-codes-2-xlsx", "service-identity")]
    [InlineData("r8-calculation-note", "r6-calculation-note", "unit-rule-step")]
    public void Load_rejects_invalid_protected_facility_r8_source_authority(
        string expectedDocumentId,
        string substitutedDocumentId,
        string support)
    {
        var masters = R8ProtectedFacilityRepresentativeMasters();
        MutateServiceEntry(masters, "service-pass-through", entry =>
        {
            var sourceRef = SourceRefByDocument(entry, expectedDocumentId);
            sourceRef["documentId"] = substitutedDocumentId;
            sourceRef["sha256"] = SourceSha256(substitutedDocumentId);
        });

        var action = () => LoadBundle(masters, RepresentativeCatalogJson);

        action.Should().Throw<InvalidDataException>()
            .WithMessage($"*service-pass-through*{support}*{expectedDocumentId}*");
    }

    [Fact]
    public void Load_rejects_invalid_protected_facility_source_period_crossing_r8_boundary()
    {
        var masters = ProtectedFacilityRepresentativeMasters();
        MutateServiceEntry(masters, "service-pass-through", entry =>
            entry["effectiveTo"] = null);
        MutateCondition(
            masters,
            "municipality-ownership:local-government",
            condition => condition["effectiveTo"] = null);

        var action = () => LoadBundle(masters, RepresentativeCatalogJson);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*service-pass-through*effectiveFrom/effectiveTo*R8 boundary*");
    }

    [Fact]
    public void Load_rejects_r8_protected_facility_condition_with_r6_source_authority()
    {
        var masters = R8ProtectedFacilityRepresentativeMasters();
        MutateCondition(
            masters,
            "municipality-ownership:local-government",
            "2026-06",
            condition =>
            {
                var sourceRef = condition["sourceRefs"]![0]!.AsObject();
                sourceRef["documentId"] = "r6-service-codes-2-xlsx";
                sourceRef["sha256"] = R6ServiceCodesSha256;
            });

        var action = () => LoadBundle(masters, RepresentativeCatalogJson);

        action.Should().Throw<InvalidDataException>()
            .WithMessage(
                "*municipality-ownership:local-government*conditions*r8-service-codes-2-xlsx*");
    }

    [Theory]
    [InlineData("municipality-ownership:local-government")]
    [InlineData("plan-not-created")]
    public void Load_rejects_r6_protected_facility_condition_crossing_r8_boundary(
        string conditionSelector)
    {
        var masters = ProtectedFacilityRepresentativeMasters();
        MutateCondition(masters, conditionSelector, condition =>
            condition["effectiveTo"] = null);

        var action = () => LoadBundle(masters, RepresentativeCatalogJson);

        action.Should().Throw<InvalidDataException>()
            .WithMessage(
                $"*{conditionSelector}*effectiveFrom/effectiveTo*R8 boundary*");
    }

    [Fact]
    public void Load_rejects_adjustment_selector_cycles()
    {
        var masters = ValidMasters();
        var additions = MasterRoot(masters, "additions.json");
        var first = EntryByKey(additions, "add-percentage");
        first["key"] = "selector:a";
        first["values"]!["amount"]!["targetSelector"] = "selector:b";
        var second = first.DeepClone().AsObject();
        second["key"] = "selector:b";
        second["values"]!["amount"]!["targetSelector"] = "selector:a";
        additions["entries"]!.AsArray().Add(second);
        SaveRoot(masters, "additions.json", additions);
        MutateService(masters, "service-fixed", values =>
            values["selectors"] = Strings("selector:fixed", "selector:a", "selector:b"));

        var action = () => Load(masters);

        action.Should().Throw<InvalidDataException>()
            .WithMessage("*cycle*");
    }

    [Fact]
    public void Load_accepts_nonoverlapping_adjustment_revisions_in_cycle_graph()
    {
        var masters = ValidMasters();
        var additions = MasterRoot(masters, "additions.json");
        var first = EntryByKey(additions, "add-percentage");
        first["effectiveTo"] = "2024-05";
        var successor = first.DeepClone().AsObject();
        successor["effectiveFrom"] = "2024-06";
        successor["effectiveTo"] = null;
        additions["entries"]!.AsArray().Add(successor);
        SaveRoot(masters, "additions.json", additions);

        var action = () => Load(masters);

        action.Should().NotThrow();
    }

    [Fact]
    public void Load_accepts_nonoverlapping_service_revisions_in_cycle_graph()
    {
        var masters = ValidMasters();
        var percentageAmount = new JsonObject
        {
            ["kind"] = "percentage-of-target",
            ["percentage"] = "0.10",
            ["applicationKind"] = "add",
            ["percentageBaseScope"] = "per-service-code-unit",
            ["targetSelector"] = "selector:fixed",
            ["calculationOrder"] = 2,
        };
        MutateEntryByKey(masters, "additions.json", "add-per-count", entry =>
        {
            entry["values"]!["amount"] = percentageAmount.DeepClone();
            entry["values"]!["calculationStepId"] =
                "claim.step.units.per-service-code.percentage.v1";
            entry["values"]!["roundingRuleId"] =
                "claim.rounding.units.half-up.v1";
        });
        MutateService(masters, "service-unit", values =>
        {
            values["unitRule"]!["amount"] = percentageAmount.DeepClone();
            values["unitRule"]!["calculationStepId"] =
                "claim.step.units.per-service-code.percentage.v1";
            values["unitRule"]!["roundingRuleId"] =
                "claim.rounding.units.half-up.v1";
        });
        var services = MasterRoot(masters, "service-codes.json");
        var first = EntryByKey(services, "service-unit");
        first["effectiveTo"] = "2024-05";
        var successor = first.DeepClone().AsObject();
        successor["effectiveFrom"] = "2024-06";
        successor["effectiveTo"] = null;
        services["entries"]!.AsArray().Add(successor);
        SaveRoot(masters, "service-codes.json", services);

        var action = () => Load(masters);

        action.Should().NotThrow();
    }

    [Fact]
    public void Load_reads_representative_gap_rows_into_typed_unit_rules()
    {
        var bundle = LoadBundle(RepresentativeMasters(), RepresentativeCatalogJson);

        var fixedRow = bundle.ServiceCodes.Single(row => row.Key == "service-fixed");
        fixedRow.UnitRule
            .Should().Be(new FixedCompositeUnitRule(837, BillingUnit.PerDay));
        fixedRow.ConditionSelectors.Should().BeEquivalentTo(
            "reward-system-i",
            "capacity-up-to-20",
            "average-wage-45000-or-more");
        fixedRow.ComponentRefs.Should().ContainSingle(component =>
            component.MasterKind == ClaimComponentMasterKind.BasicRewards
            && component.Key == "basic-1"
            && component.Role == ClaimComponentRole.Base);
        AssertR6ServiceCodeSource(fixedRow, "workbook-order=38;row=7");

        var unitRow = bundle.ServiceCodes.Single(row => row.Key == "service-unit");
        var perCount = unitRow.UnitRule
            .Should().BeOfType<UnitAdditionRule>().Subject.Amount
            .Should().BeOfType<UnitsPerCountAmount>().Subject;
        perCount.UnitsPerCount.Should().Be(93);
        perCount.CountSelector.Should().Be("previous-year-six-month-employment-count");
        unitRow.ConditionSelectors.Should().BeEquivalentTo(
            "capacity-up-to-20",
            "payment-band-1",
            "employment-outcome-at-least-1");
        unitRow.ComponentRefs.Should().ContainSingle(component =>
            component.MasterKind == ClaimComponentMasterKind.Additions
            && component.Key == "add-per-count"
            && component.Role == ClaimComponentRole.Adjustment);
        AssertR6ServiceCodeSource(unitRow, "workbook-order=38;row=941");

        var factorRow = bundle.ServiceCodes.Single(row => row.Key == "service-factor");
        var protectedFacility = factorRow.UnitRule
            .Should().BeOfType<ProtectedFacilityBenchmarkMinimumRule>().Subject;
        var factor = protectedFacility.Factors.Single();
        factor.Rate.Should().Be(0.7m);
        factor.ConditionSelectors.Should().Equal("plan-not-created");
        factorRow.ConditionSelectors.Should().BeEquivalentTo(factor.ConditionSelectors);
        factorRow.ComponentRefs.Should().BeEmpty();
        AssertProtectedFacilityContract(protectedFacility);
        AssertR6ServiceCodeSource(factorRow, "workbook-order=38;row=908");
    }

    [Fact]
    public void Load_accepts_signed_proration_and_pass_through_boundary_fixtures()
    {
        var masters = ProtectedFacilityRepresentativeMasters();
        AddProratedBoundaryFixtures(masters);
        AddService(
            masters,
            EntryWithSources(
                "service-negative",
                """
                {
                  "serviceCode": "462913",
                  "officialLabel": "就継Ｂ身体拘束廃止未実施減算",
                  "serviceKind": "employment-continuation-support-b",
                  "selectors": ["selector:negative"],
                  "conditionSelectors": [],
                  "unitRule": {
                    "kind": "fixed-composite-unit",
                    "finalUnits": -5,
                    "billingUnit": "per-day"
                  },
                  "componentRefs": []
                }
                """,
                "2024-04",
                "2026-05",
                R6ServiceCodeSourceRef(
                    "workbook-order=38;row=913",
                    "service-identity",
                    "selectors",
                    "unit-rule-kind",
                    "unit-rule-value",
                    "effective-period")));

        var bundle = LoadBundle(masters, RepresentativeCatalogJson);

        var negative = bundle.ServiceCodes.Single(row => row.Key == "service-negative");
        negative.UnitRule
            .Should().Be(new FixedCompositeUnitRule(-5, BillingUnit.PerDay));
        AssertR6ServiceCodeSource(negative, "workbook-order=38;row=913");

        var prorated = bundle.ServiceCodes.Single(row => row.Key == "service-prorated");
        var proratedAmount = prorated.UnitRule
            .Should().BeOfType<UnitAdditionRule>().Subject.Amount
            .Should().BeOfType<ProratedUnitsAmount>().Subject;
        proratedAmount.MaximumRecipientsPerStaff.Should().BeNull();
        prorated.SourceRefs.Should().Contain(source =>
            source.DocumentId == "r6-fee-notice"
            && source.Sha256 == R6FeeNoticeSha256
            && source.Supports.SequenceEqual(
                new[] { ClaimSourceSupport.UnitRuleValue }));
        AssertR6ServiceCodeSource(prorated, "workbook-order=38;row=1044");

        var noFactor = bundle.ServiceCodes.Single(row => row.Key == "service-pass-through");
        var noFactorRule = noFactor.UnitRule
            .Should().BeOfType<ProtectedFacilityBenchmarkMinimumRule>().Subject;
        noFactorRule.Factors.Should().BeEmpty();
        noFactor.ComponentRefs.Should().BeEmpty();
        AssertProtectedFacilityContract(noFactorRule);
        AssertR6ServiceCodeSource(noFactor, "workbook-order=38;row=907");

        var twoFactor = bundle.ServiceCodes.Single(row => row.Key == "service-two-factor");
        var twoFactorRule = twoFactor.UnitRule
            .Should().BeOfType<ProtectedFacilityBenchmarkMinimumRule>().Subject;
        twoFactorRule.Factors.Select(factor => (factor.Order, factor.Rate))
            .Should().Equal((1, 0.7m), (2, 0.5m));
        twoFactorRule.Factors.SelectMany(factor =>
                new[] { factor.CalculationStepId, factor.RoundingRuleId })
            .Should().Equal(
                "claim.step.units.per-service-code.percentage.v1",
                "claim.rounding.units.half-up.v1",
                "claim.step.units.per-service-code.percentage.v1",
                "claim.rounding.units.half-up.v1");
        twoFactor.ComponentRefs.Should().BeEmpty();
        AssertProtectedFacilityContract(twoFactorRule);
        AssertR6ServiceCodeSource(twoFactor, "workbook-order=40;row=1809");
    }

    [Fact]
    public void Load_accepts_matching_bounded_proration_amounts()
    {
        var masters = RepresentativeMasters();
        AddProratedBoundaryFixtures(masters);
        MutateEntryByKey(masters, "additions.json", "add-prorated", entry =>
            entry["values"]!["amount"]!["maximumRecipientsPerStaff"] = 8);
        MutateService(masters, "service-prorated", values =>
            values["unitRule"]!["amount"]!["maximumRecipientsPerStaff"] = 8);

        var bundle = LoadBundle(masters, RepresentativeCatalogJson);

        var componentAmount = bundle.UnitAdjustments
            .Single(row => row.Key == "add-prorated").Amount
            .Should().BeOfType<ProratedUnitsAmount>().Subject;
        componentAmount.MaximumRecipientsPerStaff.Should().Be(8);
        var serviceAmount = bundle.ServiceCodes
            .Single(row => row.Key == "service-prorated").UnitRule
            .Should().BeOfType<UnitAdditionRule>().Subject.Amount
            .Should().BeOfType<ProratedUnitsAmount>().Subject;
        serviceAmount.MaximumRecipientsPerStaff.Should().Be(8);
    }

    [Fact]
    public void Load_validates_14709_service_rows_with_indexed_lookups()
    {
        var masters = ValidMasters();
        var serviceRoot = MasterRoot(masters, "service-codes.json");
        serviceRoot["conditionDefinitions"] = new JsonArray();
        var entries = new JsonArray();
        for (var index = 0; index < 14_709; index++)
        {
            var serviceCode = index == 0
                ? "462980"
                : (500_000 + index).ToString(CultureInfo.InvariantCulture);
            entries.Add(JsonNode.Parse(Entry(
                $"service-{index}",
                $$"""
                {
                  "serviceCode": "{{serviceCode}}",
                  "officialLabel": "サービス{{index}}",
                  "serviceKind": "employment-continuation-support-b",
                  "selectors": ["selector:{{index}}"],
                  "conditionSelectors": [],
                  "unitRule": {
                    "kind": "fixed-composite-unit",
                    "finalUnits": 1,
                    "billingUnit": "per-day"
                  },
                  "componentRefs": []
                }
                """,
                AllSupports)));
        }

        serviceRoot["entries"] = entries;
        SaveRoot(masters, "service-codes.json", serviceRoot);
        MutateEntryByKey(masters, "additions.json", "add-percentage", entry =>
            entry["values"]!["amount"]!["targetSelector"] = "selector:0");

        var action = () => Load(masters);

        action.Should().NotThrow();
    }

    internal static JsonClaimMasterProvider CreateProvider(
        IReadOnlyDictionary<string, string> masterJsons) =>
        CreateProvider(masterJsons, ValidCatalogJson);

    internal static JsonClaimMasterProvider CreateProvider(
        IReadOnlyDictionary<string, string> masterJsons,
        string catalogJson)
    {
        using var sources = StreamOf(catalogJson);
        var streams = masterJsons.ToDictionary(
            pair => pair.Key,
            pair => (Stream)StreamOf(pair.Value),
            StringComparer.Ordinal);
        try
        {
            return JsonClaimMasterProvider.LoadPolicy(sources, streams);
        }
        finally
        {
            foreach (var stream in streams.Values)
                stream.Dispose();
        }
    }

    private static void AddProratedBoundaryFixtures(Dictionary<string, string> masters)
    {
        MutateEntryByKey(masters, "additions.json", "add-prorated", entry =>
        {
            entry["effectiveTo"] = "2026-05";
            entry["sourceRefs"] = new JsonArray(
                R6FeeNoticeSourceRef(
                    "pages=129-136;medical-coordination-v",
                    "master-values",
                    "effective-period"));
        });
        AddService(
            masters,
            EntryWithSources(
                "service-prorated",
                """
                {
                  "serviceCode": "469992",
                  "officialLabel": "就継Ｂ医療連携体制加算Ⅴ",
                  "serviceKind": "employment-continuation-support-b",
                  "selectors": ["selector:prorated"],
                  "conditionSelectors": [],
                  "unitRule": {
                    "kind": "unit-addition",
                    "adjustmentComponentKey": "add-prorated",
                    "amount": {
                      "kind": "prorated-units",
                      "poolUnitsPerStaff": 500,
                      "staffCountSelector": "medical-coordination-v-visiting-nurse-count",
                      "recipientCountSelector": "medical-coordination-v-supported-recipient-count"
                    },
                    "calculationStepId": "claim.step.units.service-code.prorate-by-recipient-count.v1",
                    "roundingRuleId": "claim.rounding.units.half-up.v1",
                    "billingUnit": "per-day"
                  },
                  "componentRefs": [
                    { "masterKind": "additions", "key": "add-prorated", "role": "adjustment" }
                  ]
                }
                """,
                "2024-04",
                "2026-05",
                R6FeeNoticeSourceRef(
                    "pages=129-136;medical-coordination-v",
                    "unit-rule-value"),
                R6ServiceCodeSourceRef(
                    "workbook-order=38;row=1044",
                    "service-identity",
                    "selectors",
                    "unit-rule-kind",
                    "unit-rule-target",
                    "unit-rule-step",
                    "unit-rule-rounding",
                    "effective-period")));
    }

    private static Dictionary<string, string> ProtectedFacilityRepresentativeMasters()
    {
        var masters = RepresentativeMasters();
        var root = MasterRoot(masters, "service-codes.json");
        root["conditionDefinitions"]!.AsArray().Add(
            R6ServiceCodeCondition(
                "staff-shortage",
                "staffing",
                "equals",
                "staff-shortage",
                "workbook-order=40;row=1809"));
        SaveRoot(masters, "service-codes.json", root);

        AddService(
            masters,
            EntryWithSources(
                "service-pass-through",
                ProtectedFacilityServiceValues(
                    "462841",
                    "就継Ｂ基準該当",
                    "selector:pass-through",
                    "[]",
                    "[]"),
                "2024-04",
                "2026-05",
                ProtectedFacilitySourceRefs(
                    "workbook-order=38;row=907",
                    hasFactors: false)));
        AddService(
            masters,
            EntryWithSources(
                "service-two-factor",
                ProtectedFacilityServiceValues(
                    "46C843",
                    "就継Ｂ基準該当・人欠１・未計画２",
                    "selector:two-factor",
                    "[\"staff-shortage\", \"plan-not-created\"]",
                    """
                    [
                      {
                        "order": 1,
                        "rate": "0.7",
                        "conditionSelectors": ["staff-shortage"],
                        "calculationStepId": "claim.step.units.per-service-code.percentage.v1",
                        "roundingRuleId": "claim.rounding.units.half-up.v1"
                      },
                      {
                        "order": 2,
                        "rate": "0.5",
                        "conditionSelectors": ["plan-not-created"],
                        "calculationStepId": "claim.step.units.per-service-code.percentage.v1",
                        "roundingRuleId": "claim.rounding.units.half-up.v1"
                      }
                    ]
                    """),
                "2024-04",
                "2026-05",
                ProtectedFacilitySourceRefs(
                    "workbook-order=40;row=1809",
                    hasFactors: true)));
        return masters;
    }

    private static Dictionary<string, string> R8ProtectedFacilityRepresentativeMasters()
    {
        var masters = ProtectedFacilityRepresentativeMasters();
        MutateServiceEntry(masters, "service-pass-through", entry =>
        {
            entry["effectiveFrom"] = "2026-06";
            entry["effectiveTo"] = null;
            entry["sourceRefs"] = new JsonArray(
                ProtectedFacilitySourceRefs(
                    "workbook-order=38;row=1987",
                    hasFactors: false,
                    isR8: true));
        });

        var root = MasterRoot(masters, "service-codes.json");
        var localGovernment = ConditionByKey(
            root,
            "municipality-ownership:local-government");
        var r8LocalGovernment = localGovernment.DeepClone().AsObject();
        r8LocalGovernment["effectiveFrom"] = "2026-06";
        r8LocalGovernment["effectiveTo"] = null;
        r8LocalGovernment["sourceRefs"] = new JsonArray(
            OfficialSourceRef(
                "r8-service-codes-2-xlsx",
                R8ServiceCodesSha256,
                "workbook-order=38;row=1987",
                "conditions",
                "effective-period"));
        root["conditionDefinitions"]!.AsArray().Add(r8LocalGovernment);
        SaveRoot(masters, "service-codes.json", root);
        return masters;
    }

    private static string ProtectedFacilityServiceValues(
        string serviceCode,
        string officialLabel,
        string selector,
        string conditionSelectorsJson,
        string factorsJson) => $$"""
        {
          "serviceCode": "{{serviceCode}}",
          "officialLabel": "{{officialLabel}}",
          "serviceKind": "employment-continuation-support-b",
          "selectors": ["{{selector}}"],
          "conditionSelectors": {{conditionSelectorsJson}},
          "unitRule": {{ProtectedFacilityUnitRuleJson(factorsJson)}},
          "componentRefs": []
        }
        """;

    private static string ProtectedFacilityUnitRuleJson(string factorsJson) => $$"""
        {
          "kind": "formula",
          "mode": "protected-facility-benchmark-minimum",
          "runtimeInputRequirement": {
            "key": "protected-facility-administrative-expense-yen",
            "valueKind": "entered-yen",
            "valueUnit": "yen-per-person-per-month",
            "scope": "facility-and-service-fiscal-year",
            "asOfPolicy": "service-fiscal-year-april-first",
            "provenancePolicyId": "claim.input.protected-facility-administrative-expense.v1"
          },
          "statutoryFormula": {
            "daysDivisor": 22,
            "expenseAdjustmentDivisor": "0.945",
            "unitPriceDivisorYen": 10,
            "fixedAdditionUnits": 23,
            "upliftRate": "1.046",
            "calculationStepId": "claim.step.units.service-code.protected-facility-formula.v1",
            "roundingRuleId": "claim.rounding.units.half-up.v1"
          },
          "benchmark": {
            "officialSection": "b-type-service-fee-ii",
            "basicRewardStaffingKey": "b-type-service-fee-ii",
            "paymentBandMatch": "same-average-wage-band",
            "capacityMatch": "same-capacity-band",
            "localGovernmentAdjustment": {
              "conditionSelector": "municipality-ownership:local-government",
              "rate": "0.965",
              "target": "comparison-only",
              "calculationStepId": "claim.step.units.service-code.protected-facility-local-government-benchmark.v1",
              "roundingRuleId": "claim.rounding.units.half-up.v1"
            }
          },
          "selection": {
            "kind": "minimum",
            "calculationStepId": "claim.step.units.service-code.protected-facility-minimum.v1",
            "roundingRuleId": null
          },
          "factors": {{factorsJson}},
          "billingUnit": "per-day"
        }
        """;

    private static JsonObject[] ProtectedFacilitySourceRefs(
        string serviceCodeLocator,
        bool hasFactors,
        bool isR8 = false)
    {
        var serviceCodeDocumentId = isR8
            ? "r8-service-codes-2-xlsx"
            : "r6-service-codes-2-xlsx";
        var serviceCodeSha256 = isR8
            ? R8ServiceCodesSha256
            : R6ServiceCodesSha256;
        var calculationNoteDocumentId = isR8
            ? "r8-calculation-note"
            : "r6-calculation-note";
        var calculationNoteSha256 = isR8
            ? R8CalculationNoteSha256
            : R6CalculationNoteSha256;
        var serviceSupports = new List<string>
        {
            "service-identity",
            "selectors",
            "unit-rule-kind",
            "conditions",
            "effective-period",
        };
        if (hasFactors)
        {
            serviceSupports.Add("unit-rule-value");
            serviceSupports.Add("unit-rule-target");
        }

        return
        [
            OfficialSourceRef(
                serviceCodeDocumentId,
                serviceCodeSha256,
                serviceCodeLocator,
                serviceSupports.ToArray()),
            OfficialSourceRef(
                "current-fee-notice-html",
                CurrentFeeNoticeHtmlSha256,
                "html:lines=l000002791,l000002793",
                "unit-rule-formula",
                "unit-rule-comparison",
                "unit-rule-local-government-adjustment",
                "unit-rule-runtime-input"),
            OfficialSourceRef(
                "protected-facility-administrative-expense-standard-html",
                ProtectedFacilityAdministrativeExpenseStandardHtmlSha256,
                "html:lines=l000000054,l000000060-l000000062",
                "unit-rule-runtime-input-provenance"),
            OfficialSourceRef(
                calculationNoteDocumentId,
                calculationNoteSha256,
                "pages=8-9",
                "unit-rule-step",
                "unit-rule-rounding"),
            CrossCheckSourceRef(
                "h31-fee-notice-consolidated",
                H31FeeNoticeConsolidatedSha256,
                "pdf:physical-page=46",
                "unit-rule-formula",
                "unit-rule-comparison"),
            CrossCheckSourceRef(
                "h31-fee-notice-consolidated",
                H31FeeNoticeConsolidatedSha256,
                "pdf:physical-page=47",
                "unit-rule-local-government-adjustment"),
        ];
    }

    private static void AssertProtectedFacilityContract(
        ProtectedFacilityBenchmarkMinimumRule rule)
    {
        rule.RuntimeInputRequirement.Should().Be(
            new ProtectedFacilityAdministrativeExpenseRequirement(
                "protected-facility-administrative-expense-yen",
                "entered-yen",
                "yen-per-person-per-month",
                "facility-and-service-fiscal-year",
                "service-fiscal-year-april-first",
                "claim.input.protected-facility-administrative-expense.v1"));
        rule.StatutoryFormula.Should().Be(new ProtectedFacilityStatutoryFormula(
            22,
            0.945m,
            10,
            23,
            1.046m,
            "claim.step.units.service-code.protected-facility-formula.v1",
            "claim.rounding.units.half-up.v1"));
        rule.Benchmark.Should().Be(new ProtectedFacilityBenchmark(
            "b-type-service-fee-ii",
            "b-type-service-fee-ii",
            "same-average-wage-band",
            "same-capacity-band",
            new ProtectedFacilityLocalGovernmentAdjustment(
                "municipality-ownership:local-government",
                0.965m,
                "comparison-only",
                "claim.step.units.service-code.protected-facility-local-government-benchmark.v1",
                "claim.rounding.units.half-up.v1")));
        rule.Selection.Should().Be(new ProtectedFacilityMinimumSelection(
            "minimum",
            "claim.step.units.service-code.protected-facility-minimum.v1",
            null));
        rule.BillingUnit.Should().Be(BillingUnit.PerDay);
    }

    private static Dictionary<string, string> RepresentativeMasters()
    {
        var masters = ValidMasters();
        MutateEntryByKey(masters, "basic-rewards.json", "basic-1", entry =>
        {
            entry["effectiveTo"] = "2026-05";
            entry["sourceRefs"] = new JsonArray(
                R6ServiceCodeSourceRef(
                    "workbook-order=38;row=7",
                    "master-values",
                    "effective-period"));
        });
        MutateEntryByKey(masters, "additions.json", "add-per-count", entry =>
        {
            entry["effectiveTo"] = "2026-05";
            entry["sourceRefs"] = new JsonArray(
                R6ServiceCodeSourceRef(
                    "workbook-order=38;row=941",
                    "master-values",
                    "effective-period"));
        });
        MutateEntryByKey(masters, "additions.json", "add-percentage", entry =>
            entry["effectiveTo"] = "2026-05");

        var root = MasterRoot(masters, "service-codes.json");
        var fixedEntry = EntryByKey(root, "service-fixed");
        fixedEntry["effectiveTo"] = "2026-05";
        fixedEntry["sourceRefs"] = new JsonArray(
            R6ServiceCodeSourceRef(
                "workbook-order=38;row=7",
                "service-identity",
                "selectors",
                "unit-rule-kind",
                "unit-rule-value",
                "conditions",
                "effective-period"));
        fixedEntry["values"]!["conditionSelectors"] = Strings(
            "reward-system-i",
            "capacity-up-to-20",
            "average-wage-45000-or-more");

        var unitEntry = EntryByKey(root, "service-unit");
        unitEntry["effectiveTo"] = "2026-05";
        unitEntry["sourceRefs"] = new JsonArray(
            R6ServiceCodeSourceRef(
                "workbook-order=38;row=941",
                "service-identity",
                "selectors",
                "unit-rule-kind",
                "unit-rule-value",
                "unit-rule-target",
                "unit-rule-step",
                "conditions",
                "effective-period"));
        unitEntry["values"]!["conditionSelectors"] = Strings(
            "capacity-up-to-20",
            "payment-band-1",
            "employment-outcome-at-least-1");

        var factorEntry = EntryByKey(root, "service-factor");
        factorEntry["effectiveTo"] = "2026-05";
        factorEntry["sourceRefs"] = new JsonArray(
            ProtectedFacilitySourceRefs(
                "workbook-order=38;row=908",
                hasFactors: true));
        factorEntry["values"] = JsonNode.Parse(ProtectedFacilityServiceValues(
            "462842",
            "就継Ｂ基準該当・未計画１",
            "selector:factor",
            "[\"plan-not-created\"]",
            """
            [
              {
                "order": 1,
                "rate": "0.7",
                "conditionSelectors": ["plan-not-created"],
                "calculationStepId": "claim.step.units.per-service-code.percentage.v1",
                "roundingRuleId": "claim.rounding.units.half-up.v1"
              }
            ]
            """));

        var capacity = ConditionByKey(root, "capacity-up-to-20");
        capacity["effectiveTo"] = "2026-05";
        capacity["sourceRefs"] = new JsonArray(
            R6ServiceCodeSourceRef(
                "workbook-order=38;row=7",
                "conditions",
                "effective-period"),
            R6ServiceCodeSourceRef(
                "workbook-order=38;row=941",
                "conditions",
                "effective-period"));
        var planNotCreated = ConditionByKey(root, "plan-not-created");
        planNotCreated["effectiveTo"] = "2026-05";
        planNotCreated["sourceRefs"] = new JsonArray(
            R6ServiceCodeSourceRef(
                "workbook-order=38;row=908",
                "conditions",
                "effective-period"));
        root["conditionDefinitions"]!.AsArray().Remove(
            ConditionByKey(root, "first-two-months"));
        root["conditionDefinitions"]!.AsArray().Add(
            R6ServiceCodeCondition(
                "municipality-ownership:local-government",
                "municipality-ownership",
                "equals",
                true,
                "workbook-order=38;row=907"));

        root["conditionDefinitions"]!.AsArray().Add(
            R6ServiceCodeCondition(
                "reward-system-i",
                "reward-system",
                "equals",
                "reward-system-i",
                "workbook-order=38;row=7"));
        root["conditionDefinitions"]!.AsArray().Add(
            R6ServiceCodeCondition(
                "average-wage-45000-or-more",
                "average-wage-band",
                "equals",
                "45000-or-more",
                "workbook-order=38;row=7"));
        root["conditionDefinitions"]!.AsArray().Add(
            R6ServiceCodeCondition(
                "payment-band-1",
                "payment-band",
                "equals",
                "band-1",
                "workbook-order=38;row=941"));
        root["conditionDefinitions"]!.AsArray().Add(
            R6ServiceCodeCondition(
                "employment-outcome-at-least-1",
                "employment-outcome-count",
                "greater-than-or-equal",
                1,
                "workbook-order=38;row=941"));
        SaveRoot(masters, "service-codes.json", root);
        return masters;
    }

    internal static Dictionary<string, string> ValidMasters() =>
        new(StringComparer.Ordinal)
        {
            ["basic-rewards.json"] = MasterJson(
                "basic-rewards",
                Entry(
                    "basic-1",
                    """
                    {
                      "paymentBand": "band-1",
                      "staffingKey": "staffing-1",
                      "capacityKey": "capacity-1",
                      "serviceCode": "462980",
                      "baseUnits": 837
                    }
                    """,
                    AllSupports)),
            ["additions.json"] = MasterJson(
                "additions",
                Entry(
                    "add-per-count",
                    """
                    {
                      "amount": {
                        "kind": "units-per-count",
                        "unitsPerCount": 93,
                        "countSelector": "previous-year-six-month-employment-count"
                      },
                      "calculationStepId": "claim.step.units.service-code.multiply-count.v1",
                      "roundingRuleId": null,
                      "billingUnit": "per-day"
                    }
                    """,
                    AllSupports),
                Entry(
                    "add-fixed",
                    """
                    {
                      "amount": { "kind": "fixed-units", "addedUnits": 12 },
                      "calculationStepId": "claim.step.units.service-code.fixed.v1",
                      "roundingRuleId": null,
                      "billingUnit": "per-day"
                    }
                    """,
                    AllSupports),
                Entry(
                    "add-percentage",
                    """
                    {
                      "amount": {
                        "kind": "percentage-of-target",
                        "percentage": "0.10",
                        "applicationKind": "add",
                        "percentageBaseScope": "per-service-code-unit",
                        "targetSelector": "selector:fixed",
                        "calculationOrder": 1
                      },
                      "calculationStepId": "claim.step.units.per-service-code.percentage.v1",
                      "roundingRuleId": "claim.rounding.units.half-up.v1",
                      "billingUnit": "per-day"
                    }
                    """,
                    AllSupports),
                Entry(
                    "add-prorated",
                    """
                    {
                      "amount": {
                        "kind": "prorated-units",
                        "poolUnitsPerStaff": 500,
                        "staffCountSelector": "medical-coordination-v-visiting-nurse-count",
                        "recipientCountSelector": "medical-coordination-v-supported-recipient-count"
                      },
                      "calculationStepId": "claim.step.units.service-code.prorate-by-recipient-count.v1",
                      "roundingRuleId": "claim.rounding.units.half-up.v1",
                      "billingUnit": "per-day"
                    }
                    """,
                    AllSupports)),
            ["region-unit-prices.json"] = MasterJson(
                "region-unit-prices",
                Entry(
                    "region-1",
                    """
                    {
                      "regionKey": "region-1",
                      "serviceKind": "employment-continuation-support-b",
                      "unitPriceYen": "11.20"
                    }
                    """,
                    AllSupports)),
            ["burden-caps.json"] = MasterJson(
                "burden-caps",
                Entry(
                    "burden-1",
                    """{ "burdenCategory": "general-2", "capYen": 37200 }""",
                    AllSupports)),
            ["transition-rules.json"] = MasterJson(
                "transition-rules",
                Entry(
                    "office-profile-policy",
                    """
                    {
                      "masterVersion": "claim-master-test",
                      "allowedAverageWageBandOptions": [
                        { "kind": "numeric", "officialOptionCode": 1 }
                      ],
                      "allowedOptionsByR8ReformStatus": {
                        "reform-target": [
                          { "kind": "numeric", "officialOptionCode": 1 }
                        ]
                      },
                      "r8EffectiveDate": "2026-06-01",
                      "filedTransitionEndRule": "add-years-exclusive",
                      "filedTransitionDurationYears": 1
                    }
                    """,
                    AllSupports,
                    effectiveFrom: "2026-06")),
            ["service-codes.json"] = ServiceMasterJson(
                [
                    Condition(
                        "capacity-up-to-20",
                        "capacity",
                        "less-than-or-equal",
                        20),
                    Condition(
                        "plan-not-created",
                        "plan-status",
                        "equals",
                        "not-created"),
                    Condition(
                        "first-two-months",
                        "shortage-duration",
                        "less-than-or-equal",
                        2),
                ],
                Entry(
                    "service-fixed",
                    """
                    {
                      "serviceCode": "462980",
                      "officialLabel": "就継ＢⅠ１１",
                      "serviceKind": "employment-continuation-support-b",
                      "selectors": ["selector:fixed"],
                      "conditionSelectors": [],
                      "unitRule": {
                        "kind": "fixed-composite-unit",
                        "finalUnits": 837,
                        "billingUnit": "per-day"
                      },
                      "componentRefs": [
                        { "masterKind": "basic-rewards", "key": "basic-1", "role": "base" }
                      ]
                    }
                    """,
                    AllSupports),
                Entry(
                    "service-unit",
                    """
                    {
                      "serviceCode": "465240",
                      "officialLabel": "就継Ｂ就労移行支援体制加算Ⅰ１１",
                      "serviceKind": "employment-continuation-support-b",
                      "selectors": ["selector:unit"],
                      "conditionSelectors": ["capacity-up-to-20"],
                      "unitRule": {
                        "kind": "unit-addition",
                        "adjustmentComponentKey": "add-per-count",
                        "amount": {
                          "kind": "units-per-count",
                          "unitsPerCount": 93,
                          "countSelector": "previous-year-six-month-employment-count"
                        },
                        "calculationStepId": "claim.step.units.service-code.multiply-count.v1",
                        "roundingRuleId": null,
                        "billingUnit": "per-day"
                      },
                      "componentRefs": [
                        { "masterKind": "additions", "key": "add-per-count", "role": "adjustment" }
                      ]
                    }
                    """,
                    AllSupports),
                Entry(
                    "service-factor",
                    """
                    {
                      "serviceCode": "462842",
                      "officialLabel": "就継Ｂ基準該当・未計画１",
                      "serviceKind": "employment-continuation-support-b",
                      "selectors": ["selector:factor"],
                      "conditionSelectors": ["plan-not-created", "first-two-months"],
                      "unitRule": {
                        "kind": "formula",
                        "mode": "factor-chain",
                        "baseComponentKey": "basic-1",
                        "factors": [
                          {
                            "order": 1,
                            "rate": "0.7",
                            "conditionSelectors": ["plan-not-created", "first-two-months"],
                            "calculationStepId": "claim.step.units.per-service-code.percentage.v1",
                            "roundingRuleId": "claim.rounding.units.half-up.v1"
                          }
                        ],
                        "billingUnit": "per-day"
                      },
                      "componentRefs": [
                        { "masterKind": "basic-rewards", "key": "basic-1", "role": "base" }
                      ]
                    }
                    """,
                    AllSupports)),
        };

    internal static void MutateFirstEntry(
        Dictionary<string, string> masters,
        string fileName,
        Action<JsonObject> mutate) =>
        MutateEntryByKey(
            masters,
            fileName,
            EntryByKey(MasterRoot(masters, fileName), null)["key"]!.GetValue<string>(),
            mutate);

    internal static void MutateFirstValues(
        Dictionary<string, string> masters,
        string fileName,
        Action<JsonObject> mutate) =>
        MutateFirstEntry(masters, fileName, entry => mutate(entry["values"]!.AsObject()));

    private static void Load(IReadOnlyDictionary<string, string> masterJsons) =>
        _ = CreateProvider(masterJsons);

    private static ClaimCalculationMasterBundle LoadBundle(
        IReadOnlyDictionary<string, string> masterJsons)
        => LoadBundle(masterJsons, ValidCatalogJson);

    private static ClaimCalculationMasterBundle LoadBundle(
        IReadOnlyDictionary<string, string> masterJsons,
        string catalogJson)
    {
        var provider = CreateProvider(masterJsons, catalogJson);
        return (ClaimCalculationMasterBundle)typeof(JsonClaimMasterProvider)
            .GetField("_calculationMasters", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(provider)!;
    }

    private static MemoryStream StreamOf(string json) => new(Encoding.UTF8.GetBytes(json));

    private static JsonObject MasterRoot(
        Dictionary<string, string> masters,
        string fileName) => JsonNode.Parse(masters[fileName])!.AsObject();

    private static void SaveRoot(
        Dictionary<string, string> masters,
        string fileName,
        JsonObject? root = null) =>
        masters[fileName] = (root ?? MasterRoot(masters, fileName)).ToJsonString();

    private static void MutateRoot(
        Dictionary<string, string> masters,
        string fileName,
        Action<JsonObject> mutate)
    {
        var root = MasterRoot(masters, fileName);
        mutate(root);
        SaveRoot(masters, fileName, root);
    }

    private static JsonObject EntryByKey(JsonObject root, string? key) => key is null
        ? root["entries"]![0]!.AsObject()
        : root["entries"]!.AsArray()
            .Select(node => node!.AsObject())
            .Single(entry => entry["key"]!.GetValue<string>() == key);

    private static void MutateEntryByKey(
        Dictionary<string, string> masters,
        string fileName,
        string key,
        Action<JsonObject> mutate)
    {
        var root = MasterRoot(masters, fileName);
        mutate(EntryByKey(root, key));
        SaveRoot(masters, fileName, root);
    }

    private static void MutateServiceEntry(
        Dictionary<string, string> masters,
        string key,
        Action<JsonObject> mutate) =>
        MutateEntryByKey(masters, "service-codes.json", key, mutate);

    private static void MutateService(
        Dictionary<string, string> masters,
        string key,
        Action<JsonObject> mutate) =>
        MutateServiceEntry(masters, key, entry => mutate(entry["values"]!.AsObject()));

    private static void MutateService(
        JsonObject root,
        string key,
        Action<JsonObject> mutate) =>
        mutate(EntryByKey(root, key)["values"]!.AsObject());

    private static void SetJsonPath(JsonObject root, string path, string json)
    {
        var segments = path.Split('.');
        var parent = root;
        foreach (var segment in segments[..^1])
            parent = parent[segment]!.AsObject();
        parent[segments[^1]] = JsonNode.Parse(json);
    }

    private static void RemoveSourceSupport(
        Dictionary<string, string> masters,
        string serviceKey,
        string support) =>
        MutateServiceEntry(masters, serviceKey, entry =>
        {
            var sourceRefs = entry["sourceRefs"]!.AsArray();
            var source = sourceRefs
                .Select(node => node!.AsObject())
                .First(sourceRef => sourceRef["supports"]!.AsArray()
                    .Any(node => node!.GetValue<string>() == support));
            var supports = source["supports"]!.AsArray();
            supports.Remove(supports.Single(node => node!.GetValue<string>() == support));
            if (supports.Count == 0)
                sourceRefs.Remove(source);
        });

    private static void MoveSourceSupport(
        Dictionary<string, string> masters,
        string serviceKey,
        string support,
        string expectedDocumentId,
        string substitutedDocumentId) =>
        MutateServiceEntry(masters, serviceKey, entry =>
        {
            var sourceRefs = entry["sourceRefs"]!.AsArray();
            var expected = SourceRefByDocument(entry, expectedDocumentId);
            var expectedSupports = expected["supports"]!.AsArray();
            expectedSupports.Remove(expectedSupports.Single(node =>
                node!.GetValue<string>() == support));
            if (expectedSupports.Count == 0)
                sourceRefs.Remove(expected);

            var substituted = SourceRefByDocument(entry, substitutedDocumentId);
            substituted["supports"]!.AsArray().Add(support);
        });

    private static JsonObject SourceRefByDocument(
        JsonObject entry,
        string documentId) =>
        entry["sourceRefs"]!.AsArray()
            .Select(node => node!.AsObject())
            .First(sourceRef =>
                sourceRef["documentId"]!.GetValue<string>() == documentId);

    private static string SourceSha256(string documentId) => documentId switch
    {
        "r6-service-codes-2-xlsx" => R6ServiceCodesSha256,
        "r6-calculation-note" => R6CalculationNoteSha256,
        "r8-service-codes-2-xlsx" => R8ServiceCodesSha256,
        "r8-calculation-note" => R8CalculationNoteSha256,
        _ => throw new ArgumentOutOfRangeException(nameof(documentId), documentId, null),
    };

    private static JsonObject ConditionByKey(JsonObject root, string key) =>
        root["conditionDefinitions"]!.AsArray()
            .Select(node => node!.AsObject())
            .Single(condition => condition["key"]!.GetValue<string>() == key);

    private static void MutateCondition(
        Dictionary<string, string> masters,
        string key,
        Action<JsonObject> mutate)
    {
        var root = MasterRoot(masters, "service-codes.json");
        mutate(ConditionByKey(root, key));
        SaveRoot(masters, "service-codes.json", root);
    }

    private static void MutateCondition(
        Dictionary<string, string> masters,
        string key,
        string effectiveFrom,
        Action<JsonObject> mutate)
    {
        var root = MasterRoot(masters, "service-codes.json");
        var condition = root["conditionDefinitions"]!.AsArray()
            .Select(node => node!.AsObject())
            .Single(candidate =>
                candidate["key"]!.GetValue<string>() == key
                && candidate["effectiveFrom"]!.GetValue<string>() == effectiveFrom);
        mutate(condition);
        SaveRoot(masters, "service-codes.json", root);
    }

    private static void AddService(Dictionary<string, string> masters, string entry)
    {
        var root = MasterRoot(masters, "service-codes.json");
        root["entries"]!.AsArray().Add(JsonNode.Parse(entry));
        SaveRoot(masters, "service-codes.json", root);
    }

    private static void SetAllSourceRefs(
        Dictionary<string, string> masters,
        params JsonObject[] sourceRefs)
    {
        foreach (var fileName in masters.Keys.ToArray())
        {
            var root = MasterRoot(masters, fileName);
            foreach (var entry in root["entries"]!.AsArray())
            {
                entry!["sourceRefs"] = new JsonArray(
                    sourceRefs.Select(source => source.DeepClone()).ToArray());
            }

            if (root.TryGetPropertyValue("conditionDefinitions", out var conditions)
                && conditions is not null)
            {
                foreach (var condition in conditions.AsArray())
                {
                    condition!["sourceRefs"] = new JsonArray(
                        sourceRefs.Select(source => source.DeepClone()).ToArray());
                }
            }

            SaveRoot(masters, fileName, root);
        }
    }

    private static JsonObject SourceRef(
        string documentId,
        string evidenceRole,
        params string[] supports) => new()
        {
            ["documentId"] = documentId,
            ["sha256"] = Sha256,
            ["locator"] = $"source:{documentId}",
            ["evidenceRole"] = evidenceRole,
            ["supports"] = Strings(supports),
        };

    private static string Entry(
        string key,
        string values,
        string[] supports,
        string effectiveFrom = "2024-04",
        string? effectiveTo = null)
    {
        var entry = new JsonObject
        {
            ["key"] = key,
            ["effectiveFrom"] = effectiveFrom,
            ["effectiveTo"] = effectiveTo,
            ["sourceRefs"] = new JsonArray(
                SourceRef("doc-1", "authoritative", supports)),
            ["values"] = JsonNode.Parse(values),
        };
        return entry.ToJsonString();
    }

    private static string MasterJson(string kind, params string[] entries)
    {
        var root = new JsonObject
        {
            ["schemaVersion"] = "2",
            ["masterKind"] = kind,
            ["entries"] = new JsonArray(
                entries.Select(entry => JsonNode.Parse(entry)).ToArray()),
        };
        return root.ToJsonString();
    }

    private static string ServiceMasterJson(
        JsonObject[] conditions,
        params string[] entries)
    {
        var root = JsonNode.Parse(MasterJson("service-codes", entries))!.AsObject();
        root["conditionDefinitions"] = new JsonArray(conditions);
        return root.ToJsonString();
    }

    private static JsonObject Condition(
        string key,
        string kind,
        string @operator,
        object value,
        string effectiveFrom = "2024-04",
        string? effectiveTo = null) => new()
        {
            ["key"] = key,
            ["effectiveFrom"] = effectiveFrom,
            ["effectiveTo"] = effectiveTo,
            ["kind"] = kind,
            ["operator"] = @operator,
            ["value"] = JsonValue.Create(value),
            ["sourceRefs"] = new JsonArray(
                SourceRef("doc-1", "authoritative", AllSupports)),
        };

    private static JsonObject R6ServiceCodeCondition(
        string key,
        string kind,
        string @operator,
        object value,
        string locator)
    {
        var condition = Condition(
            key,
            kind,
            @operator,
            value,
            effectiveTo: "2026-05");
        condition["sourceRefs"] = new JsonArray(
            R6ServiceCodeSourceRef(locator, "conditions", "effective-period"));
        return condition;
    }

    private static string EntryWithSources(
        string key,
        string values,
        string effectiveFrom,
        string? effectiveTo,
        params JsonObject[] sourceRefs)
    {
        var entry = JsonNode.Parse(Entry(
            key,
            values,
            AllSupports,
            effectiveFrom,
            effectiveTo))!.AsObject();
        entry["sourceRefs"] = new JsonArray(sourceRefs);
        return entry.ToJsonString();
    }

    private static JsonObject R6ServiceCodeSourceRef(
        string locator,
        params string[] supports) => OfficialSourceRef(
            "r6-service-codes-2-xlsx",
            R6ServiceCodesSha256,
            locator,
            supports);

    private static JsonObject R6FeeNoticeSourceRef(
        string locator,
        params string[] supports) => OfficialSourceRef(
            "r6-fee-notice",
            R6FeeNoticeSha256,
            locator,
            supports);

    private static JsonObject OfficialSourceRef(
        string documentId,
        string sha256,
        string locator,
        params string[] supports) => new()
        {
            ["documentId"] = documentId,
            ["sha256"] = sha256,
            ["locator"] = locator,
            ["evidenceRole"] = "authoritative",
            ["supports"] = Strings(supports),
        };

    private static JsonObject CrossCheckSourceRef(
        string documentId,
        string sha256,
        string locator,
        params string[] supports)
    {
        var sourceRef = OfficialSourceRef(documentId, sha256, locator, supports);
        sourceRef["evidenceRole"] = "cross-check";
        return sourceRef;
    }

    private static void AssertR6ServiceCodeSource(
        ServiceCodeMasterRow row,
        string locator)
    {
        row.EffectiveFrom.ToString().Should().Be("2024-04");
        row.EffectiveTo?.ToString().Should().Be("2026-05");
        row.SourceRefs.Should().Contain(source =>
            source.DocumentId == "r6-service-codes-2-xlsx"
            && source.Sha256 == R6ServiceCodesSha256
            && source.Locator == locator
            && source.EvidenceRole == ClaimSourceEvidenceRole.Authoritative);
    }

    private static JsonArray Strings(params string[] values)
    {
        var result = new JsonArray();
        foreach (var value in values)
            result.Add(value);
        return result;
    }

    private static JsonObject Source(string documentId, params string[] corrects) => new()
    {
        ["documentId"] = documentId,
        ["title"] = $"Source {documentId}",
        ["publisher"] = "Ministry",
        ["effectiveAt"] = "2024-04-01",
        ["publishedAt"] = null,
        ["retrievedAt"] = "2026-07-10",
        ["url"] = $"https://example.test/{documentId}.pdf",
        ["sha256"] = Sha256,
        ["supersedes"] = null,
        ["corrects"] = corrects.Length == 0 ? null : Strings(corrects),
        ["supplements"] = null,
        ["applicabilityNote"] = null,
        ["correctionNote"] = corrects.Length == 0 ? null : "Correction relation.",
    };

    private static JsonObject OfficialSource(string documentId, string sha256)
    {
        var source = Source(documentId);
        source["sha256"] = sha256;
        return source;
    }

    private static string CatalogJson(params JsonObject[] sources)
    {
        var sourceIds = Strings(sources.Select(source =>
            source["documentId"]!.GetValue<string>()).ToArray());
        return new JsonObject
        {
            ["schemaVersion"] = "1",
            ["sources"] = new JsonArray(sources),
            ["releases"] = new JsonArray(
                new JsonObject
                {
                    ["masterVersion"] = "claim-master-test",
                    ["effectiveFrom"] = "2024-04",
                    ["effectiveTo"] = null,
                    ["sourceDocumentIds"] = sourceIds,
                }),
        }.ToJsonString();
    }

    private static readonly string ValidCatalogJson = CatalogJson(Source("doc-1"));
    private static readonly string RepresentativeCatalogJson = CatalogJson(
        Source("doc-1"),
        OfficialSource("r6-service-codes-2-xlsx", R6ServiceCodesSha256),
        OfficialSource("r8-service-codes-2-xlsx", R8ServiceCodesSha256),
        OfficialSource("r6-fee-notice", R6FeeNoticeSha256),
        OfficialSource("r6-calculation-note", R6CalculationNoteSha256),
        OfficialSource("r8-calculation-note", R8CalculationNoteSha256),
        OfficialSource("current-fee-notice-html", CurrentFeeNoticeHtmlSha256),
        OfficialSource(
            "protected-facility-administrative-expense-standard-html",
            ProtectedFacilityAdministrativeExpenseStandardHtmlSha256),
        OfficialSource("h31-fee-notice-consolidated", H31FeeNoticeConsolidatedSha256));
}
