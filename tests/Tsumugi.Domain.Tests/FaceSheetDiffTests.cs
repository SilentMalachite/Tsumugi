using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Logic;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class FaceSheetDiffTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 8, 2, 10, 0, 0, TimeSpan.Zero);

    private static FaceSheet Sheet(
        Guid? id = null,
        Guid? recipientId = null,
        string createdBy = "u",
        DateTimeOffset? createdAt = null,
        Guid? concurrencyToken = null,
        string? postalCode = null,
        string? address = null,
        string? phoneNumber = null,
        string? emailAddress = null,
        string? emergencyContactName = null,
        string? emergencyContactRelationship = null,
        string? emergencyContactPhone = null,
        string? familyComposition = null,
        string? cohabitants = null,
        string? primaryDoctorName = null,
        string? primaryDoctorHospital = null,
        string? primaryDoctorPhone = null,
        string? medicalHistory = null,
        string? currentConditions = null,
        string? medications = null,
        string? allergies = null,
        bool receivesNursingInsurance = false,
        bool receivesDisabilityPension = false,
        string? pensionDetails = null,
        string? lifeHistory = null,
        string? personalWishes = null,
        string? supportNeeds = null,
        string? assessmentSummary = null) =>
        FaceSheet.Create(
            id ?? Guid.NewGuid(),
            recipientId ?? Guid.NewGuid(),
            createdBy,
            createdAt ?? T0,
            concurrencyToken ?? Guid.NewGuid(),
            postalCode,
            address,
            phoneNumber,
            emailAddress,
            emergencyContactName,
            emergencyContactRelationship,
            emergencyContactPhone,
            familyComposition,
            cohabitants,
            primaryDoctorName,
            primaryDoctorHospital,
            primaryDoctorPhone,
            medicalHistory,
            currentConditions,
            medications,
            allergies,
            receivesNursingInsurance,
            receivesDisabilityPension,
            pensionDetails,
            lifeHistory,
            personalWishes,
            supportNeeds,
            assessmentSummary);

    [Fact]
    public void Identical_business_values_return_empty()
    {
        var older = Sheet(address: "A", receivesDisabilityPension: true);
        var newer = Sheet(
            address: "A",
            receivesDisabilityPension: true,
            createdAt: T1);

        FaceSheetDiff.Compare(older, newer).Should().BeEmpty();
    }

    [Fact]
    public void Address_string_change_is_reported()
    {
        var older = Sheet(address: "旧住所");
        var newer = Sheet(address: "新住所", createdAt: T1);

        var changes = FaceSheetDiff.Compare(older, newer);

        changes.Should().ContainSingle();
        changes[0].PropertyName.Should().Be("Address");
        changes[0].OldValue.Should().Be("旧住所");
        changes[0].NewValue.Should().Be("新住所");
    }

    [Fact]
    public void Bool_change_is_reported_as_string()
    {
        var older = Sheet(receivesDisabilityPension: false);
        var newer = Sheet(receivesDisabilityPension: true, createdAt: T1);

        var changes = FaceSheetDiff.Compare(older, newer);

        changes.Should().ContainSingle();
        changes[0].PropertyName.Should().Be("ReceivesDisabilityPension");
        changes[0].OldValue.Should().Be(false.ToString());
        changes[0].NewValue.Should().Be(true.ToString());
    }

    [Fact]
    public void Null_and_empty_string_are_distinct()
    {
        var older = Sheet(address: null);
        var newer = Sheet(address: "", createdAt: T1);

        var changes = FaceSheetDiff.Compare(older, newer);

        changes.Should().ContainSingle();
        changes[0].PropertyName.Should().Be("Address");
        changes[0].OldValue.Should().BeNull();
        changes[0].NewValue.Should().Be("");
    }

    [Fact]
    public void Identity_and_audit_fields_are_ignored()
    {
        var recipientId = Guid.NewGuid();
        var older = Sheet(
            id: Guid.NewGuid(),
            recipientId: recipientId,
            createdBy: "old-user",
            createdAt: T0,
            concurrencyToken: Guid.NewGuid(),
            address: "同じ");
        var newer = Sheet(
            id: Guid.NewGuid(),
            recipientId: Guid.NewGuid(),
            createdBy: "new-user",
            createdAt: T1,
            concurrencyToken: Guid.NewGuid(),
            address: "同じ");

        FaceSheetDiff.Compare(older, newer).Should().BeEmpty();
    }

    [Fact]
    public void Multiple_changes_follow_fixed_property_order()
    {
        var older = Sheet(
            postalCode: "100-0001",
            address: "旧",
            assessmentSummary: "旧要約");
        var newer = Sheet(
            postalCode: "100-0002",
            address: "新",
            assessmentSummary: "新要約",
            createdAt: T1);

        var changes = FaceSheetDiff.Compare(older, newer);

        changes.Select(c => c.PropertyName)
            .Should().Equal("PostalCode", "Address", "AssessmentSummary");
    }
}
