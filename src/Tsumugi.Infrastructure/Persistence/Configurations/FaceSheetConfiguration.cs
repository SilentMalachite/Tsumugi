using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class FaceSheetConfiguration : IEntityTypeConfiguration<FaceSheet>
{
    public void Configure(EntityTypeBuilder<FaceSheet> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("FaceSheets");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.RecipientId).IsRequired();
        builder.HasIndex(f => f.RecipientId);
        builder.Property(f => f.PostalCode).HasMaxLength(16);
        builder.Property(f => f.Address).HasMaxLength(256);
        builder.Property(f => f.PhoneNumber).HasMaxLength(32);
        builder.Property(f => f.EmailAddress).HasMaxLength(128);
        builder.Property(f => f.EmergencyContactName).HasMaxLength(128);
        builder.Property(f => f.EmergencyContactRelationship).HasMaxLength(32);
        builder.Property(f => f.EmergencyContactPhone).HasMaxLength(32);
        builder.Property(f => f.FamilyComposition).HasMaxLength(512);
        builder.Property(f => f.Cohabitants).HasMaxLength(256);
        builder.Property(f => f.PrimaryDoctorName).HasMaxLength(128);
        builder.Property(f => f.PrimaryDoctorHospital).HasMaxLength(128);
        builder.Property(f => f.PrimaryDoctorPhone).HasMaxLength(32);
        builder.Property(f => f.MedicalHistory).HasMaxLength(1024);
        builder.Property(f => f.CurrentConditions).HasMaxLength(1024);
        builder.Property(f => f.Medications).HasMaxLength(1024);
        builder.Property(f => f.Allergies).HasMaxLength(256);
        builder.Property(f => f.ReceivesNursingInsurance).IsRequired();
        builder.Property(f => f.ReceivesDisabilityPension).IsRequired();
        builder.Property(f => f.PensionDetails).HasMaxLength(256);
        builder.Property(f => f.LifeHistory).HasMaxLength(2048);
        builder.Property(f => f.PersonalWishes).HasMaxLength(1024);
        builder.Property(f => f.SupportNeeds).HasMaxLength(1024);
        builder.Property(f => f.AssessmentSummary).HasMaxLength(2048);
        builder.Property(f => f.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(f => f.CreatedAt).IsRequired();
        builder.Property(f => f.ConcurrencyToken).IsConcurrencyToken();
    }
}
