using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class CertificateConfiguration : IEntityTypeConfiguration<Certificate>
{
    public void Configure(EntityTypeBuilder<Certificate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Certificates", table => table.HasCheckConstraint(
            "CK_Certificates_RevisionLineage",
            "\"Revision\" >= 1 AND ((\"Revision\" = 1 AND \"RootCertificateId\" = \"Id\" AND \"ExpectedHeadCertificateId\" IS NULL) OR (\"Revision\" >= 2 AND \"RootCertificateId\" <> \"Id\" AND \"ExpectedHeadCertificateId\" IS NOT NULL))"));
        builder.HasKey(c => c.Id);
        builder.Property(c => c.RootCertificateId).IsRequired();
        builder.Property(c => c.Revision).IsRequired();
        builder.Property(c => c.ExpectedHeadCertificateId);
        builder.HasIndex(c => new { c.RootCertificateId, c.Revision })
            .IsUnique()
            .HasDatabaseName("UX_Certificates_RootCertificateId_Revision");
        builder.HasIndex(c => c.ExpectedHeadCertificateId)
            .HasFilter("\"ExpectedHeadCertificateId\" IS NOT NULL")
            .IsUnique()
            .HasDatabaseName("UX_Certificates_ExpectedHeadCertificateId");
        builder.Property(c => c.RecipientId).IsRequired();
        builder.HasIndex(c => c.RecipientId);
        builder.Property(c => c.CertificateNumber).IsRequired().HasMaxLength(32);
        builder.Property(c => c.SupplyDays).IsRequired();
        builder.Property(c => c.MonthlyCostCap).IsRequired();
        builder.Property(c => c.Municipality).IsRequired().HasMaxLength(64);
        builder.Property(c => c.MunicipalityNumber).HasMaxLength(6);
        builder.Property(c => c.SubsidyMunicipalityNumber).HasMaxLength(6);
        builder.Property(c => c.UpperLimitManagementProviderNumber).HasMaxLength(10);
        builder.Property(c => c.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.ConcurrencyToken).IsConcurrencyToken();

        // DateRange は Start / End を JSON 文字列の単一列に展開
        builder.Property(c => c.Validity)
            .HasConversion(
                v => DateRangeJson.Serialize(v),
                s => DateRangeJson.Deserialize(s))
            .IsRequired()
            .HasColumnName("Validity");

        // -------- 支給決定障害者等：発行時点のスナップショット --------
        builder.Property(c => c.RecipientAddress).HasMaxLength(256);
        builder.Property(c => c.RecipientGender).IsRequired().HasConversion<int>();
        builder.Property(c => c.GuardianName).HasMaxLength(128);
        builder.Property(c => c.GuardianRelationship).HasMaxLength(32);

        // -------- 障害種別 --------
        // readonly record struct を ComplexProperty として 4 列にフラット展開する。
        builder.ComplexProperty(c => c.Disabilities, d =>
        {
            d.Property(x => x.Physical).HasColumnName("Disability_Physical").IsRequired();
            d.Property(x => x.Intellectual).HasColumnName("Disability_Intellectual").IsRequired();
            d.Property(x => x.Mental).HasColumnName("Disability_Mental").IsRequired();
            d.Property(x => x.Intractable).HasColumnName("Disability_Intractable").IsRequired();
        });
        builder.Property(c => c.SupportCategory).IsRequired().HasConversion<int>();

        // -------- 給付種別と支給決定内容 --------
        builder.Property(c => c.BenefitType).IsRequired().HasConversion<int>();
        builder.Property(c => c.ServiceCategory).IsRequired().HasMaxLength(64);
        builder.Property(c => c.SupplyNotes).HasMaxLength(1024);

        // -------- 計画相談支援給付費の支援内容 --------
        builder.Property(c => c.ConsultationProviderName).HasMaxLength(128);
        builder.Property(c => c.ConsultationProviderNumber).HasMaxLength(32);

        // -------- 利用者負担に関する事項 --------
        builder.Property(c => c.PaymentBurden).IsRequired().HasConversion<int>();
        builder.Property(c => c.UpperLimitManagementProvider).HasMaxLength(128);
        builder.Property(c => c.MealProvisionApplicable).IsRequired();
        builder.Property(c => c.HighCostBenefitApplicable).IsRequired();
    }
}
