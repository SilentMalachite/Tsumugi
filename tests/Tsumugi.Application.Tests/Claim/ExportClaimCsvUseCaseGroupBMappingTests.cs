using System.Text;
using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Application.Dtos.Claim.Csv;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;
using Kit = Tsumugi.Application.Tests.UseCases.Claim.ClaimPreparationTestKit;

namespace Tsumugi.Application.Tests.Claim;

/// <summary>
/// グループB個別入力（Phase 3-3）が、確定 snapshot から CSV 生成入力
/// （<see cref="ClaimCsvDto"/>）まで値のまま届くこと。CSV 側の項目定義（fieldId）は
/// CSV 仕様 JSON の責務なので、ここで検証するのは<b>写像だけ</b>。
/// </summary>
public sealed class ExportClaimCsvUseCaseGroupBMappingTests
{
    private static readonly ProcessingMonth Processing = new(2026, 7);

    [Fact]
    public async Task Export_carries_group_b_explicit_addition_inputs_into_the_csv_dto()
    {
        var snapshot = Kit.FinalizationSnapshot() with
        {
            ClaimInput = new ClaimFinalizationClaimInputSnapshot(
                null, null, null, null, null, null, null,
                SpecialVisitSupportBilledCount: 2,
                OffsiteSupportCumulativeDays: 12),
            DailyRecords =
            [
                new ClaimFinalizationDailyRecordSnapshot(
                    new DateOnly(Kit.Month.Year, Kit.Month.Month, 1),
                    Attendance.Present, true, TransportKind.Round, null,
                    new TimeOnly(9, 0), new TimeOnly(16, 0), 45, false,
                    null, null, false, false, false, true,
                    SpecialVisitSupportBilledHours: 3),
            ],
        };
        var generator = new CapturingGenerator();

        await CreateSut(snapshot, generator).ExecuteAsync(
            Kit.OfficeId, Kit.Month, Processing, "operator", CancellationToken.None);

        var recipient = generator.Captured!.Recipients.Should().ContainSingle().Subject;
        recipient.SpecialVisitSupportBilledCount.Should().Be(2);
        recipient.OffsiteSupportCumulativeDays.Should().Be(12);
        var day = recipient.DailyRecords.Should().ContainSingle().Subject;
        day.SpecialVisitSupportBilledHours.Should().Be(3);
        // 算定時間数（時間）とサービス提供時間数（分）は別項目として並存する。
        day.SpecialVisitSupportMinutes.Should().Be(45);
    }

    [Fact]
    public async Task Export_passes_group_b_explicit_addition_inputs_as_null_when_the_snapshot_has_none()
    {
        var generator = new CapturingGenerator();

        await CreateSut(Kit.FinalizationSnapshot(), generator).ExecuteAsync(
            Kit.OfficeId, Kit.Month, Processing, "operator", CancellationToken.None);

        var recipient = generator.Captured!.Recipients.Should().ContainSingle().Subject;
        recipient.SpecialVisitSupportBilledCount.Should().BeNull();
        recipient.OffsiteSupportCumulativeDays.Should().BeNull();
        recipient.DailyRecords.Should().ContainSingle()
            .Which.SpecialVisitSupportBilledHours.Should().BeNull();
    }

    private static ExportClaimCsvUseCase CreateSut(
        ClaimFinalizationSnapshot snapshot, CapturingGenerator generator)
    {
        var header = Kit.Batch();
        return new ExportClaimCsvUseCase(
            Kit.VerifiedProvider(new ClaimBatchAggregate(header, [Detail(header, snapshot)])),
            new FakeOfficeContextProvider(),
            // 版解決は確定済みヘッダが記録した版を返す（版不一致の検証は別テスト）。
            new Kit.FakeCsvSpecificationVersions(header.CsvSpecificationVersion),
            generator,
            new FakeExportRepository(),
            TimeProvider.System);
    }

    private static ClaimDetail Detail(ClaimBatch header, ClaimFinalizationSnapshot snapshot)
        => ClaimDetail.Create(
            Guid.NewGuid(),
            header.Id,
            Kit.RecipientId,
            snapshotSchemaVersion: ClaimSnapshotValidationCodecV2.SchemaVersionValue,
            claimMasterVersion: header.ClaimMasterVersion,
            csvSpecificationVersion: header.CsvSpecificationVersion,
            reportSpecificationVersion: header.ReportSpecificationVersion,
            snapshotApplicationVersion: header.SnapshotApplicationVersion,
            inputSnapshotJson: Kit.MinimalEnvelopeJson,
            calculationSnapshotJson: Encoding.UTF8.GetString(
                ClaimFinalizationSnapshotWriter.Write(snapshot)),
            totalUnits: header.TotalUnits,
            totalCostYen: header.TotalCostYen,
            benefitYen: header.TotalBenefitYen,
            burdenYen: header.TotalBurdenYen,
            createdBy: header.CreatedBy,
            createdAt: Kit.Now);

    private sealed class CapturingGenerator : IClaimCsvGenerator
    {
        public ClaimCsvDto? Captured { get; private set; }

        // 確定 header が記録した版と一致させる（不一致は本番同様に fail-close するため）。

        public ClaimCsvDocument Generate(ClaimCsvDto dto)
        {
            Captured = dto;
            return new ClaimCsvDocument([1, 2, 3], "claim.csv");
        }
    }

    private sealed class FakeOfficeContextProvider : IClaimCsvOfficeContextProvider
    {
        public ClaimCsvOfficeContext Resolve(RegionGrade regionGrade, ServiceMonth serviceMonth)
            => new(UnitPriceMilliYen: 10_000);
    }

    private sealed class FakeExportRepository : IClaimCsvExportRepository
    {
        public Task AppendAsync(ClaimCsvExport csvExport, CancellationToken ct) => Task.CompletedTask;

        public Task<IReadOnlyList<ClaimCsvExport>> ListByBatchAsync(
            Guid claimBatchId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ClaimCsvExport>>([]);
    }
}
