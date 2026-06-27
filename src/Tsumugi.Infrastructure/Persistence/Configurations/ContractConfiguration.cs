using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Contracts");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.RecipientId).IsRequired();
        builder.HasIndex(c => c.RecipientId);
        builder.Property(c => c.ContractedSupplyDays).IsRequired();
        builder.Property(c => c.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.ConcurrencyToken).IsConcurrencyToken();

        // DateRange は Start / End を JSON 文字列の単一列に展開
        builder.Property(c => c.Period)
            .HasConversion(
                v => DateRangeJson.Serialize(v),
                s => DateRangeJson.Deserialize(s))
            .IsRequired()
            .HasColumnName("Period");
    }
}
