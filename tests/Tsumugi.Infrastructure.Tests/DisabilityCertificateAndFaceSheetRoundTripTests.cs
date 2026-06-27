using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

/// <summary>
/// マイグレーション AddDisabilityCertificateAndFaceSheet と新 Repository を実 SQLite で検証する。
/// 両者は append-only 対象なので UPDATE は AppendOnlyGuard により拒否されることも併せて確認する。
/// </summary>
public sealed class DisabilityCertificateAndFaceSheetRoundTripTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;
    public DisabilityCertificateAndFaceSheetRoundTripTests(SqliteFixture f) => _fixture = f;

    [Fact]
    public async Task DisabilityCertificate_round_trips_and_is_append_only()
    {
        var id = Guid.NewGuid();
        var rid = Guid.NewGuid();
        await using (var ctx = _fixture.NewContext())
        {
            ctx.DisabilityCertificates.Add(DisabilityCertificate.Create(
                id, rid, DisabilityCertificateType.Mental, "2級",
                new DateOnly(2024, 4, 1), "東京都",
                "u", DateTimeOffset.UnixEpoch, Guid.NewGuid(),
                nextRenewalDate: new DateOnly(2026, 3, 31),
                certificateNumber: "M-1234",
                notes: "更新済み"));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _fixture.NewContext())
        {
            var loaded = await ctx.DisabilityCertificates.AsNoTracking().FirstAsync(x => x.Id == id);
            loaded.Type.Should().Be(DisabilityCertificateType.Mental);
            loaded.Grade.Should().Be("2級");
            loaded.NextRenewalDate.Should().Be(new DateOnly(2026, 3, 31));
            loaded.CertificateNumber.Should().Be("M-1234");
        }

        // 既存行への UPDATE は AppendOnlyGuard で拒否されることを確認
        await using (var ctx = _fixture.NewContext())
        {
            var entity = await ctx.DisabilityCertificates.FirstAsync(x => x.Id == id);
            ctx.Entry(entity).Property(x => x.Notes).CurrentValue = "改竄";
            Func<Task> act = () => ctx.SaveChangesAsync();
            await act.Should().ThrowAsync<AppendOnlyViolationException>();
        }
    }

    [Fact]
    public async Task FaceSheet_round_trips_and_latest_query_returns_newest()
    {
        var rid = Guid.NewGuid();
        await using (var ctx = _fixture.NewContext())
        {
            ctx.FaceSheets.Add(FaceSheet.Create(
                Guid.NewGuid(), rid, "u", DateTimeOffset.UnixEpoch, Guid.NewGuid(),
                phoneNumber: "111", address: "古い住所"));
            ctx.FaceSheets.Add(FaceSheet.Create(
                Guid.NewGuid(), rid, "u", DateTimeOffset.UnixEpoch.AddHours(1), Guid.NewGuid(),
                phoneNumber: "222", address: "新しい住所",
                primaryDoctorName: "山田", lifeHistory: "...",
                receivesDisabilityPension: true,
                pensionDetails: "障害基礎年金1級"));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _fixture.NewContext())
        {
            var repo = new FaceSheetRepository(ctx);
            var latest = await repo.FindLatestByRecipientAsync(rid, default);
            latest.Should().NotBeNull();
            latest!.PhoneNumber.Should().Be("222");
            latest.Address.Should().Be("新しい住所");
            latest.PrimaryDoctorName.Should().Be("山田");
            latest.ReceivesDisabilityPension.Should().BeTrue();
            latest.PensionDetails.Should().Be("障害基礎年金1級");
        }
    }
}
