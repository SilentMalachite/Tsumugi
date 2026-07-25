using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Application.Tests.Claim;

public sealed class SetClaimInputUseCaseTests
{
    /// <summary>汎用 pass-through 入力（ADR 0042）は既定で宣言なし。宣言する場合はテスト側で差し替える。</summary>
    private static readonly IClaimGenericFieldCatalog GenericCatalog = new NoGenericFields();

    private static readonly IClaimCsvSpecificationVersions CsvVersions = new FixedCsvVersions();

    private static readonly DateTimeOffset Now = new(2026, 7, 12, 1, 2, 3, TimeSpan.Zero);
    private static readonly ServiceMonth Month = new(2026, 6);

    [Fact]
    public async Task Execute_appends_new_correction_cancel_and_reentry_as_distinct_revisions()
    {
        var repo = new FakeClaimInputRepository();
        var uow = new FakeUnitOfWork();
        var sut = new SetClaimInputUseCase(repo, uow, GenericCatalog, CsvVersions, new FixedTimeProvider(Now));
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();

        var created = await sut.ExecuteAsync(
            ValidRequest(officeId, recipientId, RecordKind.New, expectedHeadId: null),
            "operator", default);
        var corrected = await sut.ExecuteAsync(
            ValidRequest(officeId, recipientId, RecordKind.Correct, created.Id) with
            {
                UpperLimitManagedAmountYen = 1_500,
            },
            "operator", default);
        var cancelled = await sut.ExecuteAsync(
            CancelRequest(officeId, recipientId, corrected.Id),
            "operator", default);
        var reentered = await sut.ExecuteAsync(
            ValidRequest(officeId, recipientId, RecordKind.Correct, cancelled.Id) with
            {
                UpperLimitManagedAmountYen = 2_000,
            },
            "operator", default);

        repo.Items.Select(item => item.Revision).Should().Equal(1, 2, 3, 4);
        repo.Items.Select(item => item.Id).Should().OnlyHaveUniqueItems();
        repo.Items.Select(item => item.RootId).Should().OnlyContain(rootId => rootId == created.RootId);
        repo.Items[2].Kind.Should().Be(RecordKind.Cancel);
        repo.Items[2].UpperLimitManagementResult.Should().BeNull();
        repo.Items[3].ExpectedHeadId.Should().Be(cancelled.Id);
        repo.Items[3].UpperLimitManagedAmountYen.Should().Be(2_000);
        repo.Items.Should().OnlyContain(item => item.CreatedAt == Now && item.CreatedBy == "operator");
        reentered.Revision.Should().Be(4);
        uow.SaveCalls.Should().Be(4);
    }

    [Fact]
    public async Task Execute_rejects_missing_and_stale_expected_head_with_closed_errors()
    {
        var repo = new FakeClaimInputRepository();
        var sut = new SetClaimInputUseCase(repo, new FakeUnitOfWork(), GenericCatalog, CsvVersions, new FixedTimeProvider(Now));
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var created = await sut.ExecuteAsync(
            ValidRequest(officeId, recipientId, RecordKind.New, null), "operator", default);

        var missing = () => sut.ExecuteAsync(
            ValidRequest(officeId, recipientId, RecordKind.Correct, null), "operator", default);
        var stale = () => sut.ExecuteAsync(
            ValidRequest(officeId, recipientId, RecordKind.Correct, Guid.NewGuid()),
            "operator", default);

        (await missing.Should().ThrowAsync<ClaimInputSaveException>())
            .Which.Should().Match<ClaimInputSaveException>(error =>
                error.Code == ClaimInputSaveErrorCode.ExpectedHeadRequired
                && error.FieldCode == ClaimInputFieldCode.ExpectedHead);
        (await stale.Should().ThrowAsync<ClaimInputSaveException>())
            .Which.Should().Match<ClaimInputSaveException>(error =>
                error.Code == ClaimInputSaveErrorCode.ExpectedHeadMismatch
                && error.FieldCode == ClaimInputFieldCode.ExpectedHead);
        repo.Items.Should().ContainSingle(item => item.Id == created.Id);
    }

    [Fact]
    public async Task Execute_rejects_expected_head_from_another_root()
    {
        var repo = new FakeClaimInputRepository();
        var sut = new SetClaimInputUseCase(repo, new FakeUnitOfWork(), GenericCatalog, CsvVersions, new FixedTimeProvider(Now));
        var officeId = Guid.NewGuid();
        var selectedRecipientId = Guid.NewGuid();
        var selected = await sut.ExecuteAsync(
            ValidRequest(officeId, selectedRecipientId, RecordKind.New, null),
            "operator", default);
        var other = await sut.ExecuteAsync(
            ValidRequest(officeId, Guid.NewGuid(), RecordKind.New, null),
            "operator", default);

        var act = () => sut.ExecuteAsync(
            ValidRequest(officeId, selectedRecipientId, RecordKind.Correct, other.Id),
            "operator", default);

        (await act.Should().ThrowAsync<ClaimInputSaveException>())
            .Which.Code.Should().Be(ClaimInputSaveErrorCode.ExpectedHeadMismatch);
        repo.Items.Should().ContainSingle(item => item.RootId == selected.RootId);
    }

    [Fact]
    public async Task Execute_rejects_empty_identity_with_closed_field_code()
    {
        var repo = new FakeClaimInputRepository();
        var sut = new SetClaimInputUseCase(repo, new FakeUnitOfWork(), GenericCatalog, CsvVersions, new FixedTimeProvider(Now));

        var act = () => sut.ExecuteAsync(
            ValidRequest(Guid.Empty, Guid.NewGuid(), RecordKind.New, null),
            "operator", default);

        var error = (await act.Should().ThrowAsync<ClaimInputSaveException>()).Which;
        error.Code.Should().Be(ClaimInputSaveErrorCode.InvalidRequest);
        error.FieldCode.Should().Be(ClaimInputFieldCode.Identity);
        repo.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_rejects_empty_actor_without_echoing_it()
    {
        var repo = new FakeClaimInputRepository();
        var sut = new SetClaimInputUseCase(repo, new FakeUnitOfWork(), GenericCatalog, CsvVersions, new FixedTimeProvider(Now));

        var act = () => sut.ExecuteAsync(
            ValidRequest(Guid.NewGuid(), Guid.NewGuid(), RecordKind.New, null),
            " ", default);

        var error = (await act.Should().ThrowAsync<ClaimInputSaveException>()).Which;
        error.Code.Should().Be(ClaimInputSaveErrorCode.InvalidRequest);
        error.FieldCode.Should().Be(ClaimInputFieldCode.Actor);
        repo.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_rejects_corrupt_existing_history_with_sanitized_error()
    {
        var repo = new FakeClaimInputRepository();
        var sut = new SetClaimInputUseCase(repo, new FakeUnitOfWork(), GenericCatalog, CsvVersions, new FixedTimeProvider(Now));
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var created = await sut.ExecuteAsync(
            ValidRequest(officeId, recipientId, RecordKind.New, null), "operator", default);
        repo.Items[0] = repo.Items[0] with { Revision = 2 };

        var act = () => sut.ExecuteAsync(
            ValidRequest(officeId, recipientId, RecordKind.Correct, created.Id),
            "operator", default);

        var error = (await act.Should().ThrowAsync<ClaimInputSaveException>()).Which;
        error.Code.Should().Be(ClaimInputSaveErrorCode.InvalidHistory);
        error.FieldCode.Should().Be(ClaimInputFieldCode.History);
        error.Message.Should().NotContain("Revision");
        repo.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Execute_rejects_unknown_record_kind()
    {
        var repo = new FakeClaimInputRepository();
        var sut = new SetClaimInputUseCase(repo, new FakeUnitOfWork(), GenericCatalog, CsvVersions, new FixedTimeProvider(Now));
        var request = ValidRequest(
            Guid.NewGuid(), Guid.NewGuid(), (RecordKind)999, expectedHeadId: null);

        var act = () => sut.ExecuteAsync(request, "operator", default);

        var error = (await act.Should().ThrowAsync<ClaimInputSaveException>()).Which;
        error.Code.Should().Be(ClaimInputSaveErrorCode.InvalidRequest);
        error.FieldCode.Should().Be(ClaimInputFieldCode.RecordKind);
        error.Message.Should().NotContain("999");
        repo.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_rejects_replay_instead_of_reusing_the_previous_operation()
    {
        var repo = new FakeClaimInputRepository();
        var sut = new SetClaimInputUseCase(repo, new FakeUnitOfWork(), GenericCatalog, CsvVersions, new FixedTimeProvider(Now));
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var created = await sut.ExecuteAsync(
            ValidRequest(officeId, recipientId, RecordKind.New, null), "operator", default);
        var request = ValidRequest(officeId, recipientId, RecordKind.Correct, created.Id);
        await sut.ExecuteAsync(request, "operator", default);

        var replay = () => sut.ExecuteAsync(request, "operator", default);

        (await replay.Should().ThrowAsync<ClaimInputSaveException>())
            .Which.Code.Should().Be(ClaimInputSaveErrorCode.ExpectedHeadMismatch);
        repo.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Execute_rejects_cross_field_values_without_echoing_input()
    {
        var repo = new FakeClaimInputRepository();
        var sut = new SetClaimInputUseCase(repo, new FakeUnitOfWork(), GenericCatalog, CsvVersions, new FixedTimeProvider(Now));
        var request = ValidRequest(Guid.NewGuid(), Guid.NewGuid(), RecordKind.New, null) with
        {
            ExceptionalUsageEndMonth = null,
            MunicipalSubsidyAmountYen = 987_654,
        };

        var act = () => sut.ExecuteAsync(request, "operator-secret", default);

        var error = (await act.Should().ThrowAsync<ClaimInputSaveException>()).Which;
        error.Code.Should().Be(ClaimInputSaveErrorCode.InvalidValue);
        error.FieldCode.Should().Be(ClaimInputFieldCode.Values);
        error.Message.Should().NotContain("987654").And.NotContain("operator-secret");
        repo.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_round_trips_group_b_explicit_addition_inputs()
    {
        var repo = new FakeClaimInputRepository();
        var sut = new SetClaimInputUseCase(repo, new FakeUnitOfWork(), GenericCatalog, CsvVersions, new FixedTimeProvider(Now));
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();

        var created = await sut.ExecuteAsync(
            ValidRequest(officeId, recipientId, RecordKind.New, null) with
            {
                SpecialVisitSupportBilledCount = 2,
                OffsiteSupportCumulativeDays = 12,
            },
            "operator", default);

        var saved = repo.Items.Should().ContainSingle().Subject;
        saved.Id.Should().Be(created.Id);
        saved.SpecialVisitSupportBilledCount.Should().Be(2);
        saved.OffsiteSupportCumulativeDays.Should().Be(12);
    }

    [Fact]
    public async Task Execute_keeps_group_b_explicit_addition_inputs_null_when_not_submitted()
    {
        var repo = new FakeClaimInputRepository();
        var sut = new SetClaimInputUseCase(repo, new FakeUnitOfWork(), GenericCatalog, CsvVersions, new FixedTimeProvider(Now));

        await sut.ExecuteAsync(
            ValidRequest(Guid.NewGuid(), Guid.NewGuid(), RecordKind.New, null),
            "operator", default);

        var saved = repo.Items.Should().ContainSingle().Subject;
        saved.SpecialVisitSupportBilledCount.Should().BeNull();
        saved.OffsiteSupportCumulativeDays.Should().BeNull();
    }

    /// <summary>
    /// 負値は Domain の <c>ClaimInputPolicy</c> でも弾かれるが、UI 入力は検証エラー
    /// （<see cref="ClaimInputSaveErrorCode.InvalidValue"/>）として返すのが既存の作法。
    /// 制度上の上限（月内算定回数・累計日数の限度）はコードに持たないため検証しない。
    /// </summary>
    [Theory]
    [InlineData(-1, null)]
    [InlineData(null, -1)]
    public async Task Execute_rejects_negative_group_b_explicit_addition_inputs(
        int? billedCount, int? cumulativeDays)
    {
        var repo = new FakeClaimInputRepository();
        var sut = new SetClaimInputUseCase(repo, new FakeUnitOfWork(), GenericCatalog, CsvVersions, new FixedTimeProvider(Now));
        var request = ValidRequest(Guid.NewGuid(), Guid.NewGuid(), RecordKind.New, null) with
        {
            SpecialVisitSupportBilledCount = billedCount,
            OffsiteSupportCumulativeDays = cumulativeDays,
        };

        var act = () => sut.ExecuteAsync(request, "operator", default);

        var error = (await act.Should().ThrowAsync<ClaimInputSaveException>()).Which;
        error.Code.Should().Be(ClaimInputSaveErrorCode.InvalidValue);
        error.FieldCode.Should().Be(ClaimInputFieldCode.Values);
        error.Message.Should().NotContain("-1");
        repo.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_rejects_cancel_carrying_group_b_explicit_addition_inputs()
    {
        var repo = new FakeClaimInputRepository();
        var sut = new SetClaimInputUseCase(repo, new FakeUnitOfWork(), GenericCatalog, CsvVersions, new FixedTimeProvider(Now));
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var created = await sut.ExecuteAsync(
            ValidRequest(officeId, recipientId, RecordKind.New, null), "operator", default);

        var act = () => sut.ExecuteAsync(
            CancelRequest(officeId, recipientId, created.Id) with
            {
                SpecialVisitSupportBilledCount = 1,
            },
            "operator", default);

        (await act.Should().ThrowAsync<ClaimInputSaveException>())
            .Which.Code.Should().Be(ClaimInputSaveErrorCode.InvalidValue);
        repo.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Intensive_support_episode_allows_cancelled_history_to_be_reentered()
    {
        var repo = new FakeIntensiveSupportEpisodeRepository();
        var uow = new FakeUnitOfWork();
        var sut = new SetIntensiveSupportEpisodeUseCase(
            repo, uow, new FixedTimeProvider(Now));
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();

        var created = await sut.ExecuteAsync(
            new SetIntensiveSupportEpisodeRequest(
                officeId, recipientId, RecordKind.New, null, new DateOnly(2026, 6, 1)),
            "operator", default);
        var cancelled = await sut.ExecuteAsync(
            new SetIntensiveSupportEpisodeRequest(
                officeId, recipientId, RecordKind.Cancel, created.Id, null),
            "operator", default);
        var reentered = await sut.ExecuteAsync(
            new SetIntensiveSupportEpisodeRequest(
                officeId, recipientId, RecordKind.Correct, cancelled.Id, new DateOnly(2026, 7, 1)),
            "operator", default);

        repo.Items.Select(item => item.Kind).Should()
            .Equal(RecordKind.New, RecordKind.Cancel, RecordKind.Correct);
        reentered.Revision.Should().Be(3);
        repo.Items[^1].StartDate.Should().Be(new DateOnly(2026, 7, 1));
        uow.SaveCalls.Should().Be(3);
    }

    private static SetClaimInputRequest ValidRequest(
        Guid officeId,
        Guid recipientId,
        RecordKind kind,
        Guid? expectedHeadId) =>
        new(officeId, recipientId, Month, kind, expectedHeadId)
        {
            UpperLimitManagementResult = UpperLimitManagementResult.Result2,
            UpperLimitManagedAmountYen = 1_000,
            MunicipalSubsidyAmountYen = 500,
            ExceptionalUsageStartMonth = Month,
            ExceptionalUsageEndMonth = Month,
            ExceptionalUsageDays = 10,
            StandardUsageDayTotal = 22,
        };

    private static SetClaimInputRequest CancelRequest(
        Guid officeId,
        Guid recipientId,
        Guid expectedHeadId) =>
        new(officeId, recipientId, Month, RecordKind.Cancel, expectedHeadId);

    private sealed class FakeClaimInputRepository : IClaimInputRepository
    {
        public List<ClaimInput> Items { get; } = [];

        public Task AddAsync(ClaimInput input, CancellationToken ct)
        {
            Items.Add(input);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ClaimInput>> ListHistoryAsync(
            Guid officeId,
            Guid recipientId,
            ServiceMonth serviceMonth,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ClaimInput>>(Items
                .Where(item => item.OfficeId == officeId
                               && item.RecipientId == recipientId
                               && item.ServiceMonth == serviceMonth)
                .ToArray());
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken ct)
        {
            SaveCalls++;
            return Task.FromResult(1);
        }
    }

    private sealed class FakeIntensiveSupportEpisodeRepository
        : IIntensiveSupportEpisodeRepository
    {
        public List<IntensiveSupportEpisode> Items { get; } = [];

        public Task AddAsync(IntensiveSupportEpisode episode, CancellationToken ct)
        {
            Items.Add(episode);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<IntensiveSupportEpisode>> ListHistoryAsync(
            Guid officeId,
            Guid recipientId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<IntensiveSupportEpisode>>(Items
                .Where(item => item.OfficeId == officeId && item.RecipientId == recipientId)
                .ToArray());
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
    // NOTE(teeth): 汎用 pass-through 入力（ADR 0042）。宣言済みの名前だけ保存し、未宣言は拒否、
    // 型・桁数の判定は仕様を所有する層（catalog）に委ねる。
    [Fact]
    public async Task Execute_stores_declared_generic_values()
    {
        var repo = new FakeClaimInputRepository();
        var sut = new SetClaimInputUseCase(
            repo, new FakeUnitOfWork(), new DeclaringGenericFields(), new FixedCsvVersions(),
            new FixedTimeProvider(Now));

        await sut.ExecuteAsync(
            new SetClaimInputRequest(
                Guid.NewGuid(), Guid.NewGuid(), new ServiceMonth(2026, 7), RecordKind.New, null)
            {
                GenericValues = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["DemoDays"] = " 12 ",
                    ["Untouched"] = "   ",
                },
            },
            "tester",
            CancellationToken.None);

        var stored = repo.Items.Should().ContainSingle().Subject;
        var value = stored.GenericValues.Should().ContainSingle().Subject;
        value.Name.Should().Be("DemoDays");
        value.Value.Should().Be("12", "前後の空白は落とす");
        value.ClaimInputId.Should().Be(stored.Id);
    }

    [Fact]
    public async Task Execute_rejects_an_undeclared_generic_value()
    {
        var sut = new SetClaimInputUseCase(
            new FakeClaimInputRepository(), new FakeUnitOfWork(), new DeclaringGenericFields(), new FixedCsvVersions(),
            new FixedTimeProvider(Now));

        var act = () => sut.ExecuteAsync(
            new SetClaimInputRequest(
                Guid.NewGuid(), Guid.NewGuid(), new ServiceMonth(2026, 7), RecordKind.New, null)
            {
                GenericValues = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["NotDeclared"] = "1",
                },
            },
            "tester",
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*宣言されていません*");
    }

    [Fact]
    public async Task Execute_rejects_generic_values_on_a_cancel_record()
    {
        var repo = new FakeClaimInputRepository();
        var head = await new SetClaimInputUseCase(
                repo, new FakeUnitOfWork(), new DeclaringGenericFields(), new FixedCsvVersions(),
                new FixedTimeProvider(Now))
            .ExecuteAsync(
                new SetClaimInputRequest(
                    Guid.NewGuid(), Guid.NewGuid(), new ServiceMonth(2026, 7), RecordKind.New, null),
                "tester",
                CancellationToken.None);

        var act = () => new SetClaimInputUseCase(
                repo, new FakeUnitOfWork(), new DeclaringGenericFields(), new FixedCsvVersions(),
                new FixedTimeProvider(Now))
            .ExecuteAsync(
                new SetClaimInputRequest(
                    repo.Items[0].OfficeId,
                    repo.Items[0].RecipientId,
                    new ServiceMonth(2026, 7),
                    RecordKind.Cancel,
                    head.Id)
                {
                    GenericValues = new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        ["DemoDays"] = "1",
                    },
                },
                "tester",
                CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>1 項目だけ宣言し、検証は名前の存在だけを見るフェイク。</summary>
    private sealed class DeclaringGenericFields : IClaimGenericFieldCatalog
    {
        public IReadOnlyList<ClaimGenericFieldDeclaration> GetDeclarations(string specificationVersion) =>
            [new("DemoDays", "provider:J121:04:009", "実証用", "実証用", "numeric", 2, "ClaimInputView")];

        public void ValidateValue(string specificationVersion, string name, string value)
        {
            if (GetDeclarations(specificationVersion).All(declaration => declaration.Name != name))
            {
                throw new InvalidOperationException($"汎用請求入力 '{name}' は宣言されていません。");
            }
        }
    }

    /// <summary>汎用 pass-through 入力（ADR 0042）の宣言フェイク。既定は宣言なし。</summary>
    private sealed class NoGenericFields : IClaimGenericFieldCatalog
    {
        /// <summary>宣言が無いので検証も行わない（宣言済みの検証は Infrastructure.Csv 側のテスト）。</summary>
        public void ValidateValue(string specificationVersion, string name, string value)
        {
        }

        public IReadOnlyList<ClaimGenericFieldDeclaration> GetDeclarations(string specificationVersion) => [];
    }

    private sealed class FixedCsvVersions : IClaimCsvSpecificationVersions
    {
        public string Current => "r7-10";

        public IReadOnlyList<string> UpcomingVersions => [];

        public string ResolveForProcessingMonth(ProcessingMonth processingMonth) => Current;
    }


}
