using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class ContractedProviderConfiguration : IEntityTypeConfiguration<ContractedProvider>
{
    public void Configure(EntityTypeBuilder<ContractedProvider> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ContractedProviders");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.CertificateId).IsRequired();
        builder.HasIndex(p => p.CertificateId);
        builder.Property(p => p.ProviderNumber).IsRequired().HasMaxLength(32);
        builder.Property(p => p.ProviderName).IsRequired().HasMaxLength(128);
        builder.Property(p => p.ServiceCategory).IsRequired().HasMaxLength(64);
        builder.Property(p => p.ContractedSupplyDays).IsRequired();
        builder.Property(p => p.ContractDate).IsRequired();
        builder.Property(p => p.Notes).HasMaxLength(512);
        builder.Property(p => p.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.ConcurrencyToken).IsConcurrencyToken();
    }
}
