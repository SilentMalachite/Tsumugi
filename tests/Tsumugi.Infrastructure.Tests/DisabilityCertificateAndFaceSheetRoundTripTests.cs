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

    [Fact]
    public async Task DisabilityCertificate_ListAllAsync_returns_all_recipients_without_tracking()
    {
        var first = DisabilityCertificate.Create(
            Guid.NewGuid(), Guid.NewGuid(), DisabilityCertificateType.Mental, "2級",
            new DateOnly(2024, 4, 1), "東京都", "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        var second = DisabilityCertificate.Create(
            Guid.NewGuid(), Guid.NewGuid(), DisabilityCertificateType.Physical, "1級",
            new DateOnly(2025, 4, 1), "東京都", "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        await using (var ctx = _fixture.NewContext())
        {
            ctx.DisabilityCertificates.AddRange(first, second);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _fixture.NewContext())
        {
            var repo = new DisabilityCertificateRepository(ctx);
            var all = await repo.ListAllAsync(default);

            all.Select(c => c.Id).Should().Contain([first.Id, second.Id]);
            ctx.ChangeTracker.Entries<DisabilityCertificate>().Should().BeEmpty(
                "全件照会は AsNoTracking で追跡しない");
        }
    }

    [Fact]
    public async Task FaceSheet_ListByRecipientAsync_returns_created_at_ascending_without_other_recipients()
    {
        var recipientId = Guid.NewGuid();
        var otherRecipientId = Guid.NewGuid();
        var oldest = FaceSheet.Create(
            Guid.NewGuid(), recipientId, "first", DateTimeOffset.UnixEpoch, Guid.NewGuid(),
            phoneNumber: "111");
        var other = FaceSheet.Create(
            Guid.NewGuid(), otherRecipientId, "other", DateTimeOffset.UnixEpoch.AddMinutes(30), Guid.NewGuid(),
            phoneNumber: "999");
        var newest = FaceSheet.Create(
            Guid.NewGuid(), recipientId, "second", DateTimeOffset.UnixEpoch.AddHours(1), Guid.NewGuid(),
            phoneNumber: "222");
        await using (var ctx = _fixture.NewContext())
        {
            ctx.FaceSheets.AddRange(newest, other, oldest);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _fixture.NewContext())
        {
            var repo = new FaceSheetRepository(ctx);
            var list = await repo.ListByRecipientAsync(recipientId, default);

            list.Should().HaveCount(2);
            list.Select(f => f.Id).Should().Equal(oldest.Id, newest.Id);
            list.Should().OnlyContain(f => f.RecipientId == recipientId);
        }
    }
}
