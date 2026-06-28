# Claude Code 実装指示 — Tsumugi フェーズ2（工賃計算）

> 役割: 実装。親文書 `01_ClaudeCode_実装指示書_Tsumugi.md` のフェーズ2を、**現状のリポジトリ実態**に接地して詳細化したもの。
> 着手条件: フェーズ1の受け入れ基準が適合済み（適合済み）。本書は既存の追記機構・UseCase作法をそのまま再利用する。
> レビューは `02_Codex_レビュー指示書_Tsumugi.md` の観点で実施。

---

## 1. 結論
B型の**工賃計算**を実装する。工賃は「①作業実績(append-only) ＋ ②工賃原資/設定(期間マスタ) を入力に、純粋関数で月次集計 → ③工賃確定(append-only スナップショット)」という流れにする。計算方式・締め日・端数規則は**運用依存で未確定**なため、**戦略として外部化**し、確定前に事業所運用（既存Excel `KouchinModule.bas v5`）と突合する。配分の端数は合計が原資と一致するよう規則を明文化してテストで固定する。金額は**整数円**で扱う。

---

## 2. 前提（現状の実態に合わせる）

### 2.1 そのまま再利用する既存資産
- 追記機構: `Tsumugi.Domain.Entities.Entity`（`Guid Id`／`CreatedAt`／`CreatedBy`／`ConcurrencyToken`）、`Enums.RecordKind`（New/Correct/Cancel）、`OriginId` パターン。
- 実効状態の導出作法: `Domain.Logic.DailyRecordPolicy.Effective` / `EffectiveByDate`（同一OriginIdの兄弟はCreatedAt最新、取消で null）。**工賃の作業実績も同型で実装**する。
- 追記不変の強制: `Infrastructure.Persistence.AppendOnlyGuard.AppendOnlyTypes`（HashSet）。**新規の追記型を必ずここへ追加**する。
- 期間マスタ作法: `ValueObjects.DateRange`（両端含む、End=null継続中）、`Configurations.DateRangeJson` 変換、`PeriodPolicy`。
- 永続化作法: リポジトリ＋`IUnitOfWork`/`EfUnitOfWork`、`DesignTimeDbContextFactory` 経由のマイグレーション。
- UseCase作法: コンストラクタ `(repo, IUnitOfWork uow, TimeProvider clock)`、`Validation.DateValidator.EnsureValid`、DTOへ`Map`。
- 日付規律: `DateOnly`・暦日・両端含む。`InvariantGlobalization=true`（`Directory.Build.props`）。
- UI: `App/Settings` のアクセシビリティ既定（ダーク・低アニメ・キーボード）、`Avalonia.Controls.DataGrid` 利用可。

### 2.2 工賃のドメイン前提（B型固有・誤りやすい点）
- B型は**非雇用**。工賃は生産活動収益の分配であり、**最低賃金法の適用対象外**（A型と異なる）。よって「最低賃金」チェックは入れない。
- 関連する遵守指標は **平均工賃月額**（目標工賃・工賃向上計画）と「月額3,000円以上」目安。ただし**正式な算定定義・基準期間は告示/通知由来**のため数値・式を確定せず `docs/open-questions.md` で突合する（フェーズ3の報酬区分入力でもある）。
- 工賃の基礎となる「利用日」は、`DailyRecordPolicy` の**実効レコードのうち `Attendance.Present`** を採用する（Absent/AbsenceSupport/Discontinued は工賃基礎にしない。欠席時対応は報酬概念でありフェーズ3）。

### 2.3 未確定（Phase 2着手前に運用突合。推測で確定しない）
- 計算方式（出来高 / 時間割 / 固定 / 均等 のどれを既定にするか）、締め日、端数規則、年度起点の扱い。→ 既存 `docs/open-questions.md` の該当項目。`KouchinModule.bas v5` の実挙動を一次情報として突合し、ADR化。

---

## 3. スコープ

### 3.1 含む
- 作業実績(append-only)・工賃原資・工賃設定(期間マスタ)・工賃確定(append-only) のCRUD/確定。
- 工賃計算コア（純粋関数・方式戦略・配分端数・月次集計・年度・平均工賃月額メトリクス）。
- 工賃明細／工賃支払一覧の **PDF/印刷出力**。
- 工賃画面（ViewModel先行）。

### 3.2 含まない（フェーズ3以降）
- 報酬算定・加算単位数・国保連請求CSV（平均工賃月額は「メトリクス算出」までで、報酬区分への適用はフェーズ3）。

### 3.3 Phase 1 引継ぎのうち本フェーズで対応する整地（工賃の前提に直結するもののみ）
- **DailyRecord 二重New対策**: `(RecipientId, ServiceDate)` かつ `Kind=New` の **partial unique index** を追加（open-questions 既出）。工賃が実効レコードを合算するため整合性が前提。
- **監査ログ**: `UpdateOffice`/`UpdateRecipient` が受けている `actor` を記録する追記型 `AuditEntry`（who/when/対象/操作）を導入（open-questions 既出、小さく入れる）。

### 3.4 本フェーズで対応しない引継ぎ（明示的に先送り。工賃と無関係なため本フェーズに混ぜない）
- 精神障害者保健福祉手帳の更新通知ビュー、フェースシート差分表示、障害種別整合の警告、ContractedProvider/Contract整理。→ いずれもトラッキング継続。必要なら別パスで実施（本指示書末尾の確認事項参照）。

---

## 4. 仕様

### 4.1 新規エンティティ
すべて `Entity` を継承し、監査列を持つ。追記型は `AppendOnlyGuard.AppendOnlyTypes` に追加し、EF構成を `Persistence/Configurations` に追加、マイグレーションを発行する。

**WorkRecord（作業実績・取引記録／厳密追記）** — `DailyRecord` と同型の追記機構
- `RecipientId`、`WorkDate(DateOnly)`、`Kind(RecordKind)`、`OriginId(Guid?)`。
- 計測値（方式に応じて使う。未使用はnull）: `WorkedMinutes(int?)`（作業時間）、`PieceCount(int?)`＋`PieceUnitYen(int?)`（出来高）、`Points(int?)`（評価点）。
- `New`/`Correction`/`Cancellation` のファクトリを `DailyRecord` と同じ形で用意。
- 実効導出は `DailyRecordPolicy` と同型の `WorkRecordPolicy.Effective/EffectiveByDate`（共通化できるなら汎用化、無理なら同型複製）。

**WageFund（工賃原資・月次）** — 分配方式で使う原資
- `OfficeId`、`年月(YearMonth: int Year, int Month)`、`TotalYen(int)`（当月分配原資＝生産活動収益から工賃に充てる額）、監査列。
- 月次の単発値。修正は新値で上書きせず…→ **期間ではなく月次の確定値**として扱うため、`WageStatement` と同様に append-only ＋ `Kind/OriginId` で訂正する（黙って書き換えない）。

**WageSettings（工賃設定・期間マスタ／実効日付つき追記）**
- `OfficeId`、`Period(DateRange)`、`Method(WageMethod 列挙: Piece/Hourly/Fixed/Equal)`、`Rounding(RoundingRule 列挙: FloorYen など)`、`RemainderPolicy(余り処理: LargestRemainder / ReserveToOffice など)`、`FiscalYearStartMonth(int=4)`、（固定方式用）`FixedDailyYen(int?)`。
- 「基準日時点の設定」を期間で引く。方式の切替（年度替わり等）を破壊せず表現。

**WageStatement（工賃確定・取引記録／厳密追記）** — 締めのスナップショット
- `OfficeId`、`年月(YearMonth)`、`RecipientId`、`AmountYen(int)`、`BasisSummary(string)`（算定根拠の要約：採用方式・基礎量）、`Kind/OriginId`、監査列。
- **確定後は下層レコードの訂正で自動再計算しない**。再確定は `Correction`（新スナップショット）で表現し履歴を残す。

**AuditEntry（監査・追記）**
- `Actor`、`Action`、`TargetType`、`TargetId`、`OccurredAt`、`Summary`。同一性マスタ更新時に1行追記。

### 4.2 工賃計算コア（Domain・純粋関数）
- 方式戦略を**純粋関数**で実装。共通シグネチャの方向性:
  `Calculate(対象月の実効利用日＋実効WorkRecord, WageFund, WageSettings) → IReadOnlyList<(RecipientId, AmountYen, BasisSummary)>`。
- 方式別の素計算:
  - **Piece（出来高）**: `Σ(PieceCount×PieceUnitYen)`（原資按分なし、実額）。
  - **Hourly（時間割）**: 原資 `WageFund.TotalYen` を `WorkedMinutes` 比で按分。
  - **Fixed（固定）**: 利用日数 × `FixedDailyYen`。
  - **Equal（均等）**: 原資を対象者で均等割。
- **端数・配分（最重要・テストで固定）**:
  - 金額は**整数円**。中間比率は `decimal`、最終は `Rounding`（既定 円未満切り捨て）で整数化。
  - 按分方式（Hourly/Equal）は**配分後合計＝原資**を保証する。切り捨てで生じた余りを `RemainderPolicy` で決定的に配分（既定: 最大剰余法で残円を所定順に1円ずつ／または事業所留保）。**「合計＝原資」を不変条件としてテスト**。
  - 浮動小数点を最終金額に混入させない。
- **月次集計**: `YearMonth` 単位。`FiscalYearStartMonth` で年度を導出する純粋関数を用意（`date → 年度`）。締め日が月末でない運用に備え、集計期間は設定から導出する。
- **平均工賃月額メトリクス**: 算出関数を用意するが、**正式な定義（分母＝延べ/実利用者、対象期間）は告示/通知未確定**として `open-questions` に紐付け、暫定実装には *「要・通知突合（暫定）」* を明記。確定値をハードコードしない。

### 4.3 確定（締め）と再計算の規律
- 「計算（プレビュー）」は純粋関数でいつでも再導出可能。
- 「確定」は `WageStatement` を追記で固定。確定済み月に対する下層訂正は**自動反映しない**。差異は次月調整または再確定（`Correction`）で扱う方針をADR化。

### 4.4 帳票 / PDF（オフライン・決定的）
- 生成エンジンは **QuestPDF** を採用（Infrastructure 層、例: `Infrastructure.Reporting`）。**ライセンス条件（Community License の収益閾値等）を確認しADR化**。不可なら Avalonia 印刷経路にフォールバックする判断もADRに残す。
- 出力物: **工賃明細書（利用者ごと）** と **工賃支払一覧（事業所・月次）**。`WageStatement` を入力にする。
- 文字コード・レイアウトは決定的に。テストは生成PDFの主要テキスト/合計の検証（完全バイト一致は環境差が出るため、抽出テキスト＋金額の検証に留める）。

### 4.5 Application（UseCase）
既存作法（`(repo, IUnitOfWork, TimeProvider)`／`DateValidator`）で追加:
- 作業実績: `RecordWorkUseCase` / `CorrectWorkUseCase` / `CancelWorkUseCase` / `QueryMonthWorkUseCase`。
- 工賃原資: `SetWageFundUseCase`（追記訂正）。
- 工賃設定: `ConfigureWageSettingsUseCase`（期間マスタ追記）。
- 工賃: `CalculateWagesUseCase`（プレビュー）/ `CloseWagesUseCase`（確定＝WageStatement追記）/ `QueryWageStatementUseCase`。
- 監査: 同一性マスタ更新UseCaseで `AuditEntry` を追記。

### 4.6 UI（Avalonia / MVVM・ViewModel先行）
- 画面: 作業実績入力（月グリッド、`DailyRecord` の出欠と並走）、工賃原資/設定、工賃計算プレビュー、工賃確定、帳票出力。
- アクセシビリティ既定（`App/Settings`）を踏襲。頻用操作はキーボード完結。金額表示は整数円・桁区切り（InvariantCulture）。

---

## 5. TDD
- **Domain（中心・テーブル駆動）**:
  - 各方式の素計算（出来高/時間割/固定/均等）。
  - **配分端数の不変条件「Σ配分＝原資」**（割り切れない比率、利用者1名、全員0分、極端な偏り）。
  - 年度導出（`FiscalYearStartMonth=4`：3月/4月境界、年跨ぎ）。
  - 工賃基礎の抽出（実効`Present`のみ、取消・訂正の反映、欠席除外）。
  - 平均工賃月額メトリクス（暫定式の固定。式変更時に1箇所で差し替わること）。
- **Application（モックリポジトリ）**: 確定後の下層訂正が確定値を自動変更しないこと。再確定が`Correction`として積まれること。
- **Infrastructure（一時SQLite）**: 新追記型のCRUD往復、`AppendOnlyGuard` が `WorkRecord`/`WageStatement`/`WageFund` のUpdate/Deleteを例外で落とす（**違反テストで赤確認**）。`DailyRecord` 二重New partial unique index が効く。
- **App（ViewModel）**: 計算プレビュー・確定コマンド、金額整形。
- **帳票**: 抽出テキストと合計金額の検証。

---

## 6. 制約 / 品質ゲート（`01 §6`/CLAUDE.md を継承＋フェーズ2固有）
- `01 §6` 全項目維持（オフライン＝App含む、DB権限、nullable/警告ゼロ、依存方向、format、クロスプラットフォーム、バックアップ）。
- 新規追記型は `AppendOnlyGuard` に登録し、破壊更新が実行時に落ちる（違反テストで赤）。
- **金額は整数円**。最終金額に浮動小数点を使わない。**配分は「Σ＝原資」を保証**。
- 計算方式・締め・端数の**既定値を未確定のまま確定しない**。暫定は *要・運用突合* と明記し open-questions に紐付け、`KouchinModule.bas v5` 突合後にADR化。
- 報酬依存値（単位数・加算・地域区分単価・平均工賃月額の正式定義）をハードコードしない。
- QuestPDF 採否はライセンス確認の上ADR化。
- 新規設計判断（方式戦略、端数/余り規則、確定の再計算規律、監査ログ）は `docs/decisions/` にADR化。

---

## 7. 受け入れ基準（チェック可能）
- [ ] (AC2-1) 作業実績が `DailyRecord` と同型の厳密追記で、訂正・取消の実効導出が純粋関数で正しい。`AppendOnlyGuard` 登録済み・違反で赤を確認。
- [ ] (AC2-2) 工賃設定（方式/締め/端数/年度起点）が期間マスタとして実効日付つきで引け、方式切替を破壊せず表現できる。
- [ ] (AC2-3) 4方式の素計算が既知ケースで正しい（テーブル駆動）。
- [ ] (AC2-4) 按分方式で **Σ配分＝原資** が常に成立（端数・余りの決定的処理）。
- [ ] (AC2-5) 月次集計が `FiscalYearStartMonth=4` の年度・締めを正しく扱う。工賃基礎は実効`Present`のみ。
- [ ] (AC2-6) 工賃確定が `WageStatement` の追記スナップショットで固定され、確定後の下層訂正で自動変化しない。再確定は `Correction` として履歴に残る。
- [ ] (AC2-7) 工賃明細・工賃支払一覧をPDF/印刷出力でき、抽出テキストと合計が検証される。QuestPDF採否がADR化。
- [ ] (AC2-8) 平均工賃月額メトリクスが暫定式で算出でき、正式定義未確定が open-questions に残る（確定値をハードコードしていない）。
- [ ] (AC2-9) Phase1引継ぎ整地: `DailyRecord` 二重New partial unique index、`AuditEntry` 監査追記が入っている。
- [ ] (AC2-10) 主要操作がキーボードで完結し、ダーク・低アニメ既定。金額は整数円・桁区切り表示。

---

## 8. 着手手順
1. **運用突合を先に**: `KouchinModule.bas v5` の実挙動（方式・締め・端数・年度）を確認し、`docs/open-questions.md` の工賃項目を埋めてADR化（推測で進めない）。
2. Domainに新エンティティ（record）と計算コア（方式戦略・端数/余り・年度・基礎抽出）を**テストから**定義。
3. Infrastructureにマイグレーション・EF構成・`AppendOnlyGuard` 登録・partial unique index・リポジトリを実装し一時SQLiteで往復テスト。
4. Applicationに各UseCaseと監査追記を追加。
5. `Infrastructure.Reporting`(QuestPDF) で帳票、AppにViewModel/画面（ViewModelテスト先行）。
6. `./build/ci.sh` 緑・`dotnet format` 通過・依存方向不変を確認してフェーズ2受け入れへ。

> 公式/運用から一意に決まらない点（工賃方式の既定、平均工賃月額の正式定義、PDFライセンス）は推測で埋めず `docs/open-questions.md` に起票して設計相談へ回す。
