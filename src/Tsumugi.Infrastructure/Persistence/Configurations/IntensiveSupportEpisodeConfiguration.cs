using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class IntensiveSupportEpisodeConfiguration : IEntityTypeConfiguration<IntensiveSupportEpisode>
{
    public void Configure(EntityTypeBuilder<IntensiveSupportEpisode> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ClaimInputConfigurationShared.ConfigureHistory(
            builder,
            "IntensiveSupportEpisodes",
            "(\"Kind\" = 3 AND \"StartDate\" IS NULL) OR (\"Kind\" IN (1, 2) AND \"StartDate\" IS NOT NULL)");
        builder.Property(x => x.OfficeId).IsRequired();
        builder.Property(x => x.RecipientId).IsRequired();
        builder.Property(x => x.StartDate);
        builder.HasIndex(
                x => new { x.OfficeId, x.RecipientId },
                "IX_IntensiveSupportEpisodes_OfficeId_RecipientId")
            .HasDatabaseName("IX_IntensiveSupportEpisodes_OfficeId_RecipientId");
        builder.HasIndex(x => new { x.OfficeId, x.RecipientId }).HasFilter("\"Kind\" = 1").IsUnique()
            .HasDatabaseName("UX_IntensiveSupportEpisodes_OfficeId_RecipientId_NewOnly");
        builder.HasOne<Office>().WithMany().HasForeignKey(x => x.OfficeId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_IntensiveSupportEpisodes_Offices_OfficeId");
        builder.HasOne<Recipient>().WithMany().HasForeignKey(x => x.RecipientId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_IntensiveSupportEpisodes_Recipients_RecipientId");
    }
}
