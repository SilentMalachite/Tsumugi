using FluentAssertions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Domain.Tests;

public sealed class FaceSheetTests
{
    private static readonly DateTimeOffset T = new(2026, 6, 28, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_with_minimal_args_initializes_defaults_to_null_or_false()
    {
        var id = Guid.NewGuid();
        var rid = Guid.NewGuid();
        var token = Guid.NewGuid();

        var f = FaceSheet.Create(id, rid, createdBy: "t", createdAt: T, concurrencyToken: token);

        f.Id.Should().Be(id);
        f.RecipientId.Should().Be(rid);
        f.CreatedBy.Should().Be("t");
        f.CreatedAt.Should().Be(T);
        f.ConcurrencyToken.Should().Be(token);

        // 連絡先・家族・医療・受給状況・生活歴の string プロパティはすべて null 既定
        f.PostalCode.Should().BeNull();
        f.Address.Should().BeNull();
        f.PhoneNumber.Should().BeNull();
        f.EmailAddress.Should().BeNull();
        f.EmergencyContactName.Should().BeNull();
        f.EmergencyContactRelationship.Should().BeNull();
        f.EmergencyContactPhone.Should().BeNull();
        f.FamilyComposition.Should().BeNull();
        f.Cohabitants.Should().BeNull();
        f.PrimaryDoctorName.Should().BeNull();
        f.PrimaryDoctorHospital.Should().BeNull();
        f.PrimaryDoctorPhone.Should().BeNull();
        f.MedicalHistory.Should().BeNull();
        f.CurrentConditions.Should().BeNull();
        f.Medications.Should().BeNull();
        f.Allergies.Should().BeNull();
        f.PensionDetails.Should().BeNull();
        f.LifeHistory.Should().BeNull();
        f.PersonalWishes.Should().BeNull();
        f.SupportNeeds.Should().BeNull();
        f.AssessmentSummary.Should().BeNull();

        // bool は false 既定
        f.ReceivesNursingInsurance.Should().BeFalse();
        f.ReceivesDisabilityPension.Should().BeFalse();
    }

    [Fact]
    public void Create_with_all_optional_args_roundtrips_every_field()
    {
        var f = FaceSheet.Create(
            id: Guid.NewGuid(),
            recipientId: Guid.NewGuid(),
            createdBy: "tester",
            createdAt: T,
            concurrencyToken: Guid.NewGuid(),
            postalCode: "100-0001",
            address: "東京都千代田区千代田1-1",
            phoneNumber: "03-0000-0001",
            emailAddress: "user@example.test",
            emergencyContactName: "親族 太郎",
            emergencyContactRelationship: "父",
            emergencyContactPhone: "090-0000-0002",
            familyComposition: "三世代同居",
            cohabitants: "本人・配偶者・子",
            primaryDoctorName: "主治医 花子",
            primaryDoctorHospital: "千代田総合病院",
            primaryDoctorPhone: "03-0000-0003",
            medicalHistory: "高血圧 2010〜",
            currentConditions: "経過良好",
            medications: "降圧剤",
            allergies: "卵",
            receivesNursingInsurance: true,
            receivesDisabilityPension: true,
            pensionDetails: "障害基礎年金 1級",
            lifeHistory: "本人記録",
            personalWishes: "自立した生活",
            supportNeeds: "通所継続",
            assessmentSummary: "総合所見");

        f.PostalCode.Should().Be("100-0001");
        f.Address.Should().Be("東京都千代田区千代田1-1");
        f.PhoneNumber.Should().Be("03-0000-0001");
        f.EmailAddress.Should().Be("user@example.test");
        f.EmergencyContactName.Should().Be("親族 太郎");
        f.EmergencyContactRelationship.Should().Be("父");
        f.EmergencyContactPhone.Should().Be("090-0000-0002");
        f.FamilyComposition.Should().Be("三世代同居");
        f.Cohabitants.Should().Be("本人・配偶者・子");
        f.PrimaryDoctorName.Should().Be("主治医 花子");
        f.PrimaryDoctorHospital.Should().Be("千代田総合病院");
        f.PrimaryDoctorPhone.Should().Be("03-0000-0003");
        f.MedicalHistory.Should().Be("高血圧 2010〜");
        f.CurrentConditions.Should().Be("経過良好");
        f.Medications.Should().Be("降圧剤");
        f.Allergies.Should().Be("卵");
        f.ReceivesNursingInsurance.Should().BeTrue();
        f.ReceivesDisabilityPension.Should().BeTrue();
        f.PensionDetails.Should().Be("障害基礎年金 1級");
        f.LifeHistory.Should().Be("本人記録");
        f.PersonalWishes.Should().Be("自立した生活");
        f.SupportNeeds.Should().Be("通所継続");
        f.AssessmentSummary.Should().Be("総合所見");
    }

    [Fact]
    public void Created_record_is_value_equal_when_all_fields_match()
    {
        var id = Guid.NewGuid();
        var rid = Guid.NewGuid();
        var token = Guid.NewGuid();
        var a = FaceSheet.Create(id, rid, "t", T, token, address: "東京");
        var b = FaceSheet.Create(id, rid, "t", T, token, address: "東京");
        a.Should().Be(b);
    }
}
