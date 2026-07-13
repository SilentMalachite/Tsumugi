# Phase 3-1 Claim Calculation and Input Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Phase 3-0のfail-closed請求土台へ、公式出典に基づく請求入力、令和6・令和8の版付き算定、プレビュー・差分、validated snapshot、append-only確定を接続し、AC3-1 / 2 / 3 / 4 / 8 / 9を一括受け入れ可能にする。

**Architecture:** 依存順5スライス（入力契約 → 制度マスタ → 純粋算定 → 一貫snapshot・プレビュー → validated finalization）で実装する。Applicationの`IClaimPreparationSnapshotReader`がoperation-local SQLite read transactionからimmutable入力を返し、Domainの純粋算定、canonical hash、production codecを経て既存`ClaimFinalizationStore`だけがrevision採番と永続化を行う。

**Tech Stack:** .NET 10、C# 14、EF Core 10 / SQLite、Avalonia 11、CommunityToolkit.Mvvm、xUnit、FluentAssertions、coverlet、JSON Schema、Roslyn source guards

---

## 実装進捗 (2026-07-12 / `d812e31`)

- 実装済み: Tasks 2〜10（Certificate revision、Office / ContractedProvider / DailyRecord請求入力、月次入力・算定根拠履歴、永続化、保存ユースケース、typed requirements、readiness gate）。
- 次の実装対象: Task 11（typed navigationと入力UI）。
- Phase 3-1全体は未受け入れ。Tasks 12〜28の制度実値、純粋算定、snapshot、validated finalization、UI配線、品質ゲート、受け入れ証跡が残る。
- 以下の詳細checkboxはRed / Green / commitを含む当初の実行契約を保存するため書き換えず、現況は本節を正本とする。

## 実行契約

- 設計正本: `docs/superpowers/specs/2026-07-11-phase3-1-claim-calculation-and-input-foundation-design.md`
- 上位契約: `06_ClaudeCode_Phase3実装指示_国保連請求_Tsumugi.md`
- 制度・履歴契約: `docs/decisions/0020-claim-master-sources-and-versioning.md`〜`0026-claim-batch-snapshot.md`
- 項目正本: `docs/phase3-claim-field-mapping.md`の51 entries / 26 targets
- 2026-06-29付けの旧`docs/superpowers/plans/2026-06-29-phase3-1-calculation-engine.md`は実行しない。
- 各Taskは`@superpowers:test-driven-development`でRed → Green → Refactorを守る。
- 各Task完了前に`@superpowers:verification-before-completion`を使う。
- Task 28で`@superpowers:requesting-code-review`を使い、Phase 3-1全範囲をレビューする。
- `dotnet test`は共有PDB競合を避けて逐次実行する。
- 制度値を推測しない。一次資料、SHA、sheet / row / cell又は物理頁が一致しなければそのTaskを停止し、`docs/open-questions.md`へ記録する。
- 利用者作業の`.serena/project.yml`、`graphify-out/`、`docs/superpowers/plans/2026-07-11-phase3-0-task16-acceptance-closeout.md`をstageしない。

## ファイル構成

### Slice 1: 入力契約

**Domain — Create**

- `src/Tsumugi.Domain/Entities/ClaimInput.cs` — 月次請求固有入力のappend-only revision。
- `src/Tsumugi.Domain/Entities/IntensiveSupportEpisode.cs` — 集中的支援開始日のappend-only入力。
- `src/Tsumugi.Domain/Entities/AverageWageAnnualEvidence.cs` — 前年度平均工賃根拠。
- `src/Tsumugi.Domain/Entities/OfficeClaimProfile.cs` — 版付き体制option・R8移行根拠。
- `src/Tsumugi.Domain/Entities/CertificateClaimEvidence.cs` — 証上限・法31条の原本確認根拠。
- `src/Tsumugi.Domain/Entities/UpperLimitManagementStatement.cs` — 正式管理結果票header。
- `src/Tsumugi.Domain/Entities/UpperLimitManagementStatementLine.cs` — 正式管理結果票の事業所行。
- `src/Tsumugi.Domain/Logic/Claim/CertificatePolicy.cs` — Certificate revision chainと請求日の一意解決。
- `src/Tsumugi.Domain/Logic/Claim/ClaimInputPolicy.cs` — ClaimInput revision履歴。
- `src/Tsumugi.Domain/Logic/Claim/IntensiveSupportEpisodePolicy.cs` — episode履歴。
- `src/Tsumugi.Domain/Logic/Claim/Models/ClaimInputModels.cs` — coded inputs、entered-state、三値状態。

**Domain — Modify**

- `src/Tsumugi.Domain/Entities/Certificate.cs`
- `src/Tsumugi.Domain/Entities/ContractedProvider.cs`
- `src/Tsumugi.Domain/Entities/DailyRecord.cs`
- `src/Tsumugi.Domain/Entities/Office.cs`

**Application — Create**

- `src/Tsumugi.Application/Abstractions/IClaimInputRepositories.cs`
- `src/Tsumugi.Application/Abstractions/IClaimInputRequirementProvider.cs`
- `src/Tsumugi.Application/Claim/ClaimPreparationReadiness.cs`
- `src/Tsumugi.Application/Claim/ClaimPreparationContracts.cs`
- `src/Tsumugi.Application/UseCases/Certificate/CorrectCertificateUseCase.cs`
- `src/Tsumugi.Application/UseCases/Claim/SetClaimInputUseCase.cs`
- `src/Tsumugi.Application/UseCases/Claim/SetClaimEvidenceUseCases.cs`

**Application — Modify**

- `src/Tsumugi.Application/UseCases/Certificate/RegisterCertificateUseCase.cs`
- `src/Tsumugi.Application/UseCases/Certificate/RegisterContractedProviderUseCase.cs`
- `src/Tsumugi.Application/UseCases/DailyRecord/RecordDailyRecordUseCase.cs`
- `src/Tsumugi.Application/UseCases/DailyRecord/CorrectDailyRecordUseCase.cs`
- `src/Tsumugi.Application/UseCases/Office/UpdateOfficeUseCase.cs`
- `src/Tsumugi.Application/Dtos/CertificateDto.cs`
- `src/Tsumugi.Application/Dtos/ContractedProviderDto.cs`
- `src/Tsumugi.Application/Dtos/DailyRecordDto.cs`
- `src/Tsumugi.Application/Dtos/OfficeDto.cs`

**Infrastructure — Create / Modify**

- Create: `ClaimInputConfiguration.cs`、`IntensiveSupportEpisodeConfiguration.cs`、`AverageWageAnnualEvidenceConfiguration.cs`、`OfficeClaimProfileConfiguration.cs`、`CertificateClaimEvidenceConfiguration.cs`、`UpperLimitManagementStatementConfiguration.cs`、`UpperLimitManagementStatementLineConfiguration.cs` under `src/Tsumugi.Infrastructure/Persistence/Configurations/`.
- Create: `ClaimInputRepository.cs`、`IntensiveSupportEpisodeRepository.cs`、`AverageWageAnnualEvidenceRepository.cs`、`OfficeClaimProfileRepository.cs`、`CertificateClaimEvidenceRepository.cs`、`UpperLimitManagementStatementRepository.cs` under `src/Tsumugi.Infrastructure/Persistence/`.
- Modify `src/Tsumugi.Infrastructure/Persistence/TsumugiDbContext.cs`、`AppendOnlyGuard.cs`、`CertificateRepository.cs`。
- Create generated migration `src/Tsumugi.Infrastructure/Migrations/<timestamp>_Phase31ClaimInputFoundation.cs` and designer; modify `TsumugiDbContextModelSnapshot.cs`.

### Slice 2: 制度マスタ

- Modify `src/Tsumugi.Infrastructure/ClaimMasters/Schema/claim-master-file.schema.json`。
- Modify all five empty seed files: `basic-rewards.json`、`additions.json`、`region-unit-prices.json`、`burden-caps.json`、`transition-rules.json` and `service-codes.json` for the R6 / R8 B型 service-code rows referenced by those masters.
- Modify `src/Tsumugi.Infrastructure/ClaimMasters/ClaimMasterFileValidator.cs`、`JsonClaimMasterProvider.cs`。
- Create typed master models in `src/Tsumugi.Domain/Logic/Claim/Models/ClaimCalculationMasters.cs`。
- Create `docs/phase3-1-master-transcription-review.md` for independent source-locator sign-off.

### Slice 3: 純粋算定

- Create focused calculators under `src/Tsumugi.Domain/Logic/Claim/`: `AverageWageCalculator.cs`、`PaymentBandResolver.cs`、`ServiceCodeResolver.cs`、`PercentageAdjustmentCalculator.cs`、`ClaimRoundingPolicy.cs`、`RegionalCostCalculator.cs`、`Article31SpecialBurdenPolicy.cs`、`BurdenCalculator.cs`、`ClaimCalculator.cs`。
- Create calculation records under `src/Tsumugi.Domain/Logic/Claim/Models/`。

### Slice 4: snapshot・プレビュー

- Create `src/Tsumugi.Application/Abstractions/IClaimPreparationSnapshotReader.cs`。
- Create `src/Tsumugi.Infrastructure/Persistence/ClaimPreparationSnapshotReader.cs`。
- Create `src/Tsumugi.Application/UseCases/Claim/CalculateClaimUseCase.cs`、`QueryClaimUseCase.cs`。
- Create `src/Tsumugi.Application/Claim/ClaimSnapshotCanonicalizer.cs`、`ClaimDifferencePolicy.cs`。

### Slice 5: 確定・UI・ゲート

- Create `src/Tsumugi.Application/Claim/ClaimSnapshotValidationCodecV1.cs` and production registry.
- Create `src/Tsumugi.Application/UseCases/Claim/CloseClaimUseCase.cs`、`CancelClaimUseCase.cs`。
- Create App navigation、`ClaimInputViewModel` / View、`ClaimPreparationViewModel` / View。
- Modify `src/Tsumugi.Infrastructure/DependencyInjection.cs`、`src/Tsumugi.App/CompositionRoot.cs`、`MainViewModel.cs`、`MainWindow.axaml`。
- Modify `build/ci.sh` and `build/ci.ps1` for Application 90% gate.
- Create `docs/phase3-1-acceptance.md`; modify `docs/open-questions.md` and `CHANGELOG.md` only after all gates pass.

---

### Task 1: ベースラインと一次資料停止条件を固定する

**Files:**
- Read: `docs/superpowers/specs/2026-07-11-phase3-1-claim-calculation-and-input-foundation-design.md`
- Read: `docs/decisions/0020-claim-master-sources-and-versioning.md`
- Read: `docs/decisions/0022-burden-cap-master.md`
- Read: `docs/decisions/0023-average-wage-and-r8-transition.md`
- Read: `docs/decisions/0025-claim-rounding-rules.md`

- [ ] **Step 1: 専用worktreeを作る**

`@superpowers:using-git-worktrees`を使い、`codex/phase3-1-claim-calculation`ブランチの専用worktreeを作る。既存利用者変更を持ち込まない。

- [ ] **Step 2: 基準commitとdirty stateを記録する**

Run:

```bash
git status --short --branch
git rev-parse HEAD
git log -5 --oneline
```

Expected: HEADに設計commit `04382f0`を含み、実装worktreeはclean。

- [ ] **Step 3: 現行品質ゲートを逐次実行する**

Run:

```bash
dotnet format --verify-no-changes
./build/ci.sh
dotnet list package --vulnerable --include-transitive
```

Expected: format / CIはexit 0。脆弱性監査は既知抑制`GHSA-2m69-gcr7-jv3q`以外の未抑制advisory 0。

- [ ] **Step 4: source catalogのSHAとlive locatorを再確認する**

ADR 0020のR6/R8 source IDs、SHA、sheet / row / physical pageを再取得物と照合する。差替え、404、SHA不一致が1件でもあれば実装を停止し、`docs/open-questions.md`へ事実だけを記録する。

- [ ] **Step 5: baselineをcommitしない**

Expected: Task 1はread-only。生成された`TestResults/`を削除し、source change 0を確認する。

---

### Task 2: Certificateを決定的なappend-only revisionへ拡張する

**Files:**
- Modify: `src/Tsumugi.Domain/Entities/Certificate.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/CertificatePolicy.cs`
- Modify: `src/Tsumugi.Application/UseCases/Certificate/RegisterCertificateUseCase.cs`
- Create: `src/Tsumugi.Application/UseCases/Certificate/CorrectCertificateUseCase.cs`
- Modify: `src/Tsumugi.Application/Dtos/CertificateDto.cs`
- Modify: `src/Tsumugi.Application/Abstractions/ICertificateRepository.cs`
- Modify: `src/Tsumugi.Infrastructure/Persistence/CertificateRepository.cs`
- Modify: `src/Tsumugi.Infrastructure/Persistence/Configurations/CertificateConfiguration.cs`
- Test: `tests/Tsumugi.Domain.Tests/Logic/Claim/CertificatePolicyTests.cs`
- Test: `tests/Tsumugi.Application.Tests/Claim/CorrectCertificateUseCaseTests.cs`

- [ ] **Step 1: revision chainの失敗テストを書く**

```csharp
[Fact]
public void EffectiveVersion_rejects_branching_heads()
{
    var root = CertificateTestData.Root(revision: 1);
    var a = CertificateTestData.Correction(root, revision: 2);
    var b = CertificateTestData.Correction(root, revision: 2);

    var act = () => CertificatePolicy.EffectiveVersion([root, a, b], ServiceDate);

    act.Should().Throw<InvalidOperationException>();
}
```

- [ ] **Step 2: Domain testが赤になることを確認する**

Run: `dotnet test tests/Tsumugi.Domain.Tests --filter FullyQualifiedName~CertificatePolicyTests -v normal`

Expected: FAIL because `CertificatePolicy.EffectiveVersion` and lineage properties do not exist.

- [ ] **Step 3: lineageと3つのAC3-8項目を追加する**

`Certificate`へ次を追加する。

```csharp
public required Guid RootCertificateId { get; init; }
public required int Revision { get; init; }
public Guid? ExpectedHeadCertificateId { get; init; }
public string? MunicipalityNumber { get; init; }
public string? SubsidyMunicipalityNumber { get; init; }
public string? UpperLimitManagementProviderNumber { get; init; }
```

rootは`RootCertificateId = Id`、`Revision = 1`、expected headなし。Correctionはrootを維持し、expected headの次revisionだけを許可する。

- [ ] **Step 4: Policyを最小実装する**

`EffectiveVersion`はrootごとにrevision 1開始、連番、expected head直結、分岐なしを検証する。請求日に複数rootが有効なら選択せず例外にする。

- [ ] **Step 5: Domain testを通す**

Run: `dotnet test tests/Tsumugi.Domain.Tests --filter 'FullyQualifiedName~CertificatePolicyTests|FullyQualifiedName~CertificateTests' -v normal`

Expected: PASS。

- [ ] **Step 6: Correct use caseの失敗テストを書く**

stale expected head、別root、空自治体番号、6桁以外、同じheadからの2訂正を拒否するテストを追加する。

- [ ] **Step 7: Correct use case testが赤になることを確認する**

Run: `dotnet test tests/Tsumugi.Application.Tests --filter FullyQualifiedName~CorrectCertificateUseCaseTests -v normal`

Expected: FAIL because `CorrectCertificateUseCase` and repository head lookup do not exist.

- [ ] **Step 8: repositoryとuse caseを実装する**

`CorrectCertificateUseCase`は選択rootのheadを再読込し、全既存値を複製した新revisionを`AddAsync`する。既存行を`Update`しない。

- [ ] **Step 9: Application testを通す**

Run: `dotnet test tests/Tsumugi.Application.Tests --filter FullyQualifiedName~Certificate -v normal`

Expected: PASS。

- [ ] **Step 10: commitする**

```bash
git add src/Tsumugi.Domain/Entities/Certificate.cs \
  src/Tsumugi.Domain/Logic/Claim/CertificatePolicy.cs \
  src/Tsumugi.Application/UseCases/Certificate \
  src/Tsumugi.Application/Dtos/CertificateDto.cs \
  src/Tsumugi.Application/Abstractions/ICertificateRepository.cs \
  src/Tsumugi.Infrastructure/Persistence/CertificateRepository.cs \
  src/Tsumugi.Infrastructure/Persistence/Configurations/CertificateConfiguration.cs \
  tests/Tsumugi.Domain.Tests/Logic/Claim/CertificatePolicyTests.cs \
  tests/Tsumugi.Application.Tests/Claim/CorrectCertificateUseCaseTests.cs
git commit -m "feat(phase3-1/AC3-8): add certificate revision inputs"
```

---

### Task 3: OfficeとContractedProviderの不足項目を追加する

**Files:**
- Modify: `src/Tsumugi.Domain/Entities/Office.cs`
- Modify: `src/Tsumugi.Domain/Entities/ContractedProvider.cs`
- Modify: `src/Tsumugi.Application/UseCases/Office/UpdateOfficeUseCase.cs`
- Modify: `src/Tsumugi.Application/UseCases/Certificate/RegisterContractedProviderUseCase.cs`
- Create: `src/Tsumugi.Application/UseCases/Certificate/UpdateContractedProviderUseCase.cs`
- Modify: `src/Tsumugi.Application/Dtos/OfficeDto.cs`
- Modify: `src/Tsumugi.Application/Dtos/ContractedProviderDto.cs`
- Modify: `src/Tsumugi.Application/Abstractions/IOfficeRepository.cs`
- Modify: `src/Tsumugi.Application/Abstractions/IContractedProviderRepository.cs`
- Modify: `src/Tsumugi.Infrastructure/Persistence/OfficeRepository.cs`
- Modify: `src/Tsumugi.Infrastructure/Persistence/ContractedProviderRepository.cs`
- Modify: `src/Tsumugi.Infrastructure/Persistence/Configurations/OfficeConfiguration.cs`
- Modify: `src/Tsumugi.Infrastructure/Persistence/Configurations/ContractedProviderConfiguration.cs`
- Test: `tests/Tsumugi.Application.Tests/Claim/ClaimMasterInputUseCaseTests.cs`
- Test: `tests/Tsumugi.Infrastructure.Tests/Phase31ClaimInputRoundTripTests.cs`

- [ ] **Step 1: validation testを追加する**

Officeの郵便番号・住所・電話・代表者職氏名と、ContractedProviderの事業者記入欄番号について、未入力、空白、桁超過、stale concurrency tokenを検証する。

- [ ] **Step 2: testが赤になることを確認する**

Run: `dotnet test tests/Tsumugi.Application.Tests --filter FullyQualifiedName~ClaimMasterInputUseCaseTests -v normal`

Expected: FAIL because new inputs and update use case do not exist.

- [ ] **Step 3: entitiesとDTOを拡張する**

```csharp
// Office
public string? PostalCode { get; init; }
public string? Address { get; init; }
public string? PhoneNumber { get; init; }
public string? RepresentativeTitleAndName { get; init; }

// ContractedProvider
public int? CertificateEntryNumber { get; init; }
```

- [ ] **Step 4: optimistic updateを実装する**

既存`ConcurrencyToken`を必須にし、OfficeとContractedProviderを同一性マスタとして更新する。0を未入力扱いにせず、entry numberは入力済みなら公式桁範囲を検証する。

- [ ] **Step 5: testsを通す**

Run:

```bash
dotnet test tests/Tsumugi.Application.Tests --filter FullyQualifiedName~ClaimMasterInputUseCaseTests -v normal
dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~Phase31ClaimInputRoundTripTests -v normal
```

Expected: PASS。

- [ ] **Step 6: commitする**

```bash
git add src/Tsumugi.Domain/Entities/Office.cs src/Tsumugi.Domain/Entities/ContractedProvider.cs \
  src/Tsumugi.Application src/Tsumugi.Infrastructure/Persistence \
  tests/Tsumugi.Application.Tests/Claim/ClaimMasterInputUseCaseTests.cs \
  tests/Tsumugi.Infrastructure.Tests/Phase31ClaimInputRoundTripTests.cs
git commit -m "feat(phase3-1/AC3-8): add office and provider claim inputs"
```

---

### Task 4: DailyRecordの請求入力を既存訂正履歴へ載せる

**Files:**
- Modify: `src/Tsumugi.Domain/Entities/DailyRecord.cs`
- Modify: `src/Tsumugi.Application/UseCases/DailyRecord/RecordDailyRecordUseCase.cs`
- Modify: `src/Tsumugi.Application/UseCases/DailyRecord/CorrectDailyRecordUseCase.cs`
- Modify: `src/Tsumugi.Application/Dtos/DailyRecordDto.cs`
- Modify: `src/Tsumugi.Infrastructure/Persistence/Configurations/DailyRecordConfiguration.cs`
- Test: `tests/Tsumugi.Domain.Tests/DailyRecordTests.cs`
- Test: `tests/Tsumugi.Application.Tests/DailyRecordUseCaseTests.cs`

- [ ] **Step 1: 明示未入力とfalse / 0を区別するtestを書く**

```csharp
[Fact]
public void Correction_preserves_explicit_false_and_zero()
{
    var corrected = DailyRecord.Correction(
        Guid.NewGuid(), RecipientId, new DateOnly(2026, 6, 1), RootId,
        Attendance.Present, TransportKind.None, mealProvided: false,
        note: null, createdBy: "tester", createdAt: Now,
        offsiteSupportApplied: false,
        specialVisitSupportMinutes: 0);

    corrected.OffsiteSupportApplied.Should().BeFalse();
    corrected.SpecialVisitSupportMinutes.Should().Be(0);
}
```

nullable未入力、時刻逆転、負分数、unknown enum、Cancellationで請求入力を持たないこともtestする。

- [ ] **Step 2: testが赤になることを確認する**

Run: `dotnet test tests/Tsumugi.Domain.Tests --filter FullyQualifiedName~DailyRecordTests -v normal`

Expected: FAIL because the ten fields do not exist.

- [ ] **Step 3: 10項目とclosed enumを実装する**

```csharp
public TimeOnly? ServiceStartTime { get; init; }
public TimeOnly? ServiceEndTime { get; init; }
public int? SpecialVisitSupportMinutes { get; init; }
public bool? OffsiteSupportApplied { get; init; }
public MedicalCoordinationType MedicalCoordinationType { get; init; }
public TrialUseSupportType TrialUseSupportType { get; init; }
public bool? RegionalCollaborationApplied { get; init; }
public bool? IntensiveSupportApplied { get; init; }
public bool? EmergencyAdmissionApplied { get; init; }
public RecipientConfirmationStatus RecipientConfirmation { get; init; }
```

各enumは`Unspecified = 0`を持ち、公式code変換は後続master resolverだけが行う。

- [ ] **Step 4: use caseとDTOを拡張する**

Correctionは現在の実効値を暗黙コピーせず、画面が送った全フィールドを新revisionへ保存する。Cancellationはclaim fieldsをnull / `Unspecified`へ正規化する。

- [ ] **Step 5: Domain / Application testsを通す**

Run:

```bash
dotnet test tests/Tsumugi.Domain.Tests --filter 'FullyQualifiedName~DailyRecordTests|FullyQualifiedName~DailyRecordPolicyTests' -v normal
dotnet test tests/Tsumugi.Application.Tests --filter FullyQualifiedName~DailyRecordUseCaseTests -v normal
```

Expected: PASS。

- [ ] **Step 6: commitする**

```bash
git add src/Tsumugi.Domain/Entities/DailyRecord.cs src/Tsumugi.Domain/Enums \
  src/Tsumugi.Application/UseCases/DailyRecord src/Tsumugi.Application/Dtos/DailyRecordDto.cs \
  src/Tsumugi.Infrastructure/Persistence/Configurations/DailyRecordConfiguration.cs \
  tests/Tsumugi.Domain.Tests/DailyRecordTests.cs tests/Tsumugi.Application.Tests/DailyRecordUseCaseTests.cs
git commit -m "feat(phase3-1/AC3-8): add daily claim inputs"
```

---

### Task 5: ClaimInputとIntensiveSupportEpisodeのrevision policyを実装する

**Files:**
- Create: `src/Tsumugi.Domain/Entities/ClaimInput.cs`
- Create: `src/Tsumugi.Domain/Entities/IntensiveSupportEpisode.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/ClaimInputPolicy.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/IntensiveSupportEpisodePolicy.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/Models/ClaimInputModels.cs`
- Test: `tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimInputPolicyTests.cs`
- Test: `tests/Tsumugi.Domain.Tests/Logic/Claim/IntensiveSupportEpisodePolicyTests.cs`

- [ ] **Step 1: 履歴testを先に書く**

New → Correction → Cancel → Correction（再入力）を許可し、revision欠落、分岐、root不一致、expected head不一致、別office / recipient / month混在を拒否するtestを追加する。

- [ ] **Step 2: testが赤になることを確認する**

Run: `dotnet test tests/Tsumugi.Domain.Tests --filter 'FullyQualifiedName~ClaimInputPolicyTests|FullyQualifiedName~IntensiveSupportEpisodePolicyTests' -v normal`

Expected: FAIL because entities and policies do not exist.

- [ ] **Step 3: ClaimInputを実装する**

```csharp
public sealed record ClaimInput : Entity
{
    public required Guid OfficeId { get; init; }
    public required Guid RecipientId { get; init; }
    public required ServiceMonth ServiceMonth { get; init; }
    public required Guid RootId { get; init; }
    public required int Revision { get; init; }
    public required RecordKind Kind { get; init; }
    public Guid? ExpectedHeadId { get; init; }
    public UpperLimitManagementResult? UpperLimitManagementResult { get; init; }
    public int? UpperLimitManagedAmountYen { get; init; }
    public int? MunicipalSubsidyAmountYen { get; init; }
    public ServiceMonth? ExceptionalUsageStartMonth { get; init; }
    public ServiceMonth? ExceptionalUsageEndMonth { get; init; }
    public int? ExceptionalUsageDays { get; init; }
    public int? StandardUsageDayTotal { get; init; }
}
```

- [ ] **Step 4: IntensiveSupportEpisodeを実装する**

Office、recipient、root、revision、kind、expected head、`StartDate`を保持し、同じrootの再入力を許可する。

- [ ] **Step 5: Policy testsを通す**

Run: `dotnet test tests/Tsumugi.Domain.Tests --filter 'FullyQualifiedName~ClaimInputPolicyTests|FullyQualifiedName~IntensiveSupportEpisodePolicyTests' -v normal`

Expected: PASS。

- [ ] **Step 6: commitする**

```bash
git add src/Tsumugi.Domain/Entities/ClaimInput.cs \
  src/Tsumugi.Domain/Entities/IntensiveSupportEpisode.cs \
  src/Tsumugi.Domain/Logic/Claim/ClaimInputPolicy.cs \
  src/Tsumugi.Domain/Logic/Claim/IntensiveSupportEpisodePolicy.cs \
  src/Tsumugi.Domain/Logic/Claim/Models/ClaimInputModels.cs \
  tests/Tsumugi.Domain.Tests/Logic/Claim
git commit -m "feat(phase3-1/AC3-8): add monthly claim input histories"
```

---

### Task 6: 平均工賃・移行・負担根拠のappend-onlyモデルを実装する

**Files:**
- Create: `src/Tsumugi.Domain/Entities/AverageWageAnnualEvidence.cs`
- Create: `src/Tsumugi.Domain/Entities/OfficeClaimProfile.cs`
- Create: `src/Tsumugi.Domain/Entities/CertificateClaimEvidence.cs`
- Create: `src/Tsumugi.Domain/Entities/UpperLimitManagementStatement.cs`
- Create: `src/Tsumugi.Domain/Entities/UpperLimitManagementStatementLine.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/AverageWageAnnualEvidencePolicy.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/OfficeClaimProfilePolicy.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/CertificateClaimEvidencePolicy.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/UpperLimitManagementStatementPolicy.cs`
- Modify: `src/Tsumugi.Domain/Logic/Claim/Models/ClaimInputModels.cs`
- Test: `tests/Tsumugi.Domain.Tests/Entities/ClaimEvidenceTests.cs`
- Test: `tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimEvidencePolicyTests.cs`

- [ ] **Step 1: 不完全入力を拒否するtestを書く**

次の値検証と、各aggregateのNew → Correction → Cancel → Correction、revision欠落、分岐、root不一致、expected head不一致、期間・office・recipient混在をtestする。

- `AverageWageAnnualEvidence`: 年度不一致、負額、開所日0、延べ利用者0、`Completeness != Complete`。
- `OfficeClaimProfile`: `Unknown`、版外option、根拠なし、FiledTransition期間矛盾。
- `CertificateClaimEvidence`: 0円だが未入力、Article31 `Applicable`だが金額・期間・原本なし。
- `UpperLimitManagementStatement`: 未確定、行合計不一致、結果区分1〜3以外、月・証・事業所不一致。

- [ ] **Step 2: entity / policy testsが赤になることを確認する**

Run: `dotnet test tests/Tsumugi.Domain.Tests --filter 'FullyQualifiedName~ClaimEvidenceTests|FullyQualifiedName~ClaimEvidencePolicyTests' -v normal`

Expected: FAIL because evidence entities and policies do not exist.

- [ ] **Step 3: entered-stateとclosed stateを実装する**

```csharp
public readonly record struct EnteredYen(bool IsEntered, int? ValueYen);

public enum Article31SpecialBurdenStatus
{
    Unknown = 0,
    NotApplicable = 1,
    Applicable = 2,
}

public enum FiscalYearCompleteness
{
    Unknown = 0,
    Incomplete = 1,
    Complete = 2,
}
```

`EnteredYen`は`IsEntered=false`なら値null、`IsEntered=true`なら0以上の値必須とする。

- [ ] **Step 4: append-only metadataを共通化せず各aggregateへ明示する**

`AverageWageAnnualEvidence`と`OfficeClaimProfile`はoffice・対象年度又は有効期間、root / revision / kind / expected head、根拠document、confirmed at/by/reasonを持つ。`CertificateClaimEvidence`はcertificate ID、有効期間、entered-state、Article31入力と同じ履歴metadataを持つ。`UpperLimitManagementStatement`はservice month、recipient / certificate / managing office、原本・確定状態を持ち、lineはheader ID、行番号、事業所番号、3金額を持つ。巨大な汎用base classを作らない。

- [ ] **Step 5: aggregate別Policyを実装する**

各Policyは同じroot内の連続revision、expected head、分岐なしを検証し、Cancel後のCorrection再入力を許可する。期間・office・recipient・certificate keyが履歴内で変わる場合は拒否する。UpperLimit statementはheaderと全linesの合計・結果区分も実効選択時に再検証する。

- [ ] **Step 6: testsを通す**

Run: `dotnet test tests/Tsumugi.Domain.Tests --filter 'FullyQualifiedName~ClaimEvidenceTests|FullyQualifiedName~ClaimEvidencePolicyTests' -v normal`

Expected: PASS。

- [ ] **Step 7: commitする**

```bash
git add src/Tsumugi.Domain/Entities/AverageWageAnnualEvidence.cs \
  src/Tsumugi.Domain/Entities/OfficeClaimProfile.cs \
  src/Tsumugi.Domain/Entities/CertificateClaimEvidence.cs \
  src/Tsumugi.Domain/Entities/UpperLimitManagementStatement.cs \
  src/Tsumugi.Domain/Entities/UpperLimitManagementStatementLine.cs \
  src/Tsumugi.Domain/Logic/Claim/AverageWageAnnualEvidencePolicy.cs \
  src/Tsumugi.Domain/Logic/Claim/OfficeClaimProfilePolicy.cs \
  src/Tsumugi.Domain/Logic/Claim/CertificateClaimEvidencePolicy.cs \
  src/Tsumugi.Domain/Logic/Claim/UpperLimitManagementStatementPolicy.cs \
  src/Tsumugi.Domain/Logic/Claim/Models/ClaimInputModels.cs \
  tests/Tsumugi.Domain.Tests/Entities/ClaimEvidenceTests.cs \
  tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimEvidencePolicyTests.cs
git commit -m "feat(phase3-1/AC3-2): add claim calculation evidence"
```

---

### Task 7: 入力repository・EF configuration・migrationを完成する

**Files:**
- Create: `src/Tsumugi.Application/Abstractions/IClaimInputRepositories.cs`
- Create: repositories and configurations matching Tasks 5〜6 under `src/Tsumugi.Infrastructure/Persistence/`
- Modify: `src/Tsumugi.Infrastructure/Persistence/TsumugiDbContext.cs`
- Modify: `src/Tsumugi.Infrastructure/Persistence/AppendOnlyGuard.cs`
- Create: `src/Tsumugi.Infrastructure/Migrations/<timestamp>_Phase31ClaimInputFoundation.cs`
- Create: `src/Tsumugi.Infrastructure/Migrations/<timestamp>_Phase31ClaimInputFoundation.Designer.cs` (generated by `dotnet ef`)
- Modify: `src/Tsumugi.Infrastructure/Migrations/TsumugiDbContextModelSnapshot.cs`
- Test: `tests/Tsumugi.Infrastructure.Tests/Phase31ClaimInputMigrationTests.cs`
- Test: `tests/Tsumugi.Infrastructure.Tests/Persistence/ClaimInputRepositoryTests.cs`
- Test: `tests/Tsumugi.Infrastructure.Tests/AppendOnlyGuardPhase3Tests.cs`

- [ ] **Step 1: repositoryとmigrationの失敗testを書く**

既存Certificateが`RootCertificateId=Id / Revision=1 / ExpectedHead=null`へ移行すること、26 claim columnsがnullのまま、ClaimInput系tableが空で作成されることを検証する。

- [ ] **Step 2: testが赤になることを確認する**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter 'FullyQualifiedName~Phase31ClaimInputMigrationTests|FullyQualifiedName~ClaimInputRepositoryTests' -v normal`

Expected: FAIL because DbSets、configurations、migration do not exist.

- [ ] **Step 3: interfaces・repositories・configurationsを実装する**

repositoryは履歴全件を`AsNoTracking`で返し、実効選択をDomain Policyへ委譲する。mutable update APIをClaimInput系へ追加しない。

- [ ] **Step 4: DB制約を追加する**

- `(RootId, Revision)` unique。
- 非null`ExpectedHeadId` unique。
- revision 1 / 2以降のcheck constraint。
- ClaimInputのoffice / recipient / service month index。
- UpperLimit statement header / line FKと行番号unique。
- ClaimInput系を`AppendOnlyGuard`へ追加。

- [ ] **Step 5: migrationを生成する**

Run:

```bash
dotnet ef migrations add Phase31ClaimInputFoundation \
  --project src/Tsumugi.Infrastructure \
  --startup-project src/Tsumugi.App
```

Expected: migration、designer、model snapshotが更新される。生成物を目視し、既存claim値へのdefault補完がないことを確認する。

- [ ] **Step 6: up / down / upを検証する**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~Phase31ClaimInputMigrationTests -v normal`

Expected: PASS。既存row保持、lineage metadata決定移行、nullable claim inputsを確認。

- [ ] **Step 7: repositoryとguard testsを通す**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests --filter 'FullyQualifiedName~ClaimInputRepositoryTests|FullyQualifiedName~AppendOnlyGuardPhase3Tests' -v normal
```

Expected: PASS。

- [ ] **Step 8: commitする**

```bash
git add src/Tsumugi.Application/Abstractions/IClaimInputRepositories.cs \
  src/Tsumugi.Infrastructure/Persistence \
  src/Tsumugi.Infrastructure/Migrations \
  tests/Tsumugi.Infrastructure.Tests/Phase31ClaimInputMigrationTests.cs \
  tests/Tsumugi.Infrastructure.Tests/Persistence/ClaimInputRepositoryTests.cs \
  tests/Tsumugi.Infrastructure.Tests/AppendOnlyGuardPhase3Tests.cs
git commit -m "feat(phase3-1/AC3-8): persist claim input histories"
```

---

### Task 8: 入力保存use casesを実装する

**Files:**
- Create: `src/Tsumugi.Application/UseCases/Claim/SetClaimInputUseCase.cs`
- Create: `src/Tsumugi.Application/UseCases/Claim/SetClaimEvidenceUseCases.cs`
- Create: `src/Tsumugi.Application/Dtos/ClaimInputDtos.cs`
- Test: `tests/Tsumugi.Application.Tests/Claim/SetClaimInputUseCaseTests.cs`
- Test: `tests/Tsumugi.Application.Tests/Claim/SetClaimEvidenceUseCaseTests.cs`

- [ ] **Step 1: New / Correction / Cancel / re-entry testsを書く**

expected head必須、stale head、別root、replayではなく新operation、取消後のCorrection再入力、入力値のcross-field validationをtestする。

- [ ] **Step 2: testsが赤になることを確認する**

Run: `dotnet test tests/Tsumugi.Application.Tests --filter 'FullyQualifiedName~SetClaimInputUseCaseTests|FullyQualifiedName~SetClaimEvidenceUseCaseTests' -v normal`

Expected: FAIL because use cases do not exist.

- [ ] **Step 3: use casesを実装する**

各use caseは履歴を再読込し、`ClaimInputPolicy`、`IntensiveSupportEpisodePolicy`、`AverageWageAnnualEvidencePolicy`、`OfficeClaimProfilePolicy`、`CertificateClaimEvidencePolicy`、`UpperLimitManagementStatementPolicy`の対応するPolicyでheadを検証し、新revisionを`AddAsync`する。`TimeProvider`とactorを注入し、UIからCreatedAtを受け取らない。

- [ ] **Step 4: sanitized failureを実装する**

入力値や氏名を例外messageへ入れず、closed error codeとfield codeだけを返す。

- [ ] **Step 5: testsを通す**

Run: `dotnet test tests/Tsumugi.Application.Tests --filter 'FullyQualifiedName~SetClaimInputUseCaseTests|FullyQualifiedName~SetClaimEvidenceUseCaseTests' -v normal`

Expected: PASS。

- [ ] **Step 6: commitする**

```bash
git add src/Tsumugi.Application/UseCases/Claim \
  src/Tsumugi.Application/Dtos/ClaimInputDtos.cs \
  tests/Tsumugi.Application.Tests/Claim/SetClaimInputUseCaseTests.cs \
  tests/Tsumugi.Application.Tests/Claim/SetClaimEvidenceUseCaseTests.cs
git commit -m "feat(phase3-1/AC3-8): add claim input use cases"
```

---

### Task 9: field mappingをtyped requirementへ変換する

**Files:**
- Create: `src/Tsumugi.Application/Abstractions/IClaimInputRequirementProvider.cs`
- Create: `src/Tsumugi.Application/Claim/ClaimInputRequirement.cs`
- Create: `src/Tsumugi.Infrastructure.Csv/Mapping/ClaimInputRequirementProvider.cs`
- Test: `tests/Tsumugi.Infrastructure.Csv.Tests/ClaimInputRequirementProviderTests.cs`

- [ ] **Step 1: 51 entries / 26 targetsの集合testを書く**

```csharp
[Fact]
public void Provider_exposes_exact_phase31_target_set()
{
    var requirements = ClaimInputRequirementProvider.LoadEmbedded().GetRequirements();
    requirements.Select(x => x.TargetPath).Distinct().Should().HaveCount(26);
    requirements.SelectMany(x => x.FieldIds).Should().HaveCount(51);
}
```

- [ ] **Step 2: testが赤になることを確認する**

Run: `dotnet test tests/Tsumugi.Infrastructure.Csv.Tests --filter FullyQualifiedName~ClaimInputRequirementProviderTests -v normal`

Expected: FAIL because provider does not exist.

- [ ] **Step 3: Application contractを実装する**

```csharp
public interface IClaimInputRequirementProvider
{
    IReadOnlyList<ClaimInputRequirement> GetRequirements();
}

public sealed record ClaimInputRequirement(
    string TargetPath,
    IReadOnlyList<string> FieldIds,
    ClaimRequirementCondition Condition,
    ClaimInputDestination Destination);
```

Application contractにCSV JSON、DSL文字列、Infrastructure typeを漏らさない。`Tsumugi.Infrastructure.Csv`は既存のApplication ProjectReferenceを使用する。

- [ ] **Step 4: CSV mapping loaderを実装する**

既存embedded mappingを読み、許可済みconditionだけをclosed typed conditionへ変換する。未知condition、target重複矛盾、UI destination欠落を起動時に拒否する。

- [ ] **Step 5: positive / negative testsを通す**

Run: `dotnet test tests/Tsumugi.Infrastructure.Csv.Tests --filter FullyQualifiedName~ClaimInputRequirementProviderTests -v normal`

Expected: PASS。

- [ ] **Step 6: commitする**

```bash
git add src/Tsumugi.Application/Abstractions/IClaimInputRequirementProvider.cs \
  src/Tsumugi.Application/Claim/ClaimInputRequirement.cs \
  src/Tsumugi.Infrastructure.Csv/Mapping/ClaimInputRequirementProvider.cs \
  tests/Tsumugi.Infrastructure.Csv.Tests/ClaimInputRequirementProviderTests.cs
git commit -m "feat(phase3-1/AC3-8): expose typed claim requirements"
```

---

### Task 10: ClaimPreparationReadinessを唯一の算定前ゲートにする

**Files:**
- Create: `src/Tsumugi.Application/Claim/ClaimPreparationContracts.cs`
- Create: `src/Tsumugi.Application/Claim/ClaimPreparationReadiness.cs`
- Test: `tests/Tsumugi.Application.Tests/Claim/ClaimPreparationReadinessTests.cs`

- [ ] **Step 1: table-driven readiness testsを書く**

常時必須、条件付き必須、明示false、正式0円、複数実効Certificate、履歴破損、master版なし、原本未確認を個別にtestする。

- [ ] **Step 2: testsが赤になることを確認する**

Run: `dotnet test tests/Tsumugi.Application.Tests --filter FullyQualifiedName~ClaimPreparationReadinessTests -v normal`

Expected: FAIL because readiness does not exist.

- [ ] **Step 3: safe issue contractを実装する**

```csharp
public sealed record ClaimPreparationIssue(
    ClaimPreparationIssueCode Code,
    Guid? RecipientId,
    string FieldCode,
    ClaimInputDestination Destination);

public sealed record ClaimPreparationResult(
    bool IsReady,
    IReadOnlyList<ClaimPreparationIssue> Issues);
```

氏名、証番号、自由記述をissueへ格納しない。

- [ ] **Step 4: evaluatorを最小実装する**

typed requirementsと算定根拠契約を評価し、issueが1件でもあれば`IsReady=false`とする。Domain calculatorを呼ばない。

- [ ] **Step 5: testsを通す**

Run: `dotnet test tests/Tsumugi.Application.Tests --filter FullyQualifiedName~ClaimPreparationReadinessTests -v normal`

Expected: PASS。

- [ ] **Step 6: commitする**

```bash
git add src/Tsumugi.Application/Claim/ClaimPreparationContracts.cs \
  src/Tsumugi.Application/Claim/ClaimPreparationReadiness.cs \
  tests/Tsumugi.Application.Tests/Claim/ClaimPreparationReadinessTests.cs
git commit -m "feat(phase3-1/AC3-8): add claim readiness gate"
```

---

### Task 11: typed navigationと入力UIを完成する

**Files:**
- Create: `src/Tsumugi.App/Navigation/AppSection.cs`
- Create: `src/Tsumugi.App/Navigation/NavigationRequest.cs`
- Create: `src/Tsumugi.App/Navigation/IAppNavigationService.cs`
- Create: `src/Tsumugi.App/Navigation/AppNavigationService.cs`
- Create: `src/Tsumugi.App/ViewModels/ClaimInputViewModel.cs`
- Create: `src/Tsumugi.App/Views/ClaimInputView.axaml`
- Create: `src/Tsumugi.App/Views/ClaimInputView.axaml.cs`
- Modify: `CertificateViewModel.cs` / View、`DailyRecordViewModel.cs` / View、`OfficeViewModel.cs` / View
- Modify: `src/Tsumugi.App/ViewModels/MainViewModel.cs`
- Modify: `src/Tsumugi.App/MainWindow.axaml`
- Modify: `src/Tsumugi.App/MainWindow.axaml.cs`
- Test: `tests/Tsumugi.App.Tests/AppNavigationServiceTests.cs`
- Test: existing Certificate / DailyRecord / Office VM tests
- Test: `tests/Tsumugi.App.Tests/ClaimInputViewModelTests.cs`
- Test: `tests/Tsumugi.App.Tests/ViewInputWiringTests.cs`

- [ ] **Step 1: navigation testを書く**

readiness issueの`Destination`からCertificate、DailyRecord、Office、ClaimInputへ移動し、最小contextだけを対象VMへ渡すtestを追加する。他VMへの直接参照がないこともreflectionで確認する。

- [ ] **Step 2: testが赤になることを確認する**

Run: `dotnet test tests/Tsumugi.App.Tests --filter FullyQualifiedName~AppNavigationServiceTests -v normal`

Expected: FAIL because typed navigation does not exist.

- [ ] **Step 3: navigation coordinatorを実装する**

```csharp
public enum AppSection { Certificate, DailyRecord, Office, ClaimInput, ClaimPreparation }

public sealed record NavigationRequest(
    AppSection Section,
    Guid? RecipientId = null,
    DateOnly? ServiceDate = null,
    Guid? CertificateId = null,
    Guid? OfficeId = null,
    ServiceMonth? ServiceMonth = null);
```

`MainViewModel.SelectedSection`をTabControl選択へ双方向接続し、`MainWindow`のindex直書きnavigationをcoordinatorへ置換する。

- [ ] **Step 4: 既存画面の入力testを書く**

- Certificate: 3項目と選択版の補完Correctionだけを扱う。`CertificateClaimEvidence`の証上限・Article31原本確認は置かない。
- Office: 4項目。
- ContractedProvider: entry numberとoptimistic update。
- DailyRecord: 10項目、明示false / 0 / Unspecified、`IntensiveSupportEpisode`の開始日・revision・取消・再入力。

- [ ] **Step 5: ClaimInputViewModel testを書く**

upper-limit結果、正式結果票、`CertificateClaimEvidence`の法31条・証上限確認に加え、`AverageWageAnnualEvidence`の年度・3集計値・完全性・根拠と、`OfficeClaimProfile`の版付きoption・R8状態・指定日・支援開始日・経過措置根拠をtestする。各aggregateのrevision履歴、New / Correction / Cancel / re-entryもtestする。これらの根拠入力は`ClaimInputView`だけが所有する。

- [ ] **Step 6: 入力UI testsが赤になることを確認する**

Run:

```bash
dotnet test tests/Tsumugi.App.Tests --filter 'FullyQualifiedName~CertificateViewModelTests|FullyQualifiedName~DailyRecordViewModelTests|FullyQualifiedName~OfficeViewModelTests|FullyQualifiedName~ClaimInputViewModelTests|FullyQualifiedName~ViewInputWiringTests' -v normal
```

Expected: FAIL because the 26 target bindings、IntensiveSupportEpisode、AverageWageAnnualEvidence、OfficeClaimProfile、CertificateClaimEvidence入力とcommandsが存在しない。

- [ ] **Step 7: viewsとview modelsを実装する**

請求準備画面へ重複フォームを置かず、各本来の画面で入力する。`Ctrl+S`保存、`F5`再読込を維持する。

- [ ] **Step 8: App testsを通す**

Run:

```bash
dotnet test tests/Tsumugi.App.Tests --filter 'FullyQualifiedName~AppNavigationServiceTests|FullyQualifiedName~CertificateViewModelTests|FullyQualifiedName~DailyRecordViewModelTests|FullyQualifiedName~OfficeViewModelTests|FullyQualifiedName~ClaimInputViewModelTests|FullyQualifiedName~ViewInputWiringTests' -v normal
```

Expected: PASS。

- [ ] **Step 9: commitする**

```bash
git add src/Tsumugi.App/Navigation src/Tsumugi.App/ViewModels src/Tsumugi.App/Views \
  src/Tsumugi.App/MainWindow.axaml src/Tsumugi.App/MainWindow.axaml.cs \
  tests/Tsumugi.App.Tests
git commit -m "feat(phase3-1/AC3-8): wire claim input screens"
```

---

### Task 12: claim master schemaを算定実値へ拡張する

**Files:**
- Create: `src/Tsumugi.Domain/Logic/Claim/Models/ClaimCalculationMasters.cs`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Schema/claim-master-file.schema.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/ClaimMasterFileValidator.cs`
- Test: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSchemaPhase31Tests.cs`

- [ ] **Step 1: schema negative testsを書く**

不足する`sourceLocator`、未知`percentageBaseScope`、未知`applicationKind`、重複`calculationOrder`、`double`由来の非10進値、未知`roundingRuleId`を拒否するtestを追加する。

- [ ] **Step 2: testが赤になることを確認する**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~ClaimMasterSchemaPhase31Tests -v normal`

Expected: FAIL because the schema lacks Phase 3-1 calculation fields.

- [ ] **Step 3: typed master recordsを追加する**

```csharp
public sealed record BasicRewardMasterRow(
    string Key,
    string PaymentBand,
    string StaffingKey,
    string CapacityKey,
    string ServiceCode,
    int Units,
    ClaimSourceLocator Source);

public sealed record PercentageAdjustmentMasterRow(
    string Key,
    decimal Percentage,
    PercentageBaseScope BaseScope,
    PercentageApplicationKind ApplicationKind,
    string TargetSelector,
    int CalculationOrder,
    string RoundingRuleId,
    string CalculationStepId,
    ClaimSourceLocator Source);
```

region price、burden cap、transition rule、service codeにも同じsource contractを持たせる。

- [ ] **Step 4: schemaとvalidatorを実装する**

closed enum、非負整数、decimal、期間、source document ID、locator、selector参照、orderの穴・重複・循環を検査する。

- [ ] **Step 5: testsを通す**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~ClaimMasterSchemaPhase31Tests -v normal`

Expected: PASS。

- [ ] **Step 6: commitする**

```bash
git add src/Tsumugi.Domain/Logic/Claim/Models/ClaimCalculationMasters.cs \
  src/Tsumugi.Infrastructure/ClaimMasters/Schema/claim-master-file.schema.json \
  src/Tsumugi.Infrastructure/ClaimMasters/ClaimMasterFileValidator.cs \
  tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSchemaPhase31Tests.cs
git commit -m "feat(phase3-1/AC3-1): define calculation master schema"
```

---

### Task 13: R6 / R8制度実値をsource locator付きseedへ投入する

**Files:**
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/basic-rewards.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/additions.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/region-unit-prices.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/burden-caps.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/transition-rules.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json`
- Create: `docs/phase3-1-master-transcription-review.md`
- Test: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`

- [ ] **Step 1: seed completeness testを先に書く**

2024-04 / 06、2025-01 / 09、2026-06 release、B型基本報酬、加減算、地域単価、4 burden categories、28 payment bands、option対応、service-code参照の集合をtestする。

- [ ] **Step 2: testが赤になることを確認する**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~ClaimMasterSeedPhase31Tests -v normal`

Expected: FAIL because the five calculation seed files have zero releases.

- [ ] **Step 3: official Excel / PDFからproduction seedを転記する**

ADR 0020 / 0022 / 0023 / 0025記載のsource IDとlocatorだけを使用する。R8 B型service codesは`r8-service-codes-2-xlsx`のワークブック順38〜41、R6は対応するR6 workbook範囲を正本にし、PDF・告示・構造表で相互照合する。

- [ ] **Step 4: 各rowへprovenanceを付ける**

```json
{
  "sourceDocumentId": "r8-service-codes-2-xlsx",
  "sourceSha256": "307b631ed91a07d4fc9a77b090030b2819731aa018a0374544c1984bf2935049",
  "sourceLocator": "workbook-order=38;row=7"
}
```

locatorを確認できない値は投入しない。

- [ ] **Step 5: independent transcription reviewを行う**

Task 13のseed転記を行っていない別担当者又はfresh subagentを`source-data-reviewer`として割り当てる。実装担当者の自己承認は禁止する。

reviewerへ次だけを渡す。

- 6 seed file paths。
- ADR 0020 / 0022 / 0023 / 0025。
- SHA検証済み一次資料のlocal paths。
- review対象commit hash。

reviewerは全seed rowについて、source document ID、SHA、locator、key、数値・code、effective periodを一次資料の指定位置と照合する。`jq`でfile / release / row countを集計し、照合済みrow数がseed全row数と一致することを確認する。

合格条件は次の全てである。

- SHA一致率100%。
- locator到達率100%。
- 全row照合率100%。
- 値・code・期間のdiscrepancy 0。
- reviewerが`Approved`を返す。

`docs/phase3-1-master-transcription-review.md`へ、reviewer task ID又は氏名、対象commit、file別row count、総row数、照合数、discrepancy数、判定、実施日時を記録する。Issues Foundなら実装担当がseedを修正し、同じ独立reviewerへ全件再レビューを依頼する。

- [ ] **Step 6: seed testsを通す**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests --filter 'FullyQualifiedName~ClaimMasterSeedPhase31Tests|FullyQualifiedName~JsonClaimMasterProviderTests|FullyQualifiedName~ClaimSpecificationBoundaryTests' -v normal
```

Expected: PASS。値だけの変更でsource SHA / locatorが不変なfixtureはFAILする。

- [ ] **Step 7: commitする**

```bash
git add src/Tsumugi.Infrastructure/ClaimMasters/Seed \
  docs/phase3-1-master-transcription-review.md \
  tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs
git commit -m "data(phase3-1/AC3-1): add sourced R6 and R8 claim masters"
```

---

### Task 14: typed calculation master providerと版境界を実装する

**Files:**
- Create: `src/Tsumugi.Application/Abstractions/IClaimCalculationMasterProvider.cs`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/JsonClaimMasterProvider.cs`
- Test: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimCalculationMasterProviderTests.cs`

- [ ] **Step 1: 2026-05 / 06境界testを書く**

```csharp
[Theory]
[InlineData(2026, 5, "claim-master-r7-09")]
[InlineData(2026, 6, "claim-master-r8-06")]
public void Resolve_switches_without_code_change(int year, int month, string expectedVersion)
{
    var bundle = Provider.Resolve(new ServiceMonth(year, month));
    bundle.Release.Version.Value.Should().Be(expectedVersion);
}
```

gap、overlap、unknown reference、empty target selectorもtestする。

- [ ] **Step 2: testが赤になることを確認する**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~ClaimCalculationMasterProviderTests -v normal`

Expected: FAIL because typed calculation provider does not exist.

- [ ] **Step 3: interfaceを追加する**

```csharp
public interface IClaimCalculationMasterProvider
{
    ClaimCalculationMasterBundle Resolve(ServiceMonth serviceMonth);
}
```

既存`IClaimMasterProvider.ResolveVersion`はPhase 3-0互換のため残し、`JsonClaimMasterProvider`が両interfaceを実装する。

- [ ] **Step 4: parserと参照整合を実装する**

JSON `decimal`を直接読み、全rowのsource ID / SHA、service code、selector、rounding ID、step IDを解決したimmutable bundleを返す。

- [ ] **Step 5: testsを通す**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter 'FullyQualifiedName~ClaimCalculationMasterProviderTests|FullyQualifiedName~JsonClaimMasterProviderTests' -v normal`

Expected: PASS。

- [ ] **Step 6: commitする**

```bash
git add src/Tsumugi.Application/Abstractions/IClaimCalculationMasterProvider.cs \
  src/Tsumugi.Infrastructure/ClaimMasters/JsonClaimMasterProvider.cs \
  tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimCalculationMasterProviderTests.cs
git commit -m "feat(phase3-1/AC3-4): resolve typed claim master versions"
```

---

### Task 15: 平均工賃正式式を純粋実装する

**Files:**
- Create: `src/Tsumugi.Domain/Logic/Claim/AverageWageCalculator.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/Models/AverageWageModels.cs`
- Test: `tests/Tsumugi.Domain.Tests/Logic/Claim/AverageWageCalculatorTests.cs`

- [ ] **Step 1: corrected official rounding testsを書く**

14.679人 → 14.7人、小数第2位切上げ、最終円未満四捨五入、12固定、0除算、負値、年度不一致、incompleteをtestする。

- [ ] **Step 2: testが赤になることを確認する**

Run: `dotnet test tests/Tsumugi.Domain.Tests --filter FullyQualifiedName~AverageWageCalculatorTests -v normal`

Expected: FAIL because calculator does not exist.

- [ ] **Step 3: input / result / failureを分離する**

```csharp
public sealed record AverageWageCalculationInput(
    FiscalYear SourceFiscalYear,
    int AnnualWagePaidYen,
    int AnnualExtendedUsers,
    int AnnualOpeningDays,
    FiscalYearCompleteness Completeness,
    ClaimMasterVersion MasterVersion);
```

resultはraw / rounded daily average、raw / final monthly yen、rounding IDs、source IDsを保持する。failureはclosed reasonを返し、0円へ変換しない。

- [ ] **Step 4: decimal-onlyで実装する**

`decimal.Ceiling(rawDailyAverageUsers * 10m) / 10m`と`decimal.Round(rawAverageWageMonth, 0, MidpointRounding.AwayFromZero)`を使用し、checked変換する。

- [ ] **Step 5: testsを通す**

Run: `dotnet test tests/Tsumugi.Domain.Tests --filter FullyQualifiedName~AverageWageCalculatorTests -v normal`

Expected: PASS。

- [ ] **Step 6: commitする**

```bash
git add src/Tsumugi.Domain/Logic/Claim/AverageWageCalculator.cs \
  src/Tsumugi.Domain/Logic/Claim/Models/AverageWageModels.cs \
  tests/Tsumugi.Domain.Tests/Logic/Claim/AverageWageCalculatorTests.cs
git commit -m "feat(phase3-1/AC3-2): implement official average wage formula"
```

---

### Task 16: R8区分・経過措置・実在service code解決を実装する

**Files:**
- Create: `src/Tsumugi.Domain/Logic/Claim/PaymentBandResolver.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/ServiceCodeResolver.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/Models/PaymentBandModels.cs`
- Test: `tests/Tsumugi.Domain.Tests/Logic/Claim/PaymentBandResolverTests.cs`
- Test: `tests/Tsumugi.Domain.Tests/Logic/Claim/ServiceCodeResolverTests.cs`

- [ ] **Step 1: R6 / R8 option table testsを書く**

R6 option 1〜10、R8 option 1〜22、`FiledTransition`、新規指定1年未満、6月届出値、`R8ReformStatus.Unknown`、版外option、存在しない区分9 codeをtestする。

- [ ] **Step 2: testsが赤になることを確認する**

Run: `dotnet test tests/Tsumugi.Domain.Tests --filter 'FullyQualifiedName~PaymentBandResolverTests|FullyQualifiedName~ServiceCodeResolverTests' -v normal`

Expected: FAIL because resolvers do not exist.

- [ ] **Step 3: PaymentBand resolverを実装する**

入力optionを保持したまま、masterの閉じた対応表から数値PaymentBandと理由を返す。`FiledTransition`をservice code resolverへ直接渡さない。

- [ ] **Step 4: ServiceCode resolverを実装する**

`(masterVersion, rewardSystem, staffing, capacity, PaymentBand)`が一意に一致する実在rowだけを返す。0件・複数件・独立加減算row混入を拒否する。

- [ ] **Step 5: testsを通す**

Run: `dotnet test tests/Tsumugi.Domain.Tests --filter 'FullyQualifiedName~PaymentBandResolverTests|FullyQualifiedName~ServiceCodeResolverTests' -v normal`

Expected: PASS。

- [ ] **Step 6: commitする**

```bash
git add src/Tsumugi.Domain/Logic/Claim/PaymentBandResolver.cs \
  src/Tsumugi.Domain/Logic/Claim/ServiceCodeResolver.cs \
  src/Tsumugi.Domain/Logic/Claim/Models/PaymentBandModels.cs \
  tests/Tsumugi.Domain.Tests/Logic/Claim/PaymentBandResolverTests.cs \
  tests/Tsumugi.Domain.Tests/Logic/Claim/ServiceCodeResolverTests.cs
git commit -m "feat(phase3-1/AC3-2): resolve payment bands and service codes"
```

---

### Task 17: 公式割合適用順と丸めPolicyを実装する

**Files:**
- Create: `src/Tsumugi.Domain/Logic/Claim/ClaimRoundingPolicy.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/PercentageAdjustmentCalculator.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/Models/ClaimCalculationStep.cs`
- Test: `tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimRoundingPolicyTests.cs`
- Test: `tests/Tsumugi.Domain.Tests/Logic/Claim/PercentageAdjustmentCalculatorTests.cs`

- [ ] **Step 1: 5 rounding IDsのclosed-set testを書く**

未知ID、banker's rounding、per-line円換算、複数割合の一括丸め、order穴・重複・循環を拒否する。

- [ ] **Step 2: testsが赤になることを確認する**

Run: `dotnet test tests/Tsumugi.Domain.Tests --filter 'FullyQualifiedName~ClaimRoundingPolicyTests|FullyQualifiedName~PercentageAdjustmentCalculatorTests' -v normal`

Expected: FAIL because claim-specific rounding does not exist.

- [ ] **Step 3: RoundingPolicyを実装する**

ADR 0025の5 IDだけをswitchし、選択・合算・min・減算は受け取らない。

- [ ] **Step 4: percentage pipelineを実装する**

`PerServiceCodeUnit`は割合適用→四捨五入→回数乗算、`MonthlyTargetUnitSum`は対象整数合算→割合→四捨五入→Add / Subtractの順を固定する。

- [ ] **Step 5: testsを通す**

Run: `dotnet test tests/Tsumugi.Domain.Tests --filter 'FullyQualifiedName~ClaimRoundingPolicyTests|FullyQualifiedName~PercentageAdjustmentCalculatorTests' -v normal`

Expected: PASS。

- [ ] **Step 6: commitする**

```bash
git add src/Tsumugi.Domain/Logic/Claim/ClaimRoundingPolicy.cs \
  src/Tsumugi.Domain/Logic/Claim/PercentageAdjustmentCalculator.cs \
  src/Tsumugi.Domain/Logic/Claim/Models/ClaimCalculationStep.cs \
  tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimRoundingPolicyTests.cs \
  tests/Tsumugi.Domain.Tests/Logic/Claim/PercentageAdjustmentCalculatorTests.cs
git commit -m "feat(phase3-1/AC3-3): implement official claim rounding order"
```

---

### Task 18: 地域単価・法31条・利用者負担を実装する

**Files:**
- Create: `src/Tsumugi.Domain/Logic/Claim/RegionalCostCalculator.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/Article31SpecialBurdenPolicy.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/BurdenCalculator.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/Models/BurdenModels.cs`
- Test: `tests/Tsumugi.Domain.Tests/Logic/Claim/RegionalCostCalculatorTests.cs`
- Test: `tests/Tsumugi.Domain.Tests/Logic/Claim/BurdenCalculatorTests.cs`

- [ ] **Step 1: official order testsを書く**

月次単位合算後の地域単価1回乗算・切捨て、1割切捨て、Article31三値、証上限・制度上限、同一事業所内公式順、正式結果票転記、給付費=`cost - burden`をtestする。

- [ ] **Step 2: testsが赤になることを確認する**

Run: `dotnet test tests/Tsumugi.Domain.Tests --filter 'FullyQualifiedName~RegionalCostCalculatorTests|FullyQualifiedName~BurdenCalculatorTests' -v normal`

Expected: FAIL because calculators do not exist.

- [ ] **Step 3: regional costを実装する**

最終月次給付単位数へ`decimal`地域単価を1回だけ乗じ、`claim.rounding.cost.floor-yen.v1`で整数円へ戻す。

- [ ] **Step 4: burden pipelineを実装する**

Article31 → 証・制度上限 → 同一事業所内公式順調整 → 正式管理結果票 → cost-minus-burdenを、各step ID付きで実装する。成人B型の結果票を自動生成しない。

- [ ] **Step 5: testsを通す**

Run: `dotnet test tests/Tsumugi.Domain.Tests --filter 'FullyQualifiedName~RegionalCostCalculatorTests|FullyQualifiedName~BurdenCalculatorTests' -v normal`

Expected: PASS。

- [ ] **Step 6: commitする**

```bash
git add src/Tsumugi.Domain/Logic/Claim/RegionalCostCalculator.cs \
  src/Tsumugi.Domain/Logic/Claim/Article31SpecialBurdenPolicy.cs \
  src/Tsumugi.Domain/Logic/Claim/BurdenCalculator.cs \
  src/Tsumugi.Domain/Logic/Claim/Models/BurdenModels.cs \
  tests/Tsumugi.Domain.Tests/Logic/Claim/RegionalCostCalculatorTests.cs \
  tests/Tsumugi.Domain.Tests/Logic/Claim/BurdenCalculatorTests.cs
git commit -m "feat(phase3-1/AC3-3): calculate official claim burden"
```

---

### Task 19: ClaimCalculatorを公式golden caseへ接続する

**Files:**
- Create: `src/Tsumugi.Domain/Logic/Claim/ClaimCalculator.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/Models/ClaimCalculationModels.cs`
- Create: `tests/Tsumugi.Domain.Tests/Fixtures/claim-official-cases.json`
- Test: `tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculatorOfficialCaseTests.cs`
- Test: `tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculatorInvariantTests.cs`

- [ ] **Step 1: 公式例を独立fixtureへ転記する**

公式資料に掲載された入力、中間値、最終値だけをfixture化し、各caseへsource document ID、physical page、case labelを付ける。production seedの全rowを複製しない。

- [ ] **Step 2: end-to-endとinvariant testsを書く**

```csharp
result.TotalUnits.Should().Be(testCase.Expected.TotalUnits);
result.TotalCostYen.Should().Be(testCase.Expected.TotalCostYen);
result.DecidedBurdenYen.Should().Be(testCase.Expected.DecidedBurdenYen);
result.BenefitYen.Should().Be(testCase.Expected.BenefitYen);
result.Steps.Select(x => x.CalculationStepId)
    .Should().Equal(testCase.Expected.StepIds);
```

別test fileでdetail合計=header合計、benefit=`cost - burden`、負値なし、stable ordering、全stepにsourceがあることも先にtestする。

- [ ] **Step 3: testが赤になることを確認する**

Run: `dotnet test tests/Tsumugi.Domain.Tests --filter 'FullyQualifiedName~ClaimCalculatorOfficialCaseTests|FullyQualifiedName~ClaimCalculatorInvariantTests' -v normal`

Expected: FAIL because orchestrator does not exist.

- [ ] **Step 4: ClaimCalculatorを合成する**

calculatorは入力とtyped master bundleだけを受け、DB、clock、file、UIへ依存しない。利用者別明細合計とheader合計の一致をchecked arithmeticで検証する。

- [ ] **Step 5: testsを通す**

Run: `dotnet test tests/Tsumugi.Domain.Tests --filter FullyQualifiedName~Logic.Claim -v normal`

Expected: PASS。

- [ ] **Step 6: commitする**

```bash
git add src/Tsumugi.Domain/Logic/Claim/ClaimCalculator.cs \
  src/Tsumugi.Domain/Logic/Claim/Models/ClaimCalculationModels.cs \
  tests/Tsumugi.Domain.Tests/Fixtures/claim-official-cases.json \
  tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculatorOfficialCaseTests.cs \
  tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculatorInvariantTests.cs
git commit -m "feat(phase3-1/AC3-3): compose official claim calculation"
```

---

### Task 20: operation-local read transactionで入力snapshotを作る

**Files:**
- Create: `src/Tsumugi.Application/Abstractions/IClaimPreparationSnapshotReader.cs`
- Create: `src/Tsumugi.Application/Claim/ClaimPreparationSnapshot.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/ClaimPreparationSnapshotReader.cs`
- Test: `tests/Tsumugi.Infrastructure.Tests/Persistence/ClaimPreparationSnapshotReaderTests.cs`

- [ ] **Step 1: mixed-time readを再現するintegration testを書く**

読取り途中に別contextからDailyRecord / ClaimInput revisionを追加しても、返るsnapshotがtransaction開始時点の一貫した旧状態か、次回読取りの一貫した新状態のどちらかであり、混合しないことをtestする。

- [ ] **Step 2: testが赤になることを確認する**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~ClaimPreparationSnapshotReaderTests -v normal`

Expected: FAIL because reader does not exist.

- [ ] **Step 3: Application contractを追加する**

```csharp
public interface IClaimPreparationSnapshotReader
{
    Task<ClaimPreparationSnapshot> ReadAsync(
        Guid officeId,
        ServiceMonth serviceMonth,
        CancellationToken ct);
}
```

snapshotはimmutable arraysとclaim master version / source IDsを持ち、EF entityを外へ公開しない。

`ClaimPreparationSnapshot`は同じ操作で解決済みの`ClaimCalculationMasterBundle CalculationMasters`を必須で保持し、version / source IDsはこのbundleからのみ公開する。

- [ ] **Step 4: Infrastructure readerを実装する**

`IDbContextFactory<TsumugiDbContext>`と`IClaimCalculationMasterProvider`を注入する。1 contextを作り、connectionを開き、read transaction内で`Resolve(serviceMonth)`を正確に1回だけ呼んでbundleを固定する。その後、全entityと全履歴を`AsNoTracking`で取得し、`ClaimInputPolicy`、`IntensiveSupportEpisodePolicy`と4つのevidence Policyで実効版を選び、transaction内でmaterializeし切る。reader外でmasterを再解決しない。

- [ ] **Step 5: deterministic orderingを実装する**

recipient ID、service date、root ID、revision、service code keyのordinal順に並べる。現在時刻又はDB返却順へ依存しない。

- [ ] **Step 6: testsを通す**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~ClaimPreparationSnapshotReaderTests -v normal`

Expected: PASS。

- [ ] **Step 7: commitする**

```bash
git add src/Tsumugi.Application/Abstractions/IClaimPreparationSnapshotReader.cs \
  src/Tsumugi.Application/Claim/ClaimPreparationSnapshot.cs \
  src/Tsumugi.Infrastructure/Persistence/ClaimPreparationSnapshotReader.cs \
  tests/Tsumugi.Infrastructure.Tests/Persistence/ClaimPreparationSnapshotReaderTests.cs
git commit -m "feat(phase3-1/AC3-3): read consistent claim snapshots"
```

---

### Task 21: CalculateClaimUseCaseとcanonical PreviewHashを実装する

**Files:**
- Create: `src/Tsumugi.Application/Claim/ClaimSnapshotCanonicalizer.cs`
- Create: `src/Tsumugi.Application/UseCases/Claim/CalculateClaimUseCase.cs`
- Create: `src/Tsumugi.Application/Dtos/ClaimCalculationDtos.cs`
- Test: `tests/Tsumugi.Application.Tests/Claim/ClaimSnapshotCanonicalizerTests.cs`
- Test: `tests/Tsumugi.Application.Tests/Claim/CalculateClaimUseCaseTests.cs`

- [ ] **Step 1: canonical hash testsを書く**

入力列挙順が違っても同hash、claim関連値変更で別hash、UI表示・localized message・clock変更で同hash、hashがlowercase SHA-256であることをtestする。

- [ ] **Step 2: readiness short-circuit testを書く**

issueが1件ならcalculatorとcodecを呼ばず、safe issue一覧だけを返すfake-based testを追加する。

- [ ] **Step 3: testsが赤になることを確認する**

Run: `dotnet test tests/Tsumugi.Application.Tests --filter 'FullyQualifiedName~ClaimSnapshotCanonicalizerTests|FullyQualifiedName~CalculateClaimUseCaseTests' -v normal`

Expected: FAIL because use case and canonicalizer do not exist.

- [ ] **Step 4: canonicalizerを実装する**

固定property orderのversion付きDTOを`System.Text.Json`でUTF-8化し、安定sort後にSHA-256を計算する。entityの既定serializerを直接使わない。

- [ ] **Step 5: public APIを実装する**

```csharp
public Task<CalculateClaimResult> ExecuteAsync(
    Guid officeId,
    ServiceMonth serviceMonth,
    CancellationToken ct);
```

reader → readiness → `snapshot.CalculationMasters` → Domain calculator → canonical snapshots → preview hashの順に固定する。`CalculateClaimUseCase`へmaster providerを直接注入せず、別版を再解決できない構造にする。公開名を`PrepareClaimUseCase`へ変更しない。

- [ ] **Step 6: testsを通す**

Run: `dotnet test tests/Tsumugi.Application.Tests --filter 'FullyQualifiedName~ClaimSnapshotCanonicalizerTests|FullyQualifiedName~CalculateClaimUseCaseTests' -v normal`

Expected: PASS。

- [ ] **Step 7: commitする**

```bash
git add src/Tsumugi.Application/Claim/ClaimSnapshotCanonicalizer.cs \
  src/Tsumugi.Application/UseCases/Claim/CalculateClaimUseCase.cs \
  src/Tsumugi.Application/Dtos/ClaimCalculationDtos.cs \
  tests/Tsumugi.Application.Tests/Claim/ClaimSnapshotCanonicalizerTests.cs \
  tests/Tsumugi.Application.Tests/Claim/CalculateClaimUseCaseTests.cs
git commit -m "feat(phase3-1/AC3-3): preview canonical claim calculations"
```

---

### Task 22: semantic snapshot差分Policyを実装する

**Files:**
- Create: `src/Tsumugi.Application/Claim/ClaimDifferencePolicy.cs`
- Test: `tests/Tsumugi.Application.Tests/Claim/ClaimDifferencePolicyTests.cs`

- [ ] **Step 1: semantic diff testsを書く**

同一snapshot、利用者追加、service code変更、単位変更、rounding step変更をtestする。raw JSON文字列差分を結果にしない。

- [ ] **Step 2: testsが赤になることを確認する**

Run: `dotnet test tests/Tsumugi.Application.Tests --filter FullyQualifiedName~ClaimDifferencePolicyTests -v normal`

Expected: FAIL because difference policy does not exist.

- [ ] **Step 3: diff keyとDTOを実装する**

`RecipientId + ServiceCode + CalculationStepId`をstable keyとし、before / afterのsafe numeric valuesとchange kindを返す。氏名・証番号をApplication resultへ入れない。

- [ ] **Step 4: testsを通す**

Run: `dotnet test tests/Tsumugi.Application.Tests --filter FullyQualifiedName~ClaimDifferencePolicyTests -v normal`

Expected: PASS。

- [ ] **Step 5: commitする**

```bash
git add src/Tsumugi.Application/Claim/ClaimDifferencePolicy.cs \
  tests/Tsumugi.Application.Tests/Claim/ClaimDifferencePolicyTests.cs
git commit -m "feat(phase3-1/AC3-9): compare claim snapshots"
```

---

### Task 23: production snapshot codecとvalidated readerを実装する

**Files:**
- Create: `src/Tsumugi.Application/Claim/ClaimSnapshotValidationCodecV1.cs`
- Create: `src/Tsumugi.Application/Abstractions/IValidatedClaimSnapshotReader.cs`
- Create: `src/Tsumugi.Application/Claim/ValidatedClaimSnapshotReader.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/ProductionClaimSnapshotValidationCodecRegistry.cs`
- Test: `tests/Tsumugi.Application.Tests/Claim/ClaimSnapshotValidationCodecV1Tests.cs`
- Test: `tests/Tsumugi.Application.Tests/Claim/ValidatedClaimSnapshotReaderTests.cs`
- Test: `tests/Tsumugi.Infrastructure.Tests/Persistence/ProductionClaimSnapshotValidationCodecRegistryTests.cs`

- [ ] **Step 1: codec negative testsを書く**

非canonical JSON、hash不一致、未知property、欠落version、detail合計不一致、unknown enum、旧read-only codec書込みを拒否する。validated readerについて、revision欠落・分岐・Cancel後追加、detail欠落・余剰、header/detail合計不一致、4版不一致、operation hash不一致を拒否するtestも先に追加する。

- [ ] **Step 2: round-trip testが赤になることを確認する**

Run: `dotnet test tests/Tsumugi.Application.Tests --filter 'FullyQualifiedName~ClaimSnapshotValidationCodecV1Tests|FullyQualifiedName~ValidatedClaimSnapshotReaderTests' -v normal`

Expected: FAIL because codec and reader do not exist.

- [ ] **Step 3: codecを実装する**

V1 schema / codec IDを定数化し、canonical bytesをparse、全fieldを検証、再canonical化したbytesが完全一致した場合だけ`ValidatedClaimSnapshotEnvelope.CreateValidated`を呼ぶ。

- [ ] **Step 4: readerを実装する**

raw repositoryから`(OfficeId, ServiceMonth)`のClaimBatch全履歴と各batchの全detailsを読む。最初に`ClaimBatchPolicy.ValidateHistory`を全headerへ適用してroot、連続revision、expected head、Cancel終端を検証し、`ClaimBatchPolicy.Head`だけを実効headとする。次に各aggregateについてheader/detail ID、recipient一意性、detail合計=header合計、4版のheader/detail一致、operation payload hashを検証してから、各detailのinput / calculation envelopeを登録codecで再検証する。既知旧版を読めない、detail欠落・余剰、合計・版不一致のいずれでも履歴全体をfail closedにする。

- [ ] **Step 5: production registryを実装する**

現行V1は`CanWrite=true`、既知旧版は`CanWrite=false`で登録する。unknown IDへfallbackしない。

- [ ] **Step 6: testsを通す**

Run:

```bash
dotnet test tests/Tsumugi.Application.Tests --filter 'FullyQualifiedName~ClaimSnapshotValidationCodecV1Tests|FullyQualifiedName~ValidatedClaimSnapshotReaderTests' -v normal
dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~ProductionClaimSnapshotValidationCodecRegistryTests -v normal
```

Expected: PASS。

- [ ] **Step 7: commitする**

```bash
git add src/Tsumugi.Application/Claim/ClaimSnapshotValidationCodecV1.cs \
  src/Tsumugi.Application/Claim/ValidatedClaimSnapshotReader.cs \
  src/Tsumugi.Application/Abstractions/IValidatedClaimSnapshotReader.cs \
  src/Tsumugi.Infrastructure/Persistence/ProductionClaimSnapshotValidationCodecRegistry.cs \
  tests/Tsumugi.Application.Tests/Claim tests/Tsumugi.Infrastructure.Tests/Persistence/ProductionClaimSnapshotValidationCodecRegistryTests.cs
git commit -m "feat(phase3-1/AC3-9): validate production claim snapshots"
```

---

### Task 24: QueryClaimUseCaseで確定済みとの差分を返す

**Files:**
- Create: `src/Tsumugi.Application/UseCases/Claim/QueryClaimUseCase.cs`
- Test: `tests/Tsumugi.Application.Tests/Claim/QueryClaimUseCaseTests.cs`

- [ ] **Step 1: query testsを書く**

確定なし、実効New / Correction、headがCancel、現在入力不足、現在値同一、利用者・service code・step差分をtestする。確定snapshotはvalidated readerでしか読まない。

- [ ] **Step 2: testが赤になることを確認する**

Run: `dotnet test tests/Tsumugi.Application.Tests --filter FullyQualifiedName~QueryClaimUseCaseTests -v normal`

Expected: FAIL because query use case does not exist.

- [ ] **Step 3: QueryClaimUseCaseを実装する**

確定headを`IValidatedClaimSnapshotReader`で読み、現在値を`CalculateClaimUseCase`と同じpipelineで再計算し、`ClaimDifferencePolicy`へ渡す。確定ClaimBatchを変更しない。

- [ ] **Step 4: testを通す**

Run: `dotnet test tests/Tsumugi.Application.Tests --filter FullyQualifiedName~QueryClaimUseCaseTests -v normal`

Expected: PASS。

- [ ] **Step 5: commitする**

```bash
git add src/Tsumugi.Application/UseCases/Claim/QueryClaimUseCase.cs \
  tests/Tsumugi.Application.Tests/Claim/QueryClaimUseCaseTests.cs
git commit -m "feat(phase3-1/AC3-9): query claim recalculation differences"
```

---

### Task 25: CloseClaimUseCaseとCancelClaimUseCaseを既存storeへ接続する

**Files:**
- Create: `src/Tsumugi.Application/UseCases/Claim/CloseClaimUseCase.cs`
- Create: `src/Tsumugi.Application/UseCases/Claim/CancelClaimUseCase.cs`
- Test: `tests/Tsumugi.Application.Tests/Claim/CloseClaimUseCaseTests.cs`
- Test: `tests/Tsumugi.Application.Tests/Claim/CancelClaimUseCaseTests.cs`
- Test: `tests/Tsumugi.Infrastructure.Tests/Persistence/ClaimPhase31FinalizationIntegrationTests.cs`

- [ ] **Step 1: Closeのstale preview testを書く**

UI金額を引数に持たないこと、最新snapshot再算定、hash不一致停止、`RecordKind.Cancel`拒否、expected head、operation replayをtestする。

- [ ] **Step 2: Cancel専用経路testを書く**

下層readinessが失敗してもCancel可能、detailsなし、合計0、4版をheadからコピー、UseCaseがrevisionを採番しないことをtestする。

- [ ] **Step 3: testsが赤になることを確認する**

Run: `dotnet test tests/Tsumugi.Application.Tests --filter 'FullyQualifiedName~CloseClaimUseCaseTests|FullyQualifiedName~CancelClaimUseCaseTests' -v normal`

Expected: FAIL because use cases do not exist.

- [ ] **Step 4: CloseClaimUseCaseを実装する**

```csharp
public Task<ClaimFinalizationResult> ExecuteAsync(
    Guid officeId,
    ServiceMonth serviceMonth,
    string previewHash,
    RecordKind kind,
    ClaimExpectedHead? expectedHead,
    Guid operationId,
    string actor,
    CancellationToken ct);
```

kindはNew / Correctだけを許可し、再算定hash一致後にvalidated envelopesからdraftを作る。

- [ ] **Step 5: CancelClaimUseCaseを実装する**

確定Claim headだけをvalidated readerで読み、root / expected head / 4版を指定したdetails空・合計0 draftをstoreへ渡す。revisionはstoreのnon-deferred transactionだけが採番する。

- [ ] **Step 6: unit / integration testsを通す**

Run:

```bash
dotnet test tests/Tsumugi.Application.Tests --filter 'FullyQualifiedName~CloseClaimUseCaseTests|FullyQualifiedName~CancelClaimUseCaseTests' -v normal
dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~ClaimPhase31FinalizationIntegrationTests -v normal
```

Expected: PASS。

- [ ] **Step 7: commitする**

```bash
git add src/Tsumugi.Application/UseCases/Claim/CloseClaimUseCase.cs \
  src/Tsumugi.Application/UseCases/Claim/CancelClaimUseCase.cs \
  tests/Tsumugi.Application.Tests/Claim/CloseClaimUseCaseTests.cs \
  tests/Tsumugi.Application.Tests/Claim/CancelClaimUseCaseTests.cs \
  tests/Tsumugi.Infrastructure.Tests/Persistence/ClaimPhase31FinalizationIntegrationTests.cs
git commit -m "feat(phase3-1/AC3-9): finalize and cancel validated claims"
```

---

### Task 26: ClaimPreparation画面とDIを本番配線する

**Files:**
- Create: `src/Tsumugi.App/ViewModels/ClaimPreparationViewModel.cs`
- Create: `src/Tsumugi.App/Views/ClaimPreparationView.axaml`
- Create: `src/Tsumugi.App/Views/ClaimPreparationView.axaml.cs`
- Create: `src/Tsumugi.App/ClaimErrorMessageProvider.cs`
- Modify: `src/Tsumugi.App/ViewModels/MainViewModel.cs`
- Modify: `src/Tsumugi.App/MainWindow.axaml`
- Modify: `src/Tsumugi.App/CompositionRoot.cs`
- Modify: `src/Tsumugi.App/Tsumugi.App.csproj`
- Modify: `src/Tsumugi.Infrastructure/DependencyInjection.cs`
- Test: `tests/Tsumugi.App.Tests/ClaimPreparationViewModelTests.cs`
- Test: `tests/Tsumugi.App.Tests/ViewInputWiringTests.cs`
- Test: `tests/Tsumugi.App.Tests/CompositionRootTests.cs`

- [ ] **Step 1: ViewModel testを書く**

office / month選択、5つのClaimPreparationView配置項目、readiness一覧、typed navigation、preview、diff、New / Correction、専用Cancel、stale preview、head mismatch、keyboard commandsをtestする。

- [ ] **Step 2: testが赤になることを確認する**

Run: `dotnet test tests/Tsumugi.App.Tests --filter FullyQualifiedName~ClaimPreparationViewModelTests -v normal`

Expected: FAIL because ViewModel does not exist.

- [ ] **Step 3: sanitized error mappingを実装する**

closed error codeを固定日本語へ変換し、`Exception.Message`、氏名、証番号、JSONを表示しない。

- [ ] **Step 4: ViewModel / Viewを実装する**

readiness未通過、previewなし、stale、head mismatchでは確定commandを実行不能にする。UseCaseの再検証も維持する。Phase 3-2 / 3-3の未実装ボタンを置かない。

- [ ] **Step 5: production DIを切り替える**

- `Tsumugi.App.csproj`から`Tsumugi.Infrastructure.Csv`を参照し、typed requirement providerをAppのcomposition rootで登録する。ClaimInput、IntensiveSupportEpisode、AverageWageAnnualEvidence、OfficeClaimProfile、CertificateClaimEvidence、UpperLimitManagementStatementの保存use cases、snapshot reader、calculators、Calculate / Query / Close / Cancel use cases、navigation、ViewModelsも登録する。
- `UnavailableClaimSnapshotValidationCodecRegistry`をproduction registryへ置換。
- `IClaimFinalizationStore`は既存実装を維持。

- [ ] **Step 6: wiring testsを通す**

Run:

```bash
dotnet test tests/Tsumugi.App.Tests --filter 'FullyQualifiedName~ClaimPreparationViewModelTests|FullyQualifiedName~ViewInputWiringTests|FullyQualifiedName~CompositionRootTests' -v normal
```

Expected: PASS。production codec registryはwrite supportあり、未登録codec fallbackなし。

- [ ] **Step 7: commitする**

```bash
git add src/Tsumugi.App/ViewModels/ClaimPreparationViewModel.cs \
  src/Tsumugi.App/Views/ClaimPreparationView.axaml \
  src/Tsumugi.App/Views/ClaimPreparationView.axaml.cs \
  src/Tsumugi.App/ClaimErrorMessageProvider.cs \
  src/Tsumugi.App/ViewModels/MainViewModel.cs src/Tsumugi.App/MainWindow.axaml \
  src/Tsumugi.App/CompositionRoot.cs src/Tsumugi.App/Tsumugi.App.csproj \
  src/Tsumugi.Infrastructure/DependencyInjection.cs \
  tests/Tsumugi.App.Tests
git commit -m "feat(phase3-1/AC3-9): wire claim preparation workflow"
```

---

### Task 27: coverage・offline・hardcodeゲートをPhase 3-1へ引き上げる

**Files:**
- Modify: `build/ci.sh`
- Modify: `build/ci.ps1`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimSpecificationBoundaryTests.cs`
- Modify: `tests/Tsumugi.Infrastructure.Tests/AppOfflineComplianceTests.cs`
- Modify: `tests/Tsumugi.Infrastructure.Tests/OfflineComplianceTests.cs`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ArchitectureTests.cs`

- [ ] **Step 1: gateの赤化を確認する**

Application thresholdを一時的に90へ上げ、現状coverage不足ならCIが赤になることを確認する。制度値をC#へ1件だけ仮置きしてhardcode guardが赤、通信API仮参照でoffline guardが赤になることを確認し、仮変更は戻す。

- [ ] **Step 2: Application testsを追加して90%以上へ上げる**

未到達分岐を調べ、仕様上必要なfailure pathのtestだけを追加する。coverage目的の無意味なassertを追加しない。

- [ ] **Step 3: `Logic.Claim` branch 100%を測定する**

Run:

```bash
dotnet test tests/Tsumugi.Domain.Tests -c Release \
  -p:CollectCoverage=true \
  -p:Include="[Tsumugi.Domain]Tsumugi.Domain.Logic.Claim.*" \
  -p:Threshold=100 \
  -p:ThresholdType=branch \
  -p:ThresholdStat=total
```

Expected: PASS。generated codeや他namespaceを分母に含めない。

- [ ] **Step 4: shell / PowerShell parityを実装する**

Domain line 95%、Application line 90%、Claim branch 100%を`ci.sh`と`ci.ps1`の両方で同じ条件にする。

- [ ] **Step 5: full gateを逐次実行する**

Run:

```bash
dotnet format --verify-no-changes
./build/ci.sh
dotnet list package --vulnerable --include-transitive
```

Expected: exit 0、warnings 0、failed 0、skipped 0、新規未抑制advisory 0。

- [ ] **Step 6: commitする**

```bash
git add build/ci.sh build/ci.ps1 tests
git commit -m "test(phase3-1/AC3-10): enforce claim quality gates"
```

---

### Task 28: Phase 3-1受け入れ証跡を作成し最終レビューする

**Files:**
- Create: `docs/phase3-1-acceptance.md`
- Modify: `docs/open-questions.md`
- Modify: `CHANGELOG.md`
- Modify: this plan only to append an execution log after implementation

- [ ] **Step 1: targeted evidenceをfreshに逐次採取する**

Run:

```bash
dotnet test tests/Tsumugi.Domain.Tests --filter FullyQualifiedName~Logic.Claim -v normal
dotnet test tests/Tsumugi.Application.Tests --filter FullyQualifiedName~Claim -v normal
dotnet test tests/Tsumugi.Infrastructure.Tests --filter 'FullyQualifiedName~Claim|FullyQualifiedName~OfflineCompliance|FullyQualifiedName~Architecture' -v normal
dotnet test tests/Tsumugi.Infrastructure.Csv.Tests -v normal
dotnet test tests/Tsumugi.App.Tests --filter 'FullyQualifiedName~Claim|FullyQualifiedName~CompositionRoot|FullyQualifiedName~ViewInputWiring' -v normal
```

Expected: 全command exit 0。各passed / failed / skipped数を記録する。

- [ ] **Step 2: AC3-1 / 2 / 3 / 4 / 8 / 9を証跡へ結び付ける**

`docs/phase3-1-acceptance.md`へ、AC、実装ファイル、test、source IDs、commit、判定、deferredを表で記載する。Phase 3-2 / 3-3を完了扱いにしない。

- [ ] **Step 3: open questionsとCHANGELOGを同期する**

Phase 3-1で実装・検証済みの項目だけをcloseする。帳票、CSV、返戻、過誤、伝送は未実装として残す。

- [ ] **Step 4: diff integrityを確認する**

Run:

```bash
git diff --check
git status --short
git log --reverse --oneline 04382f0..HEAD
```

Expected: whitespace error 0。利用者変更をstageしていない。

- [ ] **Step 5: code reviewを依頼する**

`@superpowers:requesting-code-review`を使い、`04382f0..HEAD`、設計spec、実装plan、AC表、targeted / full gate evidenceを渡す。Critical / High / Mediumがあれば受け入れ文書を確定せず、修正Taskを追加して再レビューする。

- [ ] **Step 6: fresh full gateを再実行する**

レビュー修正後にTask 27 Step 5とtargeted 5 commandsを再実行する。過去の成功ログを流用しない。

- [ ] **Step 7: acceptance docsをcommitする**

```bash
git add docs/phase3-1-acceptance.md docs/open-questions.md CHANGELOG.md \
  docs/superpowers/plans/2026-07-11-phase3-1-claim-calculation-and-input-foundation.md
git commit -m "docs(phase3-1): record acceptance evidence"
```

- [ ] **Step 8: 完了判定を固定する**

Expected: AC3-1 / 2 / 3 / 4 / 8 / 9がaccepted、Critical / High / Medium 0、full CI green。1件でも満たさなければPhase 3-1は未完了とする。

---

## 実行時の停止条件

- 公式sourceのSHA、版、施行日、locatorがADR 0020〜0025と一致しない。
- R6 / R8の値を一次資料から一意に転記できない。
- 成人B型の上限額管理結果票を新規生成する規則が必要になる。
- 51 entries / 26 targetsとtyped requirementの集合が一致しない。
- 既存データを0 / false / NotApplicableへ推測移行しないと進めない。
- production codecが全history / all detailsを検証できない。
- Domain 95%、Application 90%、`Logic.Claim` branch 100%、offline / architecture / hardcode gateのいずれかが未達。

停止時は値を仮置きせず、`docs/open-questions.md`へsource ID、locator、観測事実、影響するACだけを記録してユーザーへ判断を返す。
