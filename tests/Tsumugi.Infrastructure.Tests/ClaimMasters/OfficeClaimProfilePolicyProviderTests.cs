using System.Text.Json.Nodes;
using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.ClaimMasters;

namespace Tsumugi.Infrastructure.Tests.ClaimMasters;

public sealed class OfficeClaimProfilePolicyProviderTests
{
    private static readonly ClaimMasterVersion MasterVersion = new("claim-master-test");

    [Fact]
    public void Embedded_empty_transition_seed_fails_closed_with_a_sanitized_code()
    {
        var provider = JsonClaimMasterProvider.LoadEmbedded();

        var action = () => provider.Resolve(MasterVersion);

        var error = action.Should().Throw<ClaimMasterPolicyUnavailableException>().Which;
        error.Code.Should().Be(ClaimMasterPolicyUnavailableCode.Unavailable);
        error.Message.Should().NotContain(MasterVersion.Value);
    }

    [Fact]
    public void Resolve_returns_the_exact_version_policy()
    {
        var provider =
            ClaimMasterSchemaPhase31Tests.CreateProvider(
                ClaimMasterSchemaPhase31Tests.ValidMasters());

        var policy = provider.Resolve(MasterVersion);

        policy.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_rejects_multiple_rows_for_the_same_version()
    {
        var masters = ClaimMasterSchemaPhase31Tests.ValidMasters();
        var root = JsonNode.Parse(masters["transition-rules.json"])!.AsObject();
        var duplicate = root["entries"]![0]!.DeepClone().AsObject();
        duplicate["key"] = "office-profile-policy-duplicate";
        root["entries"]!.AsArray().Add(duplicate);
        masters["transition-rules.json"] = root.ToJsonString();
        var provider =
            ClaimMasterSchemaPhase31Tests.CreateProvider(masters);

        var action = () => provider.Resolve(MasterVersion);

        var error = action.Should().Throw<ClaimMasterPolicyUnavailableException>().Which;
        error.Code.Should().Be(ClaimMasterPolicyUnavailableCode.Ambiguous);
        error.Message.Should().NotContain("office-profile-policy-duplicate");
    }

    [Fact]
    public void Load_rejects_an_invalid_transition_row_with_a_sanitized_code()
    {
        var masters = ClaimMasterSchemaPhase31Tests.ValidMasters();
        ClaimMasterSchemaPhase31Tests.MutateFirstEntry(
            masters,
            "transition-rules.json",
            entry => entry["values"]!["filedTransitionDurationYears"] = 0);

        var action = () => ClaimMasterSchemaPhase31Tests.CreateProvider(masters);

        var error = action.Should().Throw<ClaimMasterPolicyUnavailableException>().Which;
        error.Code.Should().Be(ClaimMasterPolicyUnavailableCode.InvalidMaster);
        error.Message.Should().NotContain("filedTransitionDurationYears");
    }

    [Fact]
    public void Load_sanitizes_an_invalid_transition_row_envelope()
    {
        var masters = ClaimMasterSchemaPhase31Tests.ValidMasters();
        ClaimMasterSchemaPhase31Tests.MutateFirstEntry(
            masters,
            "transition-rules.json",
            entry => entry.Remove("sourceLocator"));

        var action = () => ClaimMasterSchemaPhase31Tests.CreateProvider(masters);

        var error = action.Should().Throw<ClaimMasterPolicyUnavailableException>().Which;
        error.Code.Should().Be(ClaimMasterPolicyUnavailableCode.InvalidMaster);
        error.Message.Should().NotContain("sourceLocator");
    }

    [Fact]
    public void Resolve_current_policy_uses_historical_version_rules_for_reform_exempt_comparison()
    {
        var masters = ClaimMasterSchemaPhase31Tests.ValidMasters();
        var root = JsonNode.Parse(masters["transition-rules.json"])!.AsObject();
        var current = root["entries"]![0]!.AsObject();
        current["values"]!["allowedOptionsByR8ReformStatus"]!["reform-exempt"] =
            OptionArray(1);
        var r604 = HistoricalRule(current, "claim-master-r6-04", "2024-04", "2024-05", 2);
        var r606 = HistoricalRule(current, "claim-master-r6-06", "2024-06", "2026-05", 3);
        root["entries"]!.AsArray().Add(r604);
        root["entries"]!.AsArray().Add(r606);
        masters["transition-rules.json"] = root.ToJsonString();
        var provider = ClaimMasterSchemaPhase31Tests.CreateProvider(
            masters,
            MultiVersionCatalogJson);
        var policy = provider.Resolve(MasterVersion);
        var rootId = Guid.NewGuid();
        var profile = new OfficeClaimProfile
        {
            Id = rootId,
            OfficeId = Guid.NewGuid(),
            EffectiveFrom = new DateOnly(2026, 6, 1),
            EffectiveTo = null,
            RootId = rootId,
            Revision = 1,
            Kind = RecordKind.New,
            ExpectedHeadId = null,
            MasterVersion = MasterVersion,
            ReformStatus = R8ReformStatus.ReformExempt,
            AverageWageBandOption = new AverageWageBandOption(
                AverageWageBandOptionKind.Numeric, 1),
            EarlierRegisteredBandOption = new VersionedAverageWageBandOption(
                new ClaimMasterVersion("claim-master-r6-04"),
                new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 2)),
            EarlierRegistrationMonth = new ServiceMonth(2024, 4),
            LaterRegisteredBandOption = new VersionedAverageWageBandOption(
                new ClaimMasterVersion("claim-master-r6-06"),
                new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 3)),
            LaterRegistrationMonth = new ServiceMonth(2024, 6),
            ReformComparisonEvidenceDocumentId = "comparison-evidence",
            EvidenceDocumentId = "designation-ledger",
            ConfirmedAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            ConfirmedBy = "reviewer",
            ConfirmationReason = "official registrations compared",
            CreatedAt = new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = "operator",
            ConcurrencyToken = Guid.NewGuid(),
        };

        var action = () => policy.ValidateHistory([profile]);

        action.Should().NotThrow();
    }

    private static JsonObject HistoricalRule(
        JsonObject current,
        string masterVersion,
        string effectiveFrom,
        string effectiveTo,
        int optionCode)
    {
        var row = current.DeepClone().AsObject();
        row["effectiveFrom"] = effectiveFrom;
        row["effectiveTo"] = effectiveTo;
        row["values"]!["masterVersion"] = masterVersion;
        row["values"]!["allowedAverageWageBandOptions"] = OptionArray(optionCode);
        row["values"]!["allowedOptionsByR8ReformStatus"] = new JsonObject
        {
            ["not-applicable-before-r8"] = OptionArray(optionCode),
        };
        return row;
    }

    private static JsonArray OptionArray(int optionCode) =>
    [
        new JsonObject
        {
            ["kind"] = "numeric",
            ["officialOptionCode"] = optionCode,
        },
    ];

    private const string MultiVersionCatalogJson = """
        {
          "schemaVersion": "1",
          "sources": [
            {
              "documentId": "doc-1",
              "title": "Synthetic source",
              "publisher": "Test publisher",
              "effectiveAt": "2024-04-01",
              "publishedAt": "2024-03-01",
              "retrievedAt": "2026-06-02",
              "url": "https://example.test/doc-1.pdf",
              "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
              "supersedes": null,
              "corrects": null,
              "supplements": null,
              "applicabilityNote": null,
              "correctionNote": null
            }
          ],
          "releases": [
            {
              "masterVersion": "claim-master-r6-04",
              "effectiveFrom": "2024-04",
              "effectiveTo": "2024-05",
              "sourceDocumentIds": ["doc-1"]
            },
            {
              "masterVersion": "claim-master-r6-06",
              "effectiveFrom": "2024-06",
              "effectiveTo": "2026-05",
              "sourceDocumentIds": ["doc-1"]
            },
            {
              "masterVersion": "claim-master-test",
              "effectiveFrom": "2026-06",
              "effectiveTo": null,
              "sourceDocumentIds": ["doc-1"]
            }
          ]
        }
        """;
}
