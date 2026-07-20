using System.Text;
using FluentAssertions;
using Tsumugi.App.Services;
using Tsumugi.App.ViewModels.Claim;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.Dtos.Claim.Reports;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Kit = Tsumugi.App.Tests.ClaimPreparationViewModelTestKit;

namespace Tsumugi.App.Tests.ViewModels.Claim;

/// <summary>
/// Task 14: <see cref="ClaimReportSection"/>の3保存コマンド（SaveServiceProvisionRecord/
/// SaveClaimInvoice/SaveClaimStatement）のCanExecuteゲートと、
/// GenerateClaimReportsUseCase → IFileSaveService の配線を検証する。
/// このプロジェクトにMoqの依存が無いため、既存App.Tests/Application.Tests流儀
/// （手書きFake）に合わせる。
/// </summary>
public sealed class ClaimReportSectionTests
{
    [Fact]
    public void Commands_are_disabled_when_no_finalized_revision_exists()
    {
        var sut = CreateSection(out _, out _);
        sut.HasFinalizedRevision = false;

        sut.SaveServiceProvisionRecordCommand.CanExecute(null).Should().BeFalse();
        sut.SaveClaimInvoiceCommand.CanExecute(null).Should().BeFalse();
        sut.SaveClaimStatementCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void ServiceProvisionRecord_command_requires_selected_recipient_while_others_do_not()
    {
        var sut = CreateSection(out _, out _);
        sut.HasFinalizedRevision = true;
        sut.SelectedRecipient = null;

        sut.SaveServiceProvisionRecordCommand.CanExecute(null).Should().BeFalse();
        sut.SaveClaimInvoiceCommand.CanExecute(null).Should().BeTrue();
        sut.SaveClaimStatementCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void All_three_commands_are_enabled_once_a_recipient_is_selected()
    {
        var sut = CreateSection(out _, out _);
        sut.HasFinalizedRevision = true;
        sut.SelectedRecipient = new ClaimReportRecipientOption(Kit.RecipientId, "山田太郎");

        sut.SaveServiceProvisionRecordCommand.CanExecute(null).Should().BeTrue();
        sut.SaveClaimInvoiceCommand.CanExecute(null).Should().BeTrue();
        sut.SaveClaimStatementCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task SaveServiceProvisionRecordAsync_writes_generator_bytes_to_the_picked_path()
    {
        var sut = CreateSection(out var generator, out var fileSaveService);
        sut.HasFinalizedRevision = true;
        sut.SelectedRecipient = new ClaimReportRecipientOption(Kit.RecipientId, "山田太郎");

        await sut.SaveServiceProvisionRecordCommand.ExecuteAsync(null);

        fileSaveService.LastBytes.Should().Equal(generator.ServiceProvisionRecordResult);
        fileSaveService.LastSuggestedFileName.Should().Be(
            $"山田太郎_サービス提供実績記録票_{Kit.Month.Year:D4}-{Kit.Month.Month:D2}.pdf");
        sut.LastSavedFileName.Should().Be(fileSaveService.LastSuggestedFileName);
        sut.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task SaveClaimInvoiceAsync_writes_generator_bytes_and_uses_office_name_in_the_file_name()
    {
        var sut = CreateSection(out var generator, out var fileSaveService);
        sut.HasFinalizedRevision = true;
        sut.Office = new OfficeDto(
            Kit.OfficeId, "1310000001", "テスト事業所",
            ServiceCategory.TypeB, RegionGrade.Grade2, Guid.NewGuid());

        await sut.SaveClaimInvoiceCommand.ExecuteAsync(null);

        fileSaveService.LastBytes.Should().Equal(generator.ClaimInvoiceResult);
        fileSaveService.LastSuggestedFileName.Should().Be(
            $"テスト事業所_請求書_{Kit.Month.Year:D4}-{Kit.Month.Month:D2}.pdf");
        sut.LastSavedFileName.Should().Be(fileSaveService.LastSuggestedFileName);
    }

    [Fact]
    public async Task SaveClaimStatementAsync_writes_generator_bytes_to_the_picked_path()
    {
        var sut = CreateSection(out var generator, out var fileSaveService);
        sut.HasFinalizedRevision = true;

        await sut.SaveClaimStatementCommand.ExecuteAsync(null);

        fileSaveService.LastBytes.Should().Equal(generator.ClaimStatementResult);
        sut.LastSavedFileName.Should().Be(fileSaveService.LastSuggestedFileName);
    }

    [Fact]
    public async Task User_cancelling_the_save_dialog_does_not_throw_and_leaves_no_saved_file_name()
    {
        var sut = CreateSection(out _, out var fileSaveService);
        sut.HasFinalizedRevision = true;
        fileSaveService.NextResult = false; // ユーザーが保存ダイアログをキャンセルした状態を再現

        var act = () => sut.SaveClaimInvoiceCommand.ExecuteAsync(null);

        await act.Should().NotThrowAsync();
        fileSaveService.LastBytes.Should().NotBeNull(); // バイト自体は生成・引渡し済み
        sut.LastSavedFileName.Should().BeNull();
        sut.ErrorMessage.Should().BeNull();
    }

    private static ClaimReportSection CreateSection(
        out FakeClaimReportGenerator generator, out FakeFileSaveService fileSaveService)
    {
        var snapshot = BuildSnapshot(Kit.RecipientId, Kit.Month);
        var header = BuildHeader();
        var detail = BuildDetail(header.Id, snapshot);
        var repository = new FakeBatchRepository([new ClaimBatchAggregate(header, [detail])]);
        generator = new FakeClaimReportGenerator();
        fileSaveService = new FakeFileSaveService();
        var useCase = new GenerateClaimReportsUseCase(repository, generator);

        return new ClaimReportSection(useCase, fileSaveService)
        {
            OfficeId = Kit.OfficeId,
            ServiceMonth = Kit.Month,
        };
    }

    private static ClaimBatch BuildHeader() => new()
    {
        Id = Guid.NewGuid(),
        OfficeId = Kit.OfficeId,
        ServiceMonth = Kit.Month,
        Revision = 1,
        Kind = RecordKind.New,
        TotalUnits = 1400,
        TotalCostYen = 14_000,
        TotalBenefitYen = 12_600,
        TotalBurdenYen = 1_400,
        ClaimMasterVersion = "master-v1",
        CsvSpecificationVersion = "csv-spec-v1",
        ReportSpecificationVersion = "report-spec-v1",
        SnapshotApplicationVersion = "snapshot-app-v1",
        OperationApplicationVersion = "operation-app-v1",
        FinalizationOperationId = Guid.NewGuid(),
        OperationPayloadSchemaVersion = ClaimBatch.CurrentOperationPayloadSchemaVersion,
        OperationPayloadSha256 = new string('0', 64),
        CreatedAt = Kit.Now,
        CreatedBy = "tester",
        ConcurrencyToken = Guid.NewGuid(),
    };

    private static ClaimFinalizationSnapshot BuildSnapshot(Guid recipientId, ServiceMonth serviceMonth) => new(
        recipientId,
        serviceMonth,
        "master-v1",
        "csv-spec-v1",
        "report-spec-v1",
        new ClaimFinalizationOfficeSnapshot(
            "1310000001", "テスト事業所", RegionGrade.Grade2,
            "100-0001", "東京都千代田区1-1", "03-0000-0000", "施設長 テスト"),
        new ClaimFinalizationRecipientSnapshot("山田太郎", "ヤマダタロウ"),
        new ClaimFinalizationCertificateSnapshot("certificate-no-1", "131016", null, 37_200, null, null),
        new ClaimFinalizationClaimInputSnapshot(null, null, null, null, null, null, null),
        [
            new ClaimFinalizationDailyRecordSnapshot(
                new DateOnly(serviceMonth.Year, serviceMonth.Month, 1),
                Attendance.Present,
                true,
                TransportKind.Round,
                null,
                new TimeOnly(9, 0),
                new TimeOnly(16, 0),
                null,
                false,
                null,
                null,
                false,
                false,
                false,
                true),
        ],
        null,
        [new ClaimFinalizationClaimLineSnapshot(ClaimDetailLineKind.Basic, "610000", 700, 20, 14_000)],
        BilledDays: 20,
        TotalUnits: 14_000,
        TotalCostYen: 14_000,
        BenefitYen: 12_600,
        BurdenYen: 1_400);

    private static ClaimDetail BuildDetail(Guid claimBatchId, ClaimFinalizationSnapshot snapshot) =>
        ClaimDetail.Create(
            Guid.NewGuid(),
            claimBatchId,
            snapshot.RecipientId,
            snapshotSchemaVersion: ClaimSnapshotValidationCodecV2.SchemaVersionValue,
            claimMasterVersion: snapshot.ClaimMasterVersion,
            csvSpecificationVersion: snapshot.CsvSpecificationVersion,
            reportSpecificationVersion: snapshot.ReportSpecificationVersion,
            snapshotApplicationVersion: "snapshot-app-v1",
            inputSnapshotJson: "{}",
            calculationSnapshotJson: Encoding.UTF8.GetString(ClaimFinalizationSnapshotWriter.Write(snapshot)),
            totalUnits: 1400,
            totalCostYen: 14_000,
            benefitYen: 12_600,
            burdenYen: 1_400,
            createdBy: "tester",
            createdAt: Kit.Now);

    private sealed class FakeBatchRepository(IReadOnlyList<ClaimBatchAggregate> aggregates)
        : IClaimBatchRepository
    {
        public Task<IReadOnlyList<ClaimBatchAggregate>> ListHistoryAggregatesAsync(
            Guid officeId, ServiceMonth serviceMonth, CancellationToken ct) => Task.FromResult(aggregates);

        public Task<ClaimBatchAggregate?> FindByOperationIdAsync(
            Guid finalizationOperationId, CancellationToken ct) =>
            Task.FromResult<ClaimBatchAggregate?>(null);
    }

    /// <summary>渡されたDTOはTsumugi.Application.Testsの既存カバレッジで別途検証済みのため、
    /// ここでは既知バイト列を返すだけの手書きFake（配線検証が目的）。</summary>
    private sealed class FakeClaimReportGenerator : IClaimReportGenerator
    {
        public byte[] ServiceProvisionRecordResult { get; } = [1, 2, 3];
        public byte[] ClaimInvoiceResult { get; } = [4, 5, 6];
        public byte[] ClaimStatementResult { get; } = [7, 8, 9];

        public byte[] GenerateServiceProvisionRecord(ServiceProvisionRecordDto dto) => ServiceProvisionRecordResult;

        public byte[] GenerateClaimInvoice(ClaimInvoiceDto dto) => ClaimInvoiceResult;

        public byte[] GenerateClaimStatement(ClaimStatementDto dto) => ClaimStatementResult;
    }

    private sealed class FakeFileSaveService : IFileSaveService
    {
        public bool NextResult { get; set; } = true;
        public byte[]? LastBytes { get; private set; }
        public string? LastSuggestedFileName { get; private set; }

        public Task<bool> SaveAsync(
            byte[] bytes, string suggestedFileName, string fileTypeName, string extension,
            CancellationToken ct = default)
        {
            LastBytes = bytes;
            LastSuggestedFileName = suggestedFileName;
            return Task.FromResult(NextResult);
        }
    }
}
