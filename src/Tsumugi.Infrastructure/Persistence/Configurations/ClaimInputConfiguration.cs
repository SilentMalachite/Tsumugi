using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class ClaimInputConfiguration : IEntityTypeConfiguration<ClaimInput>
{
    public void Configure(EntityTypeBuilder<ClaimInput> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ClaimInputConfigurationShared.ConfigureHistory(
            builder,
            "ClaimInputs",
            "\"Kind\" <> 3 OR (\"UpperLimitManagementResult\" IS NULL AND \"UpperLimitManagedAmountYen\" IS NULL " +
            "AND \"MunicipalSubsidyAmountYen\" IS NULL AND \"ExceptionalUsageStartMonthKey\" IS NULL " +
            "AND \"ExceptionalUsageEndMonthKey\" IS NULL AND \"ExceptionalUsageDays\" IS NULL " +
            "AND \"StandardUsageDayTotal\" IS NULL)",
            table => table.HasCheckConstraint(
                "CK_ClaimInputs_UpperLimitManagementResult_ClosedSet",
                "\"UpperLimitManagementResult\" IS NULL OR \"UpperLimitManagementResult\" IN (1, 2, 3)"));
        builder.Property(x => x.OfficeId).IsRequired();
        builder.Property(x => x.RecipientId).IsRequired();
        builder.Property(x => x.ServiceMonth)
            .HasConversion(value => value.ToInt(), value => ServiceMonth.FromInt(value))
            .HasColumnName("ServiceMonthKey")
            .IsRequired();
        builder.Property(x => x.UpperLimitManagementResult).HasConversion<int?>();
        builder.Property(x => x.ExceptionalUsageStartMonth)
            .HasConversion(
                value => value.HasValue ? value.Value.ToInt() : (int?)null,
                value => value.HasValue ? ServiceMonth.FromInt(value.Value) : (ServiceMonth?)null)
            .HasColumnName("ExceptionalUsageStartMonthKey");
        builder.Property(x => x.ExceptionalUsageEndMonth)
            .HasConversion(
                value => value.HasValue ? value.Value.ToInt() : (int?)null,
                value => value.HasValue ? ServiceMonth.FromInt(value.Value) : (ServiceMonth?)null)
            .HasColumnName("ExceptionalUsageEndMonthKey");
        builder.HasIndex(x => new { x.OfficeId, x.RecipientId, x.ServiceMonth })
            .HasFilter("\"Kind\" = 1")
            .IsUnique()
            .HasDatabaseName("UX_ClaimInputs_OfficeId_RecipientId_ServiceMonthKey_NewOnly");
        builder.HasOne<Office>().WithMany().HasForeignKey(x => x.OfficeId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ClaimInputs_Offices_OfficeId");
        builder.HasOne<Recipient>().WithMany().HasForeignKey(x => x.RecipientId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ClaimInputs_Recipients_RecipientId");
    }
}

internal static class ClaimInputConfigurationShared
{
    internal static void ConfigureHistory<TEntity>(
        EntityTypeBuilder<TEntity> builder,
        string tableName,
        string cancelPayloadCheck,
        Action<TableBuilder<TEntity>>? configureAdditionalChecks = null)
        where TEntity : Entity
    {
        builder.ToTable(tableName, table =>
        {
            table.HasCheckConstraint(
                $"CK_{tableName}_RevisionLineage",
                "\"Revision\" >= 1 AND \"Kind\" IN (1, 2, 3) AND " +
                "((\"Revision\" = 1 AND \"RootId\" = \"Id\" AND \"Kind\" = 1 AND \"ExpectedHeadId\" IS NULL) OR " +
                "(\"Revision\" >= 2 AND \"RootId\" <> \"Id\" AND \"Kind\" IN (2, 3) AND \"ExpectedHeadId\" IS NOT NULL))");
            table.HasCheckConstraint($"CK_{tableName}_CancelPayload", cancelPayloadCheck);
            configureAdditionalChecks?.Invoke(table);
        });
        builder.HasKey("Id");
        builder.Property<Guid>("RootId").IsRequired();
        builder.Property<int>("Revision").IsRequired();
        builder.Property("Kind").HasConversion<int>().IsRequired();
        builder.Property<Guid?>("ExpectedHeadId");
        builder.Property<string>(nameof(Entity.CreatedBy)).IsRequired().HasMaxLength(64);
        builder.Property<DateTimeOffset>(nameof(Entity.CreatedAt)).IsRequired();
        builder.Property<Guid>(nameof(Entity.ConcurrencyToken)).IsRequired();
        builder.HasIndex("RootId", "Revision").IsUnique()
            .HasDatabaseName($"UX_{tableName}_RootId_Revision");
        builder.HasIndex("ExpectedHeadId").HasFilter("\"ExpectedHeadId\" IS NOT NULL").IsUnique()
            .HasDatabaseName($"UX_{tableName}_ExpectedHeadId");
        builder.HasOne<TEntity>().WithMany().HasForeignKey("RootId")
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName($"FK_{tableName}_{tableName}_RootId");
        builder.HasOne<TEntity>().WithMany().HasForeignKey("ExpectedHeadId")
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName($"FK_{tableName}_{tableName}_ExpectedHeadId");
    }

    internal static void ConfigureEnteredYen<TEntity>(
        EntityTypeBuilder<TEntity> builder,
        string propertyName,
        string columnPrefix)
        where TEntity : class
    {
        builder.ComplexProperty(propertyName, amount =>
        {
            amount.Property<bool>("IsEntered").HasColumnName($"{columnPrefix}_IsEntered").IsRequired();
            amount.Property<int?>("ValueYen").HasColumnName($"{columnPrefix}_ValueYen");
        });
    }

    internal static void AddEnteredYenCheck<TEntity>(TableBuilder<TEntity> table, string tableName, string prefix)
        where TEntity : class =>
        table.HasCheckConstraint(
            $"CK_{tableName}_{prefix}_EnteredYen",
            $"((\"{prefix}_IsEntered\" = 0 AND \"{prefix}_ValueYen\" IS NULL) OR " +
            $"(\"{prefix}_IsEntered\" = 1 AND \"{prefix}_ValueYen\" IS NOT NULL " +
            $"AND \"{prefix}_ValueYen\" >= 0))");
}
