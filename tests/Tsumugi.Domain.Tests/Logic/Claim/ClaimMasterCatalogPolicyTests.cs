using FluentAssertions;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Tests.Logic.Claim;

public sealed class ClaimMasterCatalogPolicyTests
{
    private const string ValidSha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Version_value_objects_reject_blank_values(string? value)
    {
        FluentActions.Invoking(() => new ClaimMasterVersion(value!))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new CsvSpecificationVersion(value!))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Version_value_objects_reject_values_longer_than_persistence_limit()
    {
        var value = new string('v', 65);

        FluentActions.Invoking(() => new ClaimMasterVersion(value))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new CsvSpecificationVersion(value))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Version_value_objects_preserve_stable_value_and_text()
    {
        var master = new ClaimMasterVersion("claim-master-r8-06");
        var csv = new CsvSpecificationVersion("provider-claim-r7-10");

        master.Value.Should().Be("claim-master-r8-06");
        master.ToString().Should().Be(master.Value);
        csv.Value.Should().Be("provider-claim-r7-10");
        csv.ToString().Should().Be(csv.Value);
    }

    [Theory]
    [InlineData(" claim-master-r8-06")]
    [InlineData("claim-master-r8-06 ")]
    public void Version_value_objects_reject_outer_whitespace(string value)
    {
        FluentActions.Invoking(() => new ClaimMasterVersion(value))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new CsvSpecificationVersion(value))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Version_default_values_fail_closed_for_value_equality_and_text()
    {
        var invalidMaster = default(ClaimMasterVersion);
        var validMaster = new ClaimMasterVersion("claim-master-r8-06");
        var invalidCsv = default(CsvSpecificationVersion);
        var validCsv = new CsvSpecificationVersion("provider-claim-r7-10");
        Action[] actions =
        [
            () => _ = invalidMaster.Value,
            () => _ = invalidMaster.ToString(),
            () => _ = invalidMaster.Equals(validMaster),
            () => _ = validMaster.Equals(invalidMaster),
            () => _ = invalidMaster.GetHashCode(),
            () => _ = invalidMaster == default,
            () => _ = invalidCsv.Value,
            () => _ = invalidCsv.ToString(),
            () => _ = invalidCsv.Equals(validCsv),
            () => _ = validCsv.Equals(invalidCsv),
            () => _ = invalidCsv.GetHashCode(),
            () => _ = invalidCsv == default,
        ];

        foreach (var action in actions)
            action.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(null, "title", "publisher")]
    [InlineData("", "title", "publisher")]
    [InlineData("document", " ", "publisher")]
    [InlineData("document", "title", "\t")]
    public void Source_document_rejects_blank_required_text(
        string? documentId,
        string title,
        string publisher)
        => FluentActions.Invoking(() => Source(documentId!, title, publisher))
            .Should().Throw<ArgumentException>();

    [Theory]
    [InlineData("http://example.test/source.pdf")]
    [InlineData("/relative/source.pdf")]
    [InlineData("not-a-url")]
    public void Source_document_requires_absolute_https_url(string url)
        => FluentActions.Invoking(() => Source(url: url))
            .Should().Throw<ArgumentException>();

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("g123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    public void Source_document_requires_lowercase_64_hex_sha256(string sha256)
        => FluentActions.Invoking(() => Source(sha256: sha256))
            .Should().Throw<ArgumentException>();

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Source_document_rejects_blank_optional_text(string blank)
    {
        FluentActions.Invoking(() => Source(supersedes: blank))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => Source(notes: blank))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Source_document_rejects_outer_whitespace_without_normalizing()
    {
        Action[] actions =
        [
            () => Source(documentId: " source-a"),
            () => Source(title: " Official source"),
            () => Source(publisher: "MHLW "),
            () => Source(url: " https://example.test/source.pdf"),
            () => Source(supersedes: "source-old "),
            () => Source(notes: " official correction"),
        ];

        foreach (var action in actions)
            action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Source_document_rejects_default_effective_or_retrieved_date()
    {
        FluentActions.Invoking(() => new ClaimSourceDocument(
                "source-a",
                "Official source",
                "MHLW",
                default,
                new DateOnly(2026, 7, 10),
                "https://example.test/source.pdf",
                ValidSha256,
                null,
                null))
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*source-a*");
        FluentActions.Invoking(() => new ClaimSourceDocument(
                "source-a",
                "Official source",
                "MHLW",
                new DateOnly(2026, 6, 1),
                default,
                "https://example.test/source.pdf",
                ValidSha256,
                null,
                null))
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*source-a*");
    }

    [Fact]
    public void Source_document_preserves_dates_and_optional_metadata()
    {
        var source = Source(
            supersedes: "source-old",
            notes: "official correction");

        source.EffectiveAt.Should().Be(new DateOnly(2026, 6, 1));
        source.RetrievedAt.Should().Be(new DateOnly(2026, 7, 10));
        source.Supersedes.Should().Be("source-old");
        source.Notes.Should().Be("official correction");
    }

    [Fact]
    public void Release_rejects_end_before_start_and_mentions_version_and_months()
    {
        var action = () => Release(
            "claim-master-invalid",
            new ServiceMonth(2026, 6),
            new ServiceMonth(2026, 5));

        action.Should().Throw<ArgumentException>()
            .WithMessage("*claim-master-invalid*2026-06*2026-05*");
    }

    [Fact]
    public void Release_rejects_null_blank_and_duplicate_source_ids()
    {
        FluentActions.Invoking(() => new ClaimMasterRelease(
                new ClaimMasterVersion("claim-master-r8-06"),
                new ServiceMonth(2026, 6),
                null,
                null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => Release(sourceIds: []))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => Release(sourceIds: ["source-a", " "]))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => Release(sourceIds: ["source-a", "source-a"]))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Release_rejects_source_id_outer_whitespace()
        => FluentActions.Invoking(() => Release(sourceIds: [" source-a"]))
            .Should().Throw<ArgumentException>();

    [Fact]
    public void Release_deep_freezes_source_ids_against_caller_mutation()
    {
        var sourceIds = new List<string> { "source-a" };
        var release = Release(sourceIds: sourceIds);

        sourceIds[0] = "mutated";
        sourceIds.Add("source-b");

        release.SourceDocumentIds.Should().Equal("source-a");
    }

    [Fact]
    public void Release_record_equality_and_hash_use_source_id_sequence_values()
    {
        var left = Release(sourceIds: new List<string> { "source-a", "source-b" });
        var right = Release(sourceIds: new List<string> { "source-a", "source-b" });
        var reversed = Release(sourceIds: new List<string> { "source-b", "source-a" });

        left.Should().Be(right);
        (left == right).Should().BeTrue();
        left.GetHashCode().Should().Be(right.GetHashCode());
        left.Should().NotBe(reversed);
    }

    [Fact]
    public void Validate_accepts_contiguous_inclusive_catalog_and_Resolve_uses_boundaries()
    {
        var sources = Sources();
        var releases = Releases();

        ClaimMasterCatalogPolicy.Validate(releases, sources);

        ClaimMasterCatalogPolicy.Resolve(releases, sources, new ServiceMonth(2024, 4)).Version.Value
            .Should().Be("claim-master-r6-04");
        ClaimMasterCatalogPolicy.Resolve(releases, sources, new ServiceMonth(2024, 5)).Version.Value
            .Should().Be("claim-master-r6-04");
        ClaimMasterCatalogPolicy.Resolve(releases, sources, new ServiceMonth(2024, 6)).Version.Value
            .Should().Be("claim-master-r6-06");
        ClaimMasterCatalogPolicy.Resolve(releases, sources, new ServiceMonth(2026, 5)).Version.Value
            .Should().Be("claim-master-r6-06");
        ClaimMasterCatalogPolicy.Resolve(releases, sources, new ServiceMonth(2026, 6)).Version.Value
            .Should().Be("claim-master-r8-06");
        ClaimMasterCatalogPolicy.Resolve(releases, sources, new ServiceMonth(2200, 12)).Version.Value
            .Should().Be("claim-master-r8-06");
    }

    [Fact]
    public void Validate_rejects_null_collections_and_null_elements()
    {
        FluentActions.Invoking(() => ClaimMasterCatalogPolicy.Validate(null!, Sources()))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => ClaimMasterCatalogPolicy.Validate(Releases(), null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => ClaimMasterCatalogPolicy.Validate(
                new ClaimMasterRelease[] { null! },
                Sources()))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => ClaimMasterCatalogPolicy.Validate(
                Releases(),
                new ClaimSourceDocument[] { null! }))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Validate_rejects_empty_release_catalog()
        => FluentActions.Invoking(() => ClaimMasterCatalogPolicy.Validate([], Sources()))
            .Should().Throw<ArgumentException>();

    [Fact]
    public void Validate_rejects_duplicate_source_document_id_and_mentions_id()
    {
        var sources = new[] { Source("source-a"), Source("source-a") };

        FluentActions.Invoking(() => ClaimMasterCatalogPolicy.Validate(Releases(), sources))
            .Should().Throw<ArgumentException>()
            .WithMessage("*source-a*");
    }

    [Fact]
    public void Validate_rejects_unknown_superseded_source_and_mentions_both_ids()
    {
        var sources = new[] { Source("source-a", supersedes: "source-missing") };
        var releases = new[] { Release(sourceIds: ["source-a"]) };

        FluentActions.Invoking(() => ClaimMasterCatalogPolicy.Validate(releases, sources))
            .Should().Throw<ArgumentException>()
            .WithMessage("*source-a*source-missing*");
    }

    [Fact]
    public void Validate_rejects_self_supersedes_and_mentions_document_id()
    {
        var sources = new[] { Source("source-a", supersedes: "source-a") };
        var releases = new[] { Release(sourceIds: ["source-a"]) };

        FluentActions.Invoking(() => ClaimMasterCatalogPolicy.Validate(releases, sources))
            .Should().Throw<ArgumentException>()
            .WithMessage("*source-a*");
    }

    [Fact]
    public void Validate_rejects_two_document_supersedes_cycle_and_mentions_ids()
    {
        var sources = new[]
        {
            Source("source-a", supersedes: "source-b"),
            Source("source-b", supersedes: "source-a"),
        };
        var releases = new[] { Release(sourceIds: ["source-a"]) };

        FluentActions.Invoking(() => ClaimMasterCatalogPolicy.Validate(releases, sources))
            .Should().Throw<ArgumentException>()
            .WithMessage("*source-a*source-b*source-a*");
    }

    [Fact]
    public void Validate_rejects_long_supersedes_cycle_and_allows_acyclic_chain()
    {
        var cyclicSources = new[]
        {
            Source("source-a", supersedes: "source-b"),
            Source("source-b", supersedes: "source-c"),
            Source("source-c", supersedes: "source-d"),
            Source("source-d", supersedes: "source-b"),
        };
        var releases = new[] { Release(sourceIds: ["source-a"]) };

        FluentActions.Invoking(() => ClaimMasterCatalogPolicy.Validate(releases, cyclicSources))
            .Should().Throw<ArgumentException>()
            .WithMessage("*source-b*source-c*source-d*source-b*");

        var acyclicSources = new[]
        {
            Source("source-a", supersedes: "source-b"),
            Source("source-b", supersedes: "source-c"),
            Source("source-c"),
        };
        ClaimMasterCatalogPolicy.Validate(releases, acyclicSources);
    }

    [Fact]
    public void Validate_rejects_duplicate_release_version_and_mentions_version()
    {
        var releases = new[]
        {
            Release("same-version", new ServiceMonth(2024, 4), new ServiceMonth(2024, 5)),
            Release("same-version", new ServiceMonth(2024, 6), null),
        };

        FluentActions.Invoking(() => ClaimMasterCatalogPolicy.Validate(releases, Sources()))
            .Should().Throw<ArgumentException>()
            .WithMessage("*same-version*");
    }

    [Fact]
    public void Validate_rejects_duplicate_start_month_and_mentions_versions_and_month()
    {
        var releases = new[]
        {
            Release("version-a", new ServiceMonth(2024, 4), new ServiceMonth(2024, 5)),
            Release("version-b", new ServiceMonth(2024, 4), null),
        };

        FluentActions.Invoking(() => ClaimMasterCatalogPolicy.Validate(releases, Sources()))
            .Should().Throw<ArgumentException>()
            .WithMessage("*version-a*version-b*2024-04*");
    }

    [Fact]
    public void Validate_rejects_overlapping_inclusive_periods_and_mentions_versions_and_month()
    {
        var releases = new[]
        {
            Release("version-a", new ServiceMonth(2024, 4), new ServiceMonth(2024, 6)),
            Release("version-b", new ServiceMonth(2024, 6), null),
        };

        FluentActions.Invoking(() => ClaimMasterCatalogPolicy.Validate(releases, Sources()))
            .Should().Throw<ArgumentException>()
            .WithMessage("*version-a*version-b*2024-06*");
    }

    [Fact]
    public void Validate_rejects_implicit_gap_and_mentions_versions_and_month()
    {
        var releases = new[]
        {
            Release("version-a", new ServiceMonth(2024, 4), new ServiceMonth(2024, 5)),
            Release("version-b", new ServiceMonth(2024, 7), null),
        };

        FluentActions.Invoking(() => ClaimMasterCatalogPolicy.Validate(releases, Sources()))
            .Should().Throw<ArgumentException>()
            .WithMessage("*version-a*version-b*2024-06*");
    }

    [Fact]
    public void Validate_rejects_open_ended_release_before_last_release()
    {
        var releases = new[]
        {
            Release("version-a", new ServiceMonth(2024, 4), null),
            Release("version-b", new ServiceMonth(2024, 6), null),
        };

        FluentActions.Invoking(() => ClaimMasterCatalogPolicy.Validate(releases, Sources()))
            .Should().Throw<ArgumentException>()
            .WithMessage("*version-a*version-b*2024-06*");
    }

    [Fact]
    public void Validate_rejects_single_finite_last_release()
    {
        var releases = new[]
        {
            Release("version-a", new ServiceMonth(2024, 4), new ServiceMonth(2024, 5)),
        };

        FluentActions.Invoking(() => ClaimMasterCatalogPolicy.Validate(releases, Sources()))
            .Should().Throw<ArgumentException>()
            .WithMessage("*version-a*2024-05*");
    }

    [Fact]
    public void Validate_rejects_finite_last_release_after_contiguous_release()
    {
        var releases = new[]
        {
            Release("version-a", new ServiceMonth(2024, 4), new ServiceMonth(2024, 5)),
            Release("version-b", new ServiceMonth(2024, 6), new ServiceMonth(2024, 7)),
        };

        FluentActions.Invoking(() => ClaimMasterCatalogPolicy.Validate(releases, Sources()))
            .Should().Throw<ArgumentException>()
            .WithMessage("*version-b*2024-07*");
    }

    [Fact]
    public void Validate_rejects_unknown_release_source_and_mentions_version_and_document_id()
    {
        var releases = new[]
        {
            Release("claim-master-unknown", sourceIds: ["source-missing"]),
        };

        FluentActions.Invoking(() => ClaimMasterCatalogPolicy.Validate(releases, Sources()))
            .Should().Throw<ArgumentException>()
            .WithMessage("*claim-master-unknown*source-missing*");
    }

    [Fact]
    public void Resolve_rejects_month_without_release_and_mentions_month()
    {
        FluentActions.Invoking(() => ClaimMasterCatalogPolicy.Resolve(
                Releases(),
                Sources(),
                new ServiceMonth(2024, 3)))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*2024-03*");
    }

    private static ClaimSourceDocument Source(
        string documentId = "source-a",
        string title = "Official source",
        string publisher = "MHLW",
        string url = "https://example.test/source.pdf",
        string sha256 = ValidSha256,
        string? supersedes = null,
        string? notes = null)
        => new(
            documentId,
            title,
            publisher,
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 7, 10),
            url,
            sha256,
            supersedes,
            notes);

    private static ClaimMasterRelease Release(
        string version = "claim-master-r8-06",
        ServiceMonth? effectiveFrom = null,
        ServiceMonth? effectiveTo = null,
        IReadOnlyList<string>? sourceIds = null)
        => new(
            new ClaimMasterVersion(version),
            effectiveFrom ?? new ServiceMonth(2026, 6),
            effectiveTo,
            sourceIds ?? ["source-a"]);

    private static ClaimSourceDocument[] Sources()
        => [Source("source-a")];

    private static ClaimMasterRelease[] Releases()
        =>
        [
            Release(
                "claim-master-r6-04",
                new ServiceMonth(2024, 4),
                new ServiceMonth(2024, 5)),
            Release(
                "claim-master-r6-06",
                new ServiceMonth(2024, 6),
                new ServiceMonth(2026, 5)),
            Release(
                "claim-master-r8-06",
                new ServiceMonth(2026, 6),
                null),
        ];
}
