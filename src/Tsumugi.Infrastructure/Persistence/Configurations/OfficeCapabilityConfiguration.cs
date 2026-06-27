using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class OfficeCapabilityConfiguration : IEntityTypeConfiguration<OfficeCapability>
{
    public void Configure(EntityTypeBuilder<OfficeCapability> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("OfficeCapabilities");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.OfficeId).IsRequired();
        builder.HasIndex(c => c.OfficeId);
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

        // Flags: Dictionary<string,bool> を JSON テキスト列として保存
        builder.Property(c => c.Flags)
            .HasConversion(
                f => JsonSerializer.Serialize(f, (JsonSerializerOptions?)null),
                s => DeserializeFlags(s))
            .IsRequired()
            .HasColumnName("FlagsJson");
    }

    private static ReadOnlyDictionary<string, bool> DeserializeFlags(string s) =>
        new(JsonSerializer.Deserialize<Dictionary<string, bool>>(s, (JsonSerializerOptions?)null)
            ?? throw new InvalidOperationException("Flags のデシリアライズに失敗"));
}
