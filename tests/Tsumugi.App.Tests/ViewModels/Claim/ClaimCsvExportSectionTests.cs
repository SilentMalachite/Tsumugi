using FluentAssertions;
using Tsumugi.App.Services;
using Tsumugi.App.ViewModels.Claim;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Application.Dtos.Claim.Csv;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.App.Tests.ViewModels.Claim;

public sealed class ClaimCsvExportSectionTests
{
    private static readonly Guid OfficeId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Generate_command_is_disabled_until_a_finalized_revision_exists()
    {
        var section = CreateSection(out _, out _);

        section.GenerateCsvCommand.CanExecute(null).Should().BeFalse();

        section.HasFinalizedRevision = true;

        section.GenerateCsvCommand.CanExecute(null).Should().BeTrue();
    }

    [Theory]
    [InlineData(1899, 8)]
    [InlineData(2201, 8)]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    public void Generate_command_is_disabled_for_an_out_of_range_processing_month(int year, int month)
    {
        var section = CreateSection(out _, out _);
        section.HasFinalizedRevision = true;

        section.ProcessingYear = year;
        section.ProcessingMonth = month;

        section.GenerateCsvCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task Generate_saves_the_bytes_and_records_the_file_name()
    {
        var section = CreateSection(out var generator, out var fileSave);
        Ready(section);

        await section.GenerateCsvAsync();

        fileSave.LastBytes.Should().NotBeNull();
        fileSave.LastFileTypeName.Should().Be("国保連請求CSV");
        fileSave.LastExtension.Should().Be(".csv");
        section.LastSavedFileName.Should().EndWith(".csv");
        section.ErrorMessage.Should().BeNull();
        generator.LastDto!.ProcessingMonth.Should().Be(new ProcessingMonth(2026, 11));
        // AC3-7: 処理対象年月はサービス提供年月と独立している。
        generator.LastDto.ServiceMonth.Should().Be(new ServiceMonth(2026, 7));
    }

    [Fact]
    public async Task Generate_does_not_record_a_file_name_when_the_user_cancels_the_dialog()
    {
        var section = CreateSection(out _, out var fileSave);
        fileSave.Result = false;
        Ready(section);

        await section.GenerateCsvAsync();

        section.LastSavedFileName.Should().BeNull();
        section.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task Generate_shows_a_fixed_message_when_no_finalized_revision_exists()
    {
        var section = CreateSection(out _, out var fileSave);
        section.HasFinalizedRevision = true;
        section.OfficeId = OfficeId;
        section.ServiceMonth = null;

        await section.GenerateCsvAsync();

        section.ErrorMessage.Should().Be("確定済みの請求がありません。先に請求を確定してください。");
        fileSave.LastBytes.Should().BeNull();
    }

    // 失敗表示は項目IDと理由と内部参照コードのみ。氏名・受給者証番号は出さない（ハード制約4）。
    [Fact]
    public async Task Generate_reports_the_field_id_and_reason_without_personal_data()
    {
        var section = CreateSection(out var generator, out var fileSave);
        generator.Failure = new ClaimCsvExportFailedException(
            "provider:J121:01:008", "NonRepresentableCharacter", "cp932", recipientReferenceCode: "REC-001");
        Ready(section);

        await section.GenerateCsvAsync();

        section.ErrorMessage.Should().Contain("provider:J121:01:008");
        section.ErrorMessage.Should().Contain("NonRepresentableCharacter");
        section.ErrorMessage.Should().Contain("REC-001");
        section.ErrorMessage.Should().NotContain("氏名");
        section.ErrorMessage.Should().NotContain("1234567890");
        fileSave.LastBytes.Should().BeNull();
        section.LastSavedFileName.Should().BeNull();
    }

    [Fact]
    public async Task Generate_reports_a_fixed_message_when_the_batch_is_not_finalized()
    {
        var section = CreateSection(out var generator, out _);
        generator.NoBatch = true;
        Ready(section);

        await section.GenerateCsvAsync();

        section.ErrorMessage.Should().Be("確定済みの請求がありません。先に請求を確定してください。");
    }

    private static void Ready(ClaimCsvExportSection section)
    {
        section.HasFinalizedRevision = true;
        section.OfficeId = OfficeId;
        section.ServiceMonth = new ServiceMonth(2026, 7);
        section.Actor = "tester";
        section.ProcessingYear = 2026;
        section.ProcessingMonth = 11;
    }

    private static ClaimCsvExportSection CreateSection(
        out RecordingGenerator generator,
        out RecordingFileSaveService fileSave)
    {
        generator = new RecordingGenerator();
        fileSave = new RecordingFileSaveService();
        var useCase = new ExportClaimCsvUseCase(
            new StubBatchRepository(generator),
            new StubOfficeContextProvider(),
            generator,
            new NoopExportRepository(),
            TimeProvider.System);
        return new ClaimCsvExportSection(useCase, fileSave);
    }

    private sealed class RecordingGenerator : IClaimCsvGenerator
    {
        public ClaimCsvDto? LastDto { get; private set; }
        public ClaimCsvExportFailedException? Failure { get; set; }
        public bool NoBatch { get; set; }

        public byte[] Generate(ClaimCsvDto dto)
        {
            LastDto = dto;
            if (Failure is not null) throw Failure;
            return [1, 2, 3];
        }
    }

    private sealed class StubBatchRepository(RecordingGenerator generator) : IClaimBatchRepository
    {
        public Task<IReadOnlyList<ClaimBatchAggregate>> ListHistoryAggregatesAsync(
            Guid officeId, ServiceMonth serviceMonth, CancellationToken ct)
        {
            if (generator.NoBatch) return Task.FromResult<IReadOnlyList<ClaimBatchAggregate>>([]);

            var header = ClaimBatch.NewRecord(
                Guid.NewGuid(), officeId, serviceMonth, 0, 0, 0, 0,
                "master-v1", "r7-10", "report-v1", "snapshot-app-v1", "operation-app-v1",
                Guid.NewGuid(), ClaimBatch.CurrentOperationPayloadSchemaVersion, new string('a', 64),
                "tester", DateTimeOffset.UnixEpoch);
            var detail = ClaimDetail.Create(
                Guid.NewGuid(), header.Id, Guid.NewGuid(), "claim-snapshot-v2",
                "master-v1", "r7-10", "report-v1", "snapshot-app-v1",
                "{}", ClaimCsvSnapshotStub.Json(serviceMonth),
                0, 0, 0, 0, "tester", DateTimeOffset.UnixEpoch);
            return Task.FromResult<IReadOnlyList<ClaimBatchAggregate>>(
                [new ClaimBatchAggregate(header, [detail])]);
        }

        public Task<ClaimBatchAggregate?> FindByOperationIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<ClaimBatchAggregate?>(null);
    }

    private sealed class StubOfficeContextProvider : IClaimCsvOfficeContextProvider
    {
        public ClaimCsvOfficeContext Resolve(RegionGrade regionGrade, ServiceMonth serviceMonth) =>
            new("06", 10_000);
    }

    private sealed class NoopExportRepository : IClaimCsvExportRepository
    {
        public Task AppendAsync(ClaimCsvExport csvExport, CancellationToken ct) => Task.CompletedTask;

        public Task<IReadOnlyList<ClaimCsvExport>> ListByBatchAsync(Guid claimBatchId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ClaimCsvExport>>([]);
    }

    private sealed class RecordingFileSaveService : IFileSaveService
    {
        public byte[]? LastBytes { get; private set; }
        public string? LastFileTypeName { get; private set; }
        public string? LastExtension { get; private set; }
        public bool Result { get; set; } = true;

        public Task<bool> SaveAsync(
            byte[] bytes, string suggestedFileName, string fileTypeName, string extension, CancellationToken ct = default)
        {
            LastBytes = bytes;
            LastFileTypeName = fileTypeName;
            LastExtension = extension;
            return Task.FromResult(Result);
        }
    }
}
