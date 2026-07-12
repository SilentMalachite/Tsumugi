using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class OfficeClaimProfileConfiguration : IEntityTypeConfiguration<OfficeClaimProfile>
{
    private static readonly ValueComparer<ClaimMasterVersion> VersionComparer = new(
        (left, right) => string.Equals(
            ToProviderValueOrNull(left),
            ToProviderValueOrNull(right),
            StringComparison.Ordinal),
        version => GetVersionHashCode(version),
        version => version);

    public void Configure(EntityTypeBuilder<OfficeClaimProfile> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ClaimInputConfigurationShared.ConfigureHistory(
            builder,
            "OfficeClaimProfiles",
            "\"Kind\" <> 3 OR (\"MasterVersion\" IS NULL AND \"ReformStatus\" IS NULL " +
            "AND \"AverageWageBandOption_Kind\" IS NULL AND \"AverageWageBandOption_OfficialOptionCode\" IS NULL " +
            "AND \"DesignationDate\" IS NULL AND \"SupportStartDate\" IS NULL " +
            "AND \"EarlierRegisteredBandOption_MasterVersion\" IS NULL " +
            "AND \"EarlierRegisteredBandOption_Option_Kind\" IS NULL " +
            "AND \"EarlierRegisteredBandOption_Option_OfficialOptionCode\" IS NULL " +
            "AND \"EarlierRegistrationMonthKey\" IS NULL AND \"LaterRegisteredBandOption_MasterVersion\" IS NULL " +
            "AND \"LaterRegisteredBandOption_Option_Kind\" IS NULL " +
            "AND \"LaterRegisteredBandOption_Option_OfficialOptionCode\" IS NULL " +
            "AND \"LaterRegistrationMonthKey\" IS NULL AND \"ReformComparisonEvidenceDocumentId\" IS NULL " +
            "AND \"FiledTransitionPeriod\" IS NULL AND \"FiledTransitionEvidenceDocumentId\" IS NULL " +
            "AND \"EvidenceDocumentId\" IS NULL AND \"ConfirmedAt\" IS NULL AND \"ConfirmedBy\" IS NULL " +
            "AND \"ConfirmationReason\" IS NULL)",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_OfficeClaimProfiles_AverageWageBandOption",
                    "((\"AverageWageBandOption_Kind\" IS NULL AND \"AverageWageBandOption_OfficialOptionCode\" IS NULL) OR " +
                    "(\"AverageWageBandOption_Kind\" IS NOT NULL " +
                    "AND \"AverageWageBandOption_OfficialOptionCode\" IS NOT NULL " +
                    "AND \"AverageWageBandOption_Kind\" IN (1, 2, 3) " +
                    "AND \"AverageWageBandOption_OfficialOptionCode\" > 0))");
                table.HasCheckConstraint(
                    "CK_OfficeClaimProfiles_ReformStatus_ClosedSet",
                    "\"ReformStatus\" IS NULL OR \"ReformStatus\" IN (1, 2, 3, 4)");
                AddVersionedOptionCheck(table, "EarlierRegisteredBandOption");
                AddVersionedOptionCheck(table, "LaterRegisteredBandOption");
            });
        builder.Property(x => x.OfficeId).IsRequired();
        builder.Property(x => x.EffectiveFrom).IsRequired();
        builder.Property(x => x.EffectiveTo);
        builder.Property(x => x.MasterVersion)
            .HasConversion(
                value => value.HasValue ? value.Value.Value : null,
                value => value == null ? (ClaimMasterVersion?)null : new ClaimMasterVersion(value))
            .HasMaxLength(ClaimMasterVersion.MaxLength);
        builder.Property(x => x.ReformStatus).HasConversion<int?>();
        builder.Property(x => x.EarlierRegistrationMonth)
            .HasConversion(
                value => value.HasValue ? value.Value.ToInt() : (int?)null,
                value => value.HasValue ? ServiceMonth.FromInt(value.Value) : (ServiceMonth?)null)
            .HasColumnName("EarlierRegistrationMonthKey");
        builder.Property(x => x.LaterRegistrationMonth)
            .HasConversion(
                value => value.HasValue ? value.Value.ToInt() : (int?)null,
                value => value.HasValue ? ServiceMonth.FromInt(value.Value) : (ServiceMonth?)null)
            .HasColumnName("LaterRegistrationMonthKey");
        builder.Property(x => x.FiledTransitionPeriod)
            .HasConversion(
                value => value.HasValue ? DateRangeJson.Serialize(value.Value) : null,
                value => value == null ? (DateRange?)null : DateRangeJson.Deserialize(value))
            .HasColumnName("FiledTransitionPeriod");
        builder.ComplexProperty(nameof(OfficeClaimProfile.AverageWageBandOption), option =>
        {
            option.IsRequired(false);
            option.Property<AverageWageBandOptionKind>(nameof(AverageWageBandOption.Kind))
                .HasConversion<int>().HasColumnName("AverageWageBandOption_Kind");
            option.Property<int>(nameof(AverageWageBandOption.OfficialOptionCode))
                .HasColumnName("AverageWageBandOption_OfficialOptionCode");
        });
        ConfigureVersionedOption(builder, nameof(OfficeClaimProfile.EarlierRegisteredBandOption), "EarlierRegisteredBandOption");
        ConfigureVersionedOption(builder, nameof(OfficeClaimProfile.LaterRegisteredBandOption), "LaterRegisteredBandOption");
        builder.HasIndex(x => new { x.OfficeId, x.EffectiveFrom, x.EffectiveTo })
            .HasFilter("\"Kind\" = 1 AND \"EffectiveTo\" IS NOT NULL").IsUnique()
            .HasDatabaseName("UX_OfficeClaimProfiles_OfficeId_EffectiveFrom_EffectiveTo_ClosedNewOnly");
        builder.HasIndex(x => new { x.OfficeId, x.EffectiveFrom })
            .HasFilter("\"Kind\" = 1 AND \"EffectiveTo\" IS NULL").IsUnique()
            .HasDatabaseName("UX_OfficeClaimProfiles_OfficeId_EffectiveFrom_OpenNewOnly");
        builder.HasOne<Office>().WithMany().HasForeignKey(x => x.OfficeId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_OfficeClaimProfiles_Offices_OfficeId");
    }

    private static void ConfigureVersionedOption(
        EntityTypeBuilder<OfficeClaimProfile> builder,
        string propertyName,
        string prefix)
    {
        builder.ComplexProperty(propertyName, versioned =>
        {
            versioned.IsRequired(false);
            versioned.Ignore(nameof(VersionedAverageWageBandOption.Option));
            var masterVersion = versioned
                .Property<ClaimMasterVersion>(nameof(VersionedAverageWageBandOption.MasterVersion));
            masterVersion
                .HasConversion(value => value.Value, value => new ClaimMasterVersion(value))
                .HasMaxLength(ClaimMasterVersion.MaxLength)
                .HasColumnName($"{prefix}_MasterVersion");
            masterVersion.Metadata.SetValueComparer(VersionComparer);
            versioned.Property<AverageWageBandOptionKind>("Kind")
                .HasConversion<int>().HasColumnName($"{prefix}_Option_Kind");
            versioned.Property<int>("OfficialOptionCode")
                .HasColumnName($"{prefix}_Option_OfficialOptionCode");
        });
    }

    private static void AddVersionedOptionCheck(TableBuilder<OfficeClaimProfile> table, string prefix) =>
        table.HasCheckConstraint(
            $"CK_OfficeClaimProfiles_{prefix}",
            $"((\"{prefix}_MasterVersion\" IS NULL AND \"{prefix}_Option_Kind\" IS NULL AND " +
            $"\"{prefix}_Option_OfficialOptionCode\" IS NULL) OR " +
            $"(\"{prefix}_MasterVersion\" IS NOT NULL AND \"{prefix}_Option_Kind\" IS NOT NULL AND " +
            $"\"{prefix}_Option_OfficialOptionCode\" IS NOT NULL " +
            $"AND length(trim(\"{prefix}_MasterVersion\")) BETWEEN 1 AND 64 " +
            $"AND \"{prefix}_Option_Kind\" IN (1, 2, 3) AND \"{prefix}_Option_OfficialOptionCode\" > 0))");

    private static string? ToProviderValueOrNull(ClaimMasterVersion version)
    {
        try
        {
            return version.Value;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static int GetVersionHashCode(ClaimMasterVersion version)
    {
        var value = ToProviderValueOrNull(version);
        return value is null ? 0 : StringComparer.Ordinal.GetHashCode(value);
    }
}
