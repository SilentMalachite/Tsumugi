# Phase 4 S4 UI 補完 3 点＋ContractedProvider 運用 ADR Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** 精神手帳更新アラート、フェースシートの履歴差分、受給者証と手帳の障害種別整合警告を既存タブへ追加し、Contract / ContractedProvider の運用方針を ADR で確定する。

**Architecture:** Domain に副作用なしの Policy／Diff を置き、Application の Query UseCase が repository から取得したデータを DTO に変換する。Avalonia は既存の3画面へ読み取りパネル・バナーを埋め込み、登録フロー、DB schema、MainWindow タブ順を変更しない。

**Tech Stack:** .NET 10 / C# 14、Avalonia 11、CommunityToolkit.Mvvm、EF Core 10 / SQLite、xUnit、FluentAssertions。

**Spec:** `docs/superpowers/specs/2026-08-17-phase4-s4-ui-complements-design.md`

---

## ファイル構成

| 区分 | ファイル | 責務 |
|---|---|---|
| Domain | `src/Tsumugi.Domain/Logic/DisabilityCertificatePolicy.cs` | 精神手帳の更新期日候補を抽出 |
| Domain | `src/Tsumugi.Domain/Logic/FaceSheetDiff.cs` | 2版の業務フィールド差分を構造化して返す |
| Domain | `src/Tsumugi.Domain/Logic/DisabilityConsistencyPolicy.cs` | 証と手帳の種別不整合を構造化して検出 |
| Application | `Abstractions/IDisabilityCertificateRepository.cs` | 全件取得契約 |
| Application | `Abstractions/IFaceSheetRepository.cs` | 利用者別履歴取得契約 |
| Application | `Dtos/DisabilityCertificateRenewalDueDto.cs` | 更新アラート行 |
| Application | `Dtos/FaceSheetHistoryDto.cs` | 履歴版・変更項目 |
| Application | `Dtos/DisabilityConsistencyWarningDto.cs` | UI 用整合警告 |
| Application | `UseCases/Recipient/QueryDisabilityCertificateRenewalsUseCase.cs` | 更新アラート query |
| Application | `UseCases/Recipient/QueryFaceSheetHistoryUseCase.cs` | 履歴・直前版差分 query |
| Application | `UseCases/Certificate/QueryDisabilityConsistencyUseCase.cs` | 現行証・現行手帳の整合 query |
| Infrastructure | `Persistence/DisabilityCertificateRepository.cs` | `ListAllAsync` の EF 実装 |
| Infrastructure | `Persistence/FaceSheetRepository.cs` | 昇順の履歴 EF 実装 |
| App | `ViewModels/{DisabilityCertificate,FaceSheet,Certificate}ViewModel.cs` | Query 結果を ObservableCollection に反映 |
| App | `Views/{DisabilityCertificate,FaceSheet,Certificate}View.axaml` と `.axaml.cs` | 既存タブへパネル／バナーを配置し、Loaded で VM を初期化 |
| App | `CompositionRoot.cs` | 新 Query UseCase の DI 登録 |
| Tests | `tests/Tsumugi.Domain.Tests/{DisabilityCertificatePolicyTests,FaceSheetDiffTests,DisabilityConsistencyPolicyTests}.cs` | 純粋関数の表駆動テスト |
| Tests | `tests/Tsumugi.Application.Tests/{DisabilityCertificateUseCaseTests,FaceSheetUseCaseTests,DisabilityConsistencyUseCaseTests}.cs` | Query UseCase と fake repository |
| Tests | `tests/Tsumugi.Infrastructure.Tests/DisabilityCertificateAndFaceSheetRoundTripTests.cs` | 新 repository query の実 SQLite 検証 |
| Tests | `tests/Tsumugi.App.Tests/{DisabilityCertificateViewModelTests,FaceSheetViewModelTests,CertificateViewModelTests,ViewLifecycleWiringTests,CompositionRootTests}.cs` | 埋め込み UI・Loaded 配線・DI 解決 |
| Docs | `docs/decisions/0053-contracted-provider-and-contract-roles.md` | AC4-8 の決定 |
| Docs | `docs/open-questions.md` / `CHANGELOG.md` / roadmap | S4 の完了記録 |

## 実装上の固定事項

- `DisabilityCertificate` と `FaceSheet` は append-only のまま。UPDATE／DELETE・migration は追加しない。
- `FindRenewalDue` は `Mental` かつ `NextRenewalDate` ありだけを対象にする。既定しきい値は 30 日。
- 差分は明示した業務プロパティだけを比較し、`null` と空文字を同一視しない。
- 整合は Physical / Intellectual / Mental のみを双方向検査し、Intractable は対象外にする。
- 有効な受給者証がない場合は `DisabilityCategories.None` として扱い、手帳あり・証なしだけを返し得る。専用の「証未登録」警告は出さない。
- Domain は構造化した結果を返し、表示文言は Application で組み立てる。
- MainWindow、`AppSection`、ナビゲーションテストは変更しない。
- `ViewLifecycleWiringTests` は Loaded 初期化を持つ画面を件数ピン留めしている。手帳・フェースシートを追加したら配列・InlineData・件数を同期する。
- `CertificateViewModelTests` の `InMemoryCertRepo.FindEffectiveAsync` は現状常に `null`。整合警告テストでは `CertificatePolicy.EffectiveVersion`（`Tsumugi.Domain.Logic.Claim`）相当の実効判定へ直す。

---

### Task 1: Domain の更新期日・差分・整合 Policy を TDD で追加する

**Files:**
- Create: `src/Tsumugi.Domain/Logic/DisabilityCertificatePolicy.cs`
- Create: `src/Tsumugi.Domain/Logic/FaceSheetDiff.cs`
- Create: `src/Tsumugi.Domain/Logic/DisabilityConsistencyPolicy.cs`
- Create: `tests/Tsumugi.Domain.Tests/DisabilityCertificatePolicyTests.cs`
- Create: `tests/Tsumugi.Domain.Tests/FaceSheetDiffTests.cs`
- Create: `tests/Tsumugi.Domain.Tests/DisabilityConsistencyPolicyTests.cs`

- [x] **Step 1: 更新期日抽出の失敗テストを書く**

  `DisabilityCertificatePolicyTests` に Theory を置く。Mental の残日数 0・30 は含む、31・-1・`NextRenewalDate == null`・Physical/Intellectual は含まない、結果は残日数昇順、負のしきい値は `ArgumentOutOfRangeException` を検証する。

  ```csharp
  var hits = DisabilityCertificatePolicy.FindRenewalDue(
      certificates, new DateOnly(2026, 8, 1), thresholdDays: 30);

  hits.Select(x => x.RemainingDays).Should().Equal(0, 30);
  ```

- [x] **Step 2: テストがコンパイル不能で失敗することを確認する**

  Run: `dotnet test tests/Tsumugi.Domain.Tests --filter FullyQualifiedName~DisabilityCertificatePolicyTests`

  Expected: FAIL（`DisabilityCertificatePolicy` が未定義）。

- [x] **Step 3: 最小の更新期日 Policy を実装する**

  `DisabilityCertificateRenewalDue(DisabilityCertificate Certificate, int RemainingDays)` record と `FindRenewalDue` を追加する。`CertificatePolicy.FindExpiring` と同様に null・しきい値を guard し、`DateTime.Today` を使わない。

- [x] **Step 4: 更新期日テストが通ることを確認する**

  Run: `dotnet test tests/Tsumugi.Domain.Tests --filter FullyQualifiedName~DisabilityCertificatePolicyTests`

  Expected: PASS。

- [x] **Step 5: フェースシート差分の失敗テストを書く**

  `FaceSheetDiffTests` に、次を table-driven で追加する。

  - 同じ業務値は変更なし。
  - `Address` の文字列更新は `PropertyName == "Address"`、旧新値を返す。
  - `ReceivesDisabilityPension` の bool 変更を返す。
  - null と空文字は変更として返す。
  - `Id`、`RecipientId`、`CreatedBy`、`CreatedAt`、`ConcurrencyToken` の差は返さない。

- [x] **Step 6: 差分テストがコンパイル不能で失敗することを確認する**

  Run: `dotnet test tests/Tsumugi.Domain.Tests --filter FullyQualifiedName~FaceSheetDiffTests`

  Expected: FAIL（`FaceSheetDiff` が未定義）。

- [x] **Step 7: 明示フィールド列挙の差分実装を書く**

  `FaceSheetChange(string PropertyName, string? OldValue, string? NewValue)` を追加する。bool は `bool.ToString()` で文字列化する。以下の業務プロパティを**明示的に**比較する。反射は使わない。

  `PostalCode`, `Address`, `PhoneNumber`, `EmailAddress`, `EmergencyContactName`, `EmergencyContactRelationship`, `EmergencyContactPhone`, `FamilyComposition`, `Cohabitants`, `PrimaryDoctorName`, `PrimaryDoctorHospital`, `PrimaryDoctorPhone`, `MedicalHistory`, `CurrentConditions`, `Medications`, `Allergies`, `ReceivesNursingInsurance`, `ReceivesDisabilityPension`, `PensionDetails`, `LifeHistory`, `PersonalWishes`, `SupportNeeds`, `AssessmentSummary`。

- [x] **Step 8: 差分テストが通ることを確認する**

  Run: `dotnet test tests/Tsumugi.Domain.Tests --filter FullyQualifiedName~FaceSheetDiffTests`

  Expected: PASS。

- [x] **Step 9: 整合検出の失敗テストを書く**

  `DisabilityConsistencyPolicyTests` に以下を追加する。

  - 3種別すべて一致なら空。
  - 証だけにある Physical は `CertificateOnly`。
  - 手帳だけにある Mental は `HandbookOnly`。
  - 両方向の複数検出は安定した順序（Physical, Intellectual, Mental）で返す。
  - Intractable の ON/OFF は常に検出しない。

- [x] **Step 10: 整合検出テストがコンパイル不能で失敗することを確認する**

  Run: `dotnet test tests/Tsumugi.Domain.Tests --filter FullyQualifiedName~DisabilityConsistencyPolicyTests`

  Expected: FAIL（`DisabilityConsistencyPolicy` が未定義）。

- [x] **Step 11: 構造化した整合検出を実装する**

  `DisabilityConsistencyDirection`（`CertificateOnly` / `HandbookOnly`）と `DisabilityConsistencyFinding(DisabilityCertificateType Type, DisabilityConsistencyDirection Direction)` を追加する。`Detect(DisabilityCategories disabilities, IReadOnlySet<DisabilityCertificateType> currentTypes)` は上の固定順で結果を返す。難病を入力・結果に持ち込まない。

- [x] **Step 12: Domain テスト全体を通す**

  Run: `dotnet test tests/Tsumugi.Domain.Tests`

  Expected: PASS。

- [x] **Step 13: コミットする**

  ```bash
  git add src/Tsumugi.Domain/Logic tests/Tsumugi.Domain.Tests
  git commit -m "feat(phase4-s4): AC4-5〜AC4-7の純粋ロジックを追加する"
  ```

### Task 2: Repository 契約・Query UseCase・DTO を TDD で追加する

**Files:**
- Modify: `src/Tsumugi.Application/Abstractions/IDisabilityCertificateRepository.cs`
- Modify: `src/Tsumugi.Application/Abstractions/IFaceSheetRepository.cs`
- Create: `src/Tsumugi.Application/Dtos/DisabilityCertificateRenewalDueDto.cs`
- Create: `src/Tsumugi.Application/Dtos/FaceSheetHistoryDto.cs`
- Create: `src/Tsumugi.Application/Dtos/DisabilityConsistencyWarningDto.cs`
- Create: `src/Tsumugi.Application/UseCases/Recipient/QueryDisabilityCertificateRenewalsUseCase.cs`
- Create: `src/Tsumugi.Application/UseCases/Recipient/QueryFaceSheetHistoryUseCase.cs`
- Create: `src/Tsumugi.Application/UseCases/Certificate/QueryDisabilityConsistencyUseCase.cs`
- Modify: `tests/Tsumugi.Application.Tests/DisabilityCertificateUseCaseTests.cs`
- Modify: `tests/Tsumugi.Application.Tests/FaceSheetUseCaseTests.cs`
- Create: `tests/Tsumugi.Application.Tests/DisabilityConsistencyUseCaseTests.cs`

- [x] **Step 1: Query UseCase の失敗テストを書く**

  既存 fake repository を拡張し、次を検証する。

  - renewal query は `ListAllAsync` の結果を Domain Policy へ渡し、DTO に ID・利用者 ID・更新日・残日数を写す。
  - history query は repository の CreatedAt 昇順履歴を返し、選択版の直前版だけと差分を結ぶ。最古版の変更一覧は空。
  - consistency query は `FindEffectiveAsync(recipientId, asOf)` と手帳一覧から Type ごとの最新を選び、構造化 finding を日本語 UI 文言へ変換する。
  - effective certificate が null の場合、`DisabilityCategories.None` として `HandbookOnly` のみを返す。

- [x] **Step 2: Application テストがコンパイル不能で失敗することを確認する**

  Run: `dotnet test tests/Tsumugi.Application.Tests --filter "FullyQualifiedName~DisabilityCertificateUseCaseTests|FullyQualifiedName~FaceSheetUseCaseTests|FullyQualifiedName~DisabilityConsistencyUseCaseTests"`

  Expected: FAIL（新しい repository member / DTO / UseCase が未定義）。

- [x] **Step 3: repository interface と DTO を追加する**

  - `IDisabilityCertificateRepository.ListAllAsync(CancellationToken)` を追加する。
  - `IFaceSheetRepository.ListByRecipientAsync(Guid, CancellationToken)` を追加し、CreatedAt 昇順を interface 契約の XML doc に明記する。
  - renewal DTO は `Id`, `RecipientId`, `NextRenewalDate`, `RemainingDays`。
  - history DTO は版の `Id`, `CreatedAt`, `CreatedBy` と、選択版に紐づく `FaceSheetChangeDto` の一覧。
  - consistency DTO は `RecipientId`, `DisabilityCertificateType`, `DisabilityConsistencyDirection`, `Message`。

- [x] **Step 4: 3 Query UseCase を実装する**

  - renewal: 全手帳取得 → `FindRenewalDue` → DTO。
  - history: `ListByRecipientAsync` → 各版の直前（index - 1）を `FaceSheetDiff.Compare`。最古版は空。
  - consistency: 有効な証が null なら `DisabilityCategories.None`、手帳は `GroupBy(Type)` して `IssuedDate` 降順・`CreatedAt` 降順で先頭を選ぶ → `Detect` → direction/type ごとの固定日本語メッセージへ変換。

- [x] **Step 5: Application テストを通す**

  Run: `dotnet test tests/Tsumugi.Application.Tests`

  Expected: PASS。

- [x] **Step 6: コミットする**

  ```bash
  git add src/Tsumugi.Application tests/Tsumugi.Application.Tests
  git commit -m "feat(phase4-s4): AC4-5〜AC4-7の照会ユースケースを追加する"
  ```

### Task 3: EF repository 実装と DI を接続する

**Files:**
- Modify: `src/Tsumugi.Infrastructure/Persistence/DisabilityCertificateRepository.cs`
- Modify: `src/Tsumugi.Infrastructure/Persistence/FaceSheetRepository.cs`
- Modify: `src/Tsumugi.App/CompositionRoot.cs`
- Modify: `tests/Tsumugi.Infrastructure.Tests/DisabilityCertificateAndFaceSheetRoundTripTests.cs`
- Modify: `tests/Tsumugi.App.Tests/CompositionRootTests.cs`

- [x] **Step 1: 実 SQLite の失敗テストを書く**

  `DisabilityCertificateAndFaceSheetRoundTripTests` に次を追加する。

  - `ListAllAsync` は複数利用者の手帳を `AsNoTracking` で返す。
  - `ListByRecipientAsync` は別利用者を混ぜず、`CreatedAt` 昇順で返す。

- [x] **Step 2: Infrastructure テストが失敗することを確認する**

  Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~DisabilityCertificateAndFaceSheetRoundTripTests`

  Expected: FAIL（repository の新 member が未実装）。

- [x] **Step 3: repository を実装する**

  - `DisabilityCertificateRepository.ListAllAsync`: `db.DisabilityCertificates.AsNoTracking().ToListAsync(ct)`。
  - `FaceSheetRepository.ListByRecipientAsync`: RecipientId で絞った後にメモリ上で `CreatedAt` 昇順。SQLite の `DateTimeOffset` ORDER BY を避ける既存理由コメントを維持する。

- [x] **Step 4: DI を追加する**

  `CompositionRoot.AddTsumugiServices` に3 Query UseCase を既存 Application UseCase と同じ scoped lifetime で登録する。ViewModel は既存 transient のままにする。`CompositionRootTests` に、3 Query と既存3 ViewModel が `GetRequiredService` で解決できることを追加する。

- [x] **Step 5: repository テストとビルドを通す**

  Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~DisabilityCertificateAndFaceSheetRoundTripTests && dotnet test tests/Tsumugi.App.Tests --filter FullyQualifiedName~CompositionRootTests && dotnet build`

  Expected: PASS、警告ゼロ。

- [x] **Step 6: コミットする**

  ```bash
  git add src/Tsumugi.Infrastructure src/Tsumugi.App/CompositionRoot.cs tests/Tsumugi.Infrastructure.Tests tests/Tsumugi.App.Tests/CompositionRootTests.cs
  git commit -m "feat(phase4-s4): 手帳とフェースシートの照会永続化を接続する"
  ```

### Task 4: 手帳更新アラートと整合バナーを既存手帳タブへ埋め込む

**Files:**
- Modify: `src/Tsumugi.App/ViewModels/DisabilityCertificateViewModel.cs`
- Modify: `src/Tsumugi.App/Views/DisabilityCertificateView.axaml`
- Modify: `src/Tsumugi.App/Views/DisabilityCertificateView.axaml.cs`
- Create: `tests/Tsumugi.App.Tests/DisabilityCertificateViewModelTests.cs`
- Modify: `tests/Tsumugi.App.Tests/ViewLifecycleWiringTests.cs`

- [x] **Step 1: ViewModel の失敗テストを書く**

  - `InitializeAsync` は利用者一覧と更新アラート一覧を読み込み、明示更新でも同じ一覧を再読込する。
  - `ThresholdDays = 30` と `AsOfDate` の既定を検証する。
  - 利用者選択時に整合警告を読み、警告無しならバナー用 collection を空にする。
  - 手帳追加成功後は履歴、アラート、整合警告を再読込する。

- [x] **Step 2: App テストがコンパイル不能で失敗することを確認する**

  Run: `dotnet test tests/Tsumugi.App.Tests --filter FullyQualifiedName~DisabilityCertificateViewModelTests`

  Expected: FAIL（新しい constructor dependency / public collection が未定義）。

- [x] **Step 3: ViewModel を実装する**

  `QueryDisabilityCertificateRenewalsUseCase` と `QueryDisabilityConsistencyUseCase` を constructor 注入する。次を追加する。

  - `ObservableCollection<DisabilityCertificateRenewalDueDto> RenewalDueItems`
  - `ObservableCollection<DisabilityConsistencyWarningDto> ConsistencyWarnings`
  - `ThresholdDays = 30`, `AsOfDate = DateOnly.FromDateTime(DateTime.Today)`
  - `[RelayCommand] RefreshAlertsAsync`（アラートと、選択利用者があれば整合警告を再読込）

  `DateTime.Today` は UI の初期値だけで使い、Domain／Application の判定には渡し値を使う。

- [x] **Step 4: View にキーボード操作可能な領域を追加する**

  `DisabilityCertificateView.axaml` の既存一覧より前に、次を追加する。

  - `F5` の `KeyBinding` と「更新 (F5)」ボタン
  - 基準日・しきい値の入力
  - 更新期日・残日数を表示する read-only DataGrid
  - `ConsistencyWarnings.Count > 0` のときだけ表示する、role 相当を伝える見出し付き警告 Border

  既存の `DateOnlyConverter`、`CountGreaterThanZeroConverter`、余白・フォント DynamicResource を使う。バナーの `IsVisible` は `ConsistencyWarnings.Count` に `CountGreaterThanZeroConverter` を適用し、`StringNotEmptyConverter` を collection の可視性判定に使わない。氏名・手帳番号を新規ログに出さない。

- [x] **Step 5: Loaded で既存 VM を初期化する**

  `DisabilityCertificateView.axaml.cs` を `CertificateView.axaml.cs` と同型にする。`Loaded` を `OnLoaded` へ結び、`DataContext is DisabilityCertificateViewModel vm` のとき `await vm.InitializeAsync()` を呼ぶ。`DisabilityCertificateViewModel.InitializeAsync` は利用者一覧に続けて `RefreshAlertsAsync` を await する。これにより利用者一覧とアラート初期データが画面到達時に読み込まれる。

  `ViewLifecycleWiringTests` の配列・`InlineData`・件数ピン（現状 8）へ `DisabilityCertificateView.axaml.cs` を追加する。

- [x] **Step 6: ViewModel テストを通す**

  Run: `dotnet test tests/Tsumugi.App.Tests --filter "FullyQualifiedName~DisabilityCertificateViewModelTests|FullyQualifiedName~ViewLifecycleWiringTests"`

  Expected: PASS。

- [x] **Step 7: コミットする**

  ```bash
  git add src/Tsumugi.App/ViewModels/DisabilityCertificateViewModel.cs src/Tsumugi.App/Views/DisabilityCertificateView.axaml src/Tsumugi.App/Views/DisabilityCertificateView.axaml.cs tests/Tsumugi.App.Tests/DisabilityCertificateViewModelTests.cs tests/Tsumugi.App.Tests/ViewLifecycleWiringTests.cs
  git commit -m "feat(phase4-s4): AC4-5の精神手帳更新アラートを表示する"
  ```

### Task 5: フェースシート履歴と直前版差分を既存タブへ埋め込む

**Files:**
- Modify: `src/Tsumugi.App/ViewModels/FaceSheetViewModel.cs`
- Modify: `src/Tsumugi.App/Views/FaceSheetView.axaml`
- Modify: `src/Tsumugi.App/Views/FaceSheetView.axaml.cs`
- Create: `tests/Tsumugi.App.Tests/FaceSheetViewModelTests.cs`
- Modify: `tests/Tsumugi.App.Tests/ViewLifecycleWiringTests.cs`

- [x] **Step 1: ViewModel の失敗テストを書く**

  - 利用者選択時に CreatedAt 昇順の `HistoryItems` を読み込む。
  - 選択版を変えると、その `ChangesFromPrevious` が表示される。
  - 最古版選択時は変更一覧が空。
  - 保存成功後に現行フォームと履歴を再読込し、新版が履歴へ増える。

- [x] **Step 2: テストがコンパイル不能で失敗することを確認する**

  Run: `dotnet test tests/Tsumugi.App.Tests --filter FullyQualifiedName~FaceSheetViewModelTests`

  Expected: FAIL（history query と新 properties が未定義）。

- [x] **Step 3: ViewModel を実装する**

  `QueryFaceSheetHistoryUseCase` を注入し、以下を追加する。

  - `ObservableCollection<FaceSheetHistoryDto> HistoryItems`
  - `ObservableCollection<FaceSheetChangeDto> SelectedChanges`
  - `SelectedHistoryItem` の変更 hook で `SelectedChanges` を置換

  利用者変更時は既存 `LoadLatestAsync` に履歴読込を組み合わせ、`ClearForm` で履歴・選択・差分も必ずクリアする。保存成功時は `LoadLatestAsync` を呼び直して最新フォームと履歴を同期し、その後で `IsSaved = true` を復元する（`ClearForm` が成功表示を消すため）。

- [x] **Step 4: View に履歴／差分パネルを追加する**

  `FaceSheetView.axaml` の保存ボタンの前に、以下を追加する。

  - CreatedAt・CreatedBy を列にした履歴 DataGrid（単一選択）
  - 選択版に対する「直前版との差分」DataGrid（項目・変更前・変更後）
  - 最古版または履歴なしの説明テキスト

  `Ctrl+S` は既存保存 command を維持し、履歴選択で編集フォームの内容を上書きしない。

- [x] **Step 5: Loaded で既存 VM を初期化する**

  `FaceSheetView.axaml.cs` を `CertificateView.axaml.cs` と同型にする。`Loaded` を `OnLoaded` へ結び、`DataContext is FaceSheetViewModel vm` のとき `await vm.InitializeAsync()` を呼ぶ。これにより利用者一覧を確実に読み込み、履歴パネルへ到達できる。

  `ViewLifecycleWiringTests` へ `FaceSheetView.axaml.cs` を追加し、件数ピンを手帳追加後の値から +1 する。

- [x] **Step 6: ViewModel テストを通す**

  Run: `dotnet test tests/Tsumugi.App.Tests --filter "FullyQualifiedName~FaceSheetViewModelTests|FullyQualifiedName~ViewLifecycleWiringTests"`

  Expected: PASS。

- [x] **Step 7: コミットする**

  ```bash
  git add src/Tsumugi.App/ViewModels/FaceSheetViewModel.cs src/Tsumugi.App/Views/FaceSheetView.axaml src/Tsumugi.App/Views/FaceSheetView.axaml.cs tests/Tsumugi.App.Tests/FaceSheetViewModelTests.cs tests/Tsumugi.App.Tests/ViewLifecycleWiringTests.cs
  git commit -m "feat(phase4-s4): AC4-6のフェースシート履歴差分を表示する"
  ```

### Task 6: 受給者証タブに整合バナーを追加する

**Files:**
- Modify: `src/Tsumugi.App/ViewModels/CertificateViewModel.cs`
- Modify: `src/Tsumugi.App/Views/CertificateView.axaml`
- Modify: `tests/Tsumugi.App.Tests/CertificateViewModelTests.cs`

- [x] **Step 1: ViewModel の失敗テストを書く**

  先に `InMemoryCertRepo.FindEffectiveAsync` を `Tsumugi.Domain.Logic.Claim.CertificatePolicy.EffectiveVersion` 相当へ直し、常に `null` を返す現状をやめる。そのうえで:

  - 有効な受給者証と現行手帳が不整合なら、`ConsistencyWarnings` に構造化結果からの日本語メッセージが入る。
  - 一致すると空になる。
  - 受給者証選択／ナビゲーション文脈適用後に警告を再読込する。

- [x] **Step 2: テストがコンパイル不能で失敗することを確認する**

  Run: `dotnet test tests/Tsumugi.App.Tests --filter FullyQualifiedName~CertificateViewModelTests`

  Expected: FAIL（constructor の query dependency と warnings collection が未定義）。

- [x] **Step 3: ViewModel を実装する**

  `QueryDisabilityConsistencyUseCase` を constructor 注入する。`ObservableCollection<DisabilityConsistencyWarningDto> ConsistencyWarnings` と reload helper を追加し、選択受給者証の有効期間開始日（またはナビゲーション service date がある場合はその日）を `asOf` にして query する。`NewVm()` に整合 Query を渡し、保存 command の返り値契約は変えない。

- [x] **Step 4: View に警告バナーを追加する**

  `CertificateView.axaml` の「障害種別」セクションの直後に、警告がある場合だけ見える Border を追加する。各警告文を一覧表示し、操作をブロックする `IsEnabled`／modal は追加しない。

- [x] **Step 5: ViewModel テストを通す**

  Run: `dotnet test tests/Tsumugi.App.Tests --filter FullyQualifiedName~CertificateViewModelTests`

  Expected: PASS。

- [x] **Step 6: コミットする**

  ```bash
  git add src/Tsumugi.App/ViewModels/CertificateViewModel.cs src/Tsumugi.App/Views/CertificateView.axaml tests/Tsumugi.App.Tests/CertificateViewModelTests.cs
  git commit -m "feat(phase4-s4): AC4-7の障害種別整合警告を表示する"
  ```

### Task 7: AC4-8 の ADR と完了文書を同期する

**Files:**
- Create: `docs/decisions/0053-contracted-provider-and-contract-roles.md`
- Modify: `docs/open-questions.md`
- Modify: `docs/superpowers/specs/2026-07-05-phase4-remaining-roadmap.md`
- Modify: `CHANGELOG.md`

- [x] **Step 1: ADR 0053 を作成する**

  `結論 → 背景 → 選択肢 → 決定 → 影響` の形式で以下を固定する。

  - 工賃の当月対象選定の正本は `Contract`。
  - 請求 CSV 契約情報の正本は、自事業所行を含む `ContractedProvider`（ADR 0032 を維持）。
  - 自社を `ContractedProvider` から除外する運用は採らない。
  - 二重入力 UI と証訂正後 staleness 自動修復は対象外。

- [x] **Step 2: open-questions の4項をクローズする**

  精神手帳更新通知・フェースシート履歴差分・障害種別整合・ContractedProvider/Contract 整理を `[x]` にし、それぞれの実装パスまたは ADR 0053 を証跡として書く。療育等級表記と独自項目、staleness は未解決のまま残す。

- [x] **Step 3: roadmap と CHANGELOG を同期する**

  roadmap の冒頭ステータス・S4 行・AC4-5〜AC4-8を完了に更新し、S5 が次の順序であることを明記する。CHANGELOG に Phase 4 S4 節を追加し、3 UI 機能と ADR 0053 を要約する。

- [x] **Step 4: 文書の整合を確認する**

  Run: `rg -n "AC4-[5-8]|ContractedProvider と Contract|精神障害者保健福祉手帳の更新通知|フェースシート履歴の差分表示|障害者手帳と受給者証の障害種別整合" docs/open-questions.md docs/superpowers/specs/2026-07-05-phase4-remaining-roadmap.md CHANGELOG.md docs/decisions/0053-contracted-provider-and-contract-roles.md`

  Expected: 4 open-question は S4／ADR 0053 の証跡つきでクローズされ、roadmap・CHANGELOG と矛盾しない。

- [x] **Step 5: コミットする**

  ```bash
  git add docs/decisions/0053-contracted-provider-and-contract-roles.md docs/open-questions.md docs/superpowers/specs/2026-07-05-phase4-remaining-roadmap.md CHANGELOG.md
  git commit -m "docs(phase4-s4): AC4-5〜AC4-8の運用決定を記録する"
  ```

### Task 8: 品質ゲートを実行し、完了を確認する

**Files:**
- Modify: `docs/superpowers/plans/2026-08-17-phase4-s4-ui-complements.md`（完了した checkbox のみ）

- [x] **Step 1: 対象プロジェクトのテストを通す**

  Run: `dotnet test tests/Tsumugi.Domain.Tests && dotnet test tests/Tsumugi.Application.Tests && dotnet test tests/Tsumugi.Infrastructure.Tests && dotnet test tests/Tsumugi.App.Tests`

  Expected: すべて PASS。

- [x] **Step 2: 全体品質ゲートを通す**

  Run: `./build/ci.sh`

  Expected: build、test、format、architecture、offline guard を含めて PASS。

- [x] **Step 3: 最終差分を確認する**

  Run: `git status --short && git diff --check`

  Expected: 意図した S4 の変更だけで、空白エラーなし。

- [x] **Step 4: 計画を完了状態に更新する**

  全タスクの checkbox を `[x]` に更新するのは、対応するコミットと品質ゲートの成功後だけにする。
