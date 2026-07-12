using System.Text.Json.Nodes;
using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Logic.Claim.Models;
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
}
