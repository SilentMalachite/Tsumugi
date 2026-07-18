using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.ClaimMasters;
using Xunit;

namespace Tsumugi.Infrastructure.Tests.ClaimMasters;

/// <summary>
/// Task 9bで実データ配線した<see cref="OfficeClaimBillingTokenProvider"/>の契約を固定する。
/// 定員(実頭数)・人員配置区分tokenはprofile必須（未実装のため常にnullを返していたTask 9の
/// 状態を閉じる）。地域区分tokenはprofileの明示tokenを優先し、未入力時はOffice.RegionGrade
/// 由来の名義的既定へフォールバックする（既存事業所を後方互換で救う設計判断）。
/// 両ソースが揃って不一致のときはフェイルクローズする
/// （controller decision 2026-07-19, Task 9b fix round）。
/// </summary>
public sealed class OfficeClaimBillingTokenProviderTests
{
    private static readonly ServiceMonth Month = new(2026, 6);
    private static readonly Guid OfficeId = Guid.NewGuid();

    [Fact]
    public void Resolve_sources_capacity_and_staffing_from_profile()
    {
        var provider = new OfficeClaimBillingTokenProvider();
        var office = TestOffice(RegionGrade.Grade2);
        var profile = Profile(capacityHeadcount: 20, staffingKey: "staff-a", regionKey: null);

        var tokens = provider.Resolve(office, profile, Month);

        tokens.CapacityHeadcount.Should().Be(20);
        tokens.StaffingKey.Should().Be("staff-a");
        tokens.RewardSystem.Should().Be("employment-continuation-support-b");
    }

    [Fact]
    public void Resolve_returns_null_capacity_and_staffing_when_profile_is_absent()
    {
        var provider = new OfficeClaimBillingTokenProvider();
        var office = TestOffice(RegionGrade.Grade2);

        var tokens = provider.Resolve(office, profile: null, Month);

        tokens.CapacityHeadcount.Should().BeNull();
        tokens.StaffingKey.Should().BeNull();
    }

    [Fact]
    public void Resolve_accepts_the_profiles_explicit_region_key_when_it_agrees_with_region_grade()
    {
        // profileの明示tokenとOffice.RegionGrade由来の既定が一致するケース（(b)）。
        // 不一致でない限りprofile側が採用される（両ソースが同値なのでどちらを採用しても
        // 結果は同じだが、profile-override-with-fallbackの設計は維持する）。
        var provider = new OfficeClaimBillingTokenProvider();
        var office = TestOffice(RegionGrade.Grade2);
        var profile = Profile(capacityHeadcount: 20, staffingKey: "staff-a", regionKey: "region-grade-2");

        var tokens = provider.Resolve(office, profile, Month);

        tokens.RegionKey.Should().Be("region-grade-2");
        tokens.RegionKeyConflict.Should().BeFalse();
    }

    [Fact]
    public void Resolve_fails_closed_when_profile_region_key_disagrees_with_region_grade()
    {
        // (c) 両ソースが揃って不一致：どちらかを無言で採用せず、tokenをnullにして
        // 呼び出し側に専用issueへ変換させる（controller decision 2026-07-19, Task 9b fix round）。
        var provider = new OfficeClaimBillingTokenProvider();
        var office = TestOffice(RegionGrade.Grade2);
        var profile = Profile(capacityHeadcount: 20, staffingKey: "staff-a", regionKey: "region-a");

        var tokens = provider.Resolve(office, profile, Month);

        tokens.RegionKey.Should().BeNull();
        tokens.RegionKeyConflict.Should().BeTrue();
    }

    [Fact]
    public void Resolve_falls_back_to_region_grade_when_profile_region_key_is_absent()
    {
        // 既存事業所はOffice.RegionGradeだけを持ちprofileのRegionKeyを入力していない。
        // この後方互換フォールバックがなければ、profile拡張だけで既存の請求プレビューが
        // 突然NotReadyへ後退してしまう。
        var provider = new OfficeClaimBillingTokenProvider();
        var office = TestOffice(RegionGrade.Grade3);
        var profile = Profile(capacityHeadcount: 20, staffingKey: "staff-a", regionKey: null);

        var tokens = provider.Resolve(office, profile, Month);

        tokens.RegionKey.Should().Be("region-grade-3");
        tokens.RegionKeyConflict.Should().BeFalse();
    }

    [Fact]
    public void Resolve_falls_back_to_region_grade_when_profile_is_absent()
    {
        var provider = new OfficeClaimBillingTokenProvider();
        var office = TestOffice(RegionGrade.Grade1);

        var tokens = provider.Resolve(office, profile: null, Month);

        tokens.RegionKey.Should().Be("region-grade-1");
        tokens.RegionKeyConflict.Should().BeFalse();
    }

    [Fact]
    public void Resolve_treats_blank_profile_region_key_as_absent()
    {
        var provider = new OfficeClaimBillingTokenProvider();
        var office = TestOffice(RegionGrade.Grade4);
        var profile = Profile(capacityHeadcount: 20, staffingKey: "staff-a", regionKey: "   ");

        var tokens = provider.Resolve(office, profile, Month);

        tokens.RegionKey.Should().Be("region-grade-4");
    }

    private static Office TestOffice(RegionGrade regionGrade) => Domain.Entities.Office.Create(
        OfficeId,
        "1310000001",
        "テスト事業所",
        ServiceCategory.TypeB,
        regionGrade,
        "tester",
        DateTimeOffset.UnixEpoch,
        Guid.NewGuid());

    private static OfficeClaimProfile Profile(
        int? capacityHeadcount, string? staffingKey, string? regionKey)
    {
        var id = Guid.NewGuid();
        return new OfficeClaimProfile
        {
            Id = id,
            OfficeId = OfficeId,
            EffectiveFrom = new DateOnly(2024, 4, 1),
            EffectiveTo = null,
            RootId = id,
            Revision = 1,
            Kind = RecordKind.New,
            MasterVersion = new ClaimMasterVersion("master-v1"),
            ReformStatus = R8ReformStatus.NotApplicableBeforeR8,
            AverageWageBandOption = new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 5),
            EvidenceDocumentId = "profile-doc",
            ConfirmedAt = DateTimeOffset.UnixEpoch,
            ConfirmedBy = "admin",
            ConfirmationReason = "台帳確認",
            CapacityHeadcount = capacityHeadcount,
            StaffingKey = staffingKey,
            RegionKey = regionKey,
            CreatedAt = DateTimeOffset.UnixEpoch,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };
    }
}
