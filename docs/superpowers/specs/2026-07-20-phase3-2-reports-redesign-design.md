# Phase 3-2 再設計: snapshot v2 + 3帳票（QuestPDF）

- 作成日: 2026-07-20
- 位置付け: Phase 3-2（帳票）の正本spec。旧計画 `docs/superpowers/plans/2026-06-29-phase3-2-reports.md` は本specの実装計画に置き換える
- 親仕様: `01_ClaudeCode_実装指示書_Tsumugi.md` / `06_ClaudeCode_Phase3実装指示_国保連請求_Tsumugi.md` §4.2, §7 (3-2), §8
- 直近前段: `docs/superpowers/specs/2026-07-19-phase3-1-minimal-vertical-slice-redesign-design.md`（Phase 3-1 垂直スライス、マージ済み）
- 帳票フィールド仕様正本: `docs/phase3-claim-field-mapping.md`

---

## 1. 再設計の理由（現状診断）

旧Phase 3-2計画（2026-06-29）は、レポート生成時に `officeRepo`／`recipientRepo`／`certRepo`／`dailyRepo` からライブデータを再読込する設計だった。これは Phase 3 設計spec §8 の以下規約に反する：

> 3帳票とも生成時に現行`DailyRecord`、`Certificate`、`Office`等を再読込せず、確定時のスナップショットだけを入力とする。同一入力と同一`TimeProvider`で同一バイトを返す。

一方、Phase 3-1 垂直スライスで実装された `ClaimSnapshotValidationCodecV1` は「計算結果を凍結する軽量なcalculation snapshot（`recipientId` + `serviceCode` + `billedDays` + `totalUnits` + `totalCostYen` + `benefitYen` + `burdenYen`）」のみを持ち、帳票が必要とする以下フィールドを**一切保持していない**：

- 事業所名／事業所番号／郵便番号／住所／電話番号／代表者職氏名
- 受給者漢字氏名／受給者カナ氏名
- 受給者証番号／市町村番号／自治体助成市町村番号／上限管理事業所番号
- 日次実績（サービス提供時間・特別訪問支援分・敷地外支援・医療連携種別・体験利用種別・地域協働・集中的支援・緊急受入・利用者確認）
- 集中的支援エピソード開始日
- 上限管理結果／上限管理後利用者負担額／自治体助成分請求額／例外的利用日数関連
- 明細行内訳（基本＋加算のserviceCode別行）

さらに `docs/phase3-claim-field-mapping.md` の「Phase 3-1へ送る未実装入力 (51 mapping entries)」のうち **`report:*` prefixで始まる21件は全てmissing** であり、モデル・migration・repository・typed requirementsは実装済みだが「実UI／operation-local snapshot reader／validated finalization」が未着手のまま Phase 3-1 vertical slice の凍結スコープに置かれていた。

→ Phase 3-2 は「PDFレンダリング」ではなく **「snapshot v2 + 入力UI補完 + 3帳票レンダリング」の3層合わせた垂直スライス** として再設計する。

---

## 2. ゴール

`ClaimPreparation` 画面から「入力補完 → プレビュー → 確定 → 帳票出力」を1画面で完結させ、確定時に凍結された snapshot v2 だけを入力に3帳票 PDF を決定論的に出力する。`docs/phase3-claim-field-mapping.md` の21 `report:*` フィールドを全て snapshot v2 に凍結し、DB上のモデルが空なら fail-closed で確定させない。

---

## 3. スコープ

### 3.1 含む

- **snapshot codec v2**: `ClaimSnapshotValidationCodecV1` を完全置換。canonical JSON schemaを `claim-snapshot-v2` に更新
- **OperationLocalSnapshotReader**: 確定時に Office/Recipient/Certificate/DailyRecord/ClaimInput/IntensiveSupportEpisode を集約して v2 payload に凍結
- **入力UI補完**: 以下21フィールドに対する実UIと readiness gate 追加
  - Office: `PostalCode` / `Address` / `PhoneNumber` / `RepresentativeTitleAndName`（4フィールド、`report:benefit-claim-form:header:004-008`）
  - Certificate: `MunicipalityNumber` / `SubsidyMunicipalityNumber` / `UpperLimitManagementProviderNumber`（3フィールド、`report:benefit-claim-detail:header:001,003` + `report:benefit-claim-detail:upper-limit-management:001`）
  - ClaimInput: `UpperLimitManagementResult` / `UpperLimitManagedAmountYen` / `MunicipalSubsidyAmountYen`（3フィールド、`report:benefit-claim-detail:upper-limit-management:003,004` + `report:benefit-claim-detail:summary:015`）
  - DailyRecord: `ServiceStartTime` / `ServiceEndTime` / `SpecialVisitSupportMinutes` / `MedicalCoordinationType` / `TrialUseSupportType` / `RegionalCollaborationApplied` / `EmergencyAdmissionApplied` / `IntensiveSupportApplied` / `OffsiteSupportApplied` / `RecipientConfirmation`（10フィールド、`report:service-performance:daily:004-016`）
  - IntensiveSupportEpisode: `StartDate`（1フィールド、`report:service-performance:intensive-support:001`）
- **CloseClaimUseCase readiness gate 拡張**: 上記21フィールドの欠落を確定前に検出し fail-closed
- **3帳票 QuestPDF実装**:
  - サービス提供実績記録票（`ServiceProvisionRecordDto` → `GenerateServiceProvisionRecord`）
  - 介護給付費・訓練等給付費等 請求書（`ClaimInvoiceDto` → `GenerateClaimInvoice`）
  - 介護給付費・訓練等給付費等 請求明細書（`ClaimStatementDto` → `GenerateClaimStatement`）
  - 全て機能レイアウト（A4・全必須フィールド網羅・CJK・決定論）に留める。ピクセル一致は Phase 4 以降
- **UI導線**: 既存 `ClaimPreparation` 画面に「帳票出力」セクションを追加。確定済 revision の一覧に対して3種類の保存コマンドを配置
- **DI配線**: `IClaimReportGenerator` / `GenerateClaimReportsUseCase` を DI 登録
- **21 `report:*` フィールドの網羅**: PDF 抽出テキストで全21フィールドが検出可能

### 3.2 含まない（凍結・スコープ外）

- **CSV 生成**（30 `provider:*` フィールドを CSV に書き出す部分）は Phase 3-3
- **公式帳票の実PDFピクセル一致**（罫線位置・フォントサイズ精密調整）は Phase 4 以降
- **保護施設事務費・基準該当B型**の帳票対応（Phase 3-1 で凍結、`docs/open-questions.md` 「[Phase3-1/Task13 follow-up] 保護施設事務費実値」）
- **R8-06 改定対象12区分**の新単価対応（Phase 3-1 で凍結、`ReformTarget` 事業所は 2026-06 以降フェイルクローズ継続）
- **R8-06 処遇改善加算**の率・新コード対応（Phase 3-1 で凍結）
- **band-participation option 10**（生産活動支援）対応（Phase 3-1 で凍結）
- **上限管理statement-graph解決**（複数事業所間の `UpperLimitManagementStatement` エンティティグラフ）は本スライスも対象外。本スライスは事業所ローカルの `ClaimInput.UpperLimitManagementResult` / `UpperLimitManagedAmountYen` のsnapshot凍結と描画までを含む
- **帳票の一括バッチ出力**（月次全受給者を1PDFにまとめる等）。1回1帳票の保存コマンドに留める

---

## 4. 文書・状態管理ガバナンス

Phase 3-1 再設計spec §4 と同一ガバナンスを継続適用：

### 4.1 正本の一本化
- 本specが Phase 3-2 の正本設計文書
- 実装計画は writing-plans skill 経由で `docs/superpowers/plans/2026-07-20-phase3-2-reports.md` に生成、チェックボックスが進捗の唯一の正本
- 旧計画 `docs/superpowers/plans/2026-06-29-phase3-2-reports.md` は Task 1 で archive セクション追記のうえ削除（backwards-compat hack 禁止規約）

### 4.2 codec v1 の完全置換
- `ClaimSnapshotValidationCodecV1` は削除、`ClaimSnapshotValidationCodecV2` に置換
- backwards-compat hack（v1 を残す・両対応する）は禁止
- **前提**: production DB に確定 revision が存在しない（Phase 3-1 マージ直後、GUI 手動貫通も未実施）。この前提が崩れる場合は data migration ADR を別途起票

### 4.3 文書増殖の禁止
- 本spec以外に Phase 3-2 の設計文書を作らない
- Task 単位のブリーフや報告書は `.superpowers/sdd/` 配下に留め、`docs/` 直下には追加しない
- Task 完了時の受け入れ証跡は `docs/phase3-2-acceptance.md` の1ファイルに集約

---

## 5. アーキテクチャ

### 5.1 全体パイプライン

```
┌─ 入力UI補完 (21 fields) ─────────────────┐
│ CertificateView    (+3 fields)           │
│ DailyRecordView    (+10 fields)          │
│ OfficeView         (+4 fields)           │
│ ClaimPreparationView (+3 fields)         │
│ IntensiveSupportView (+1 field)          │
└──────────────┬───────────────────────────┘
               │
               ▼
     CloseClaimUseCase (readiness gate 拡張)
               │
               ▼
     OperationLocalSnapshotReader
     ─ Office / Recipient / Certificate
     ─ DailyRecord[] / IntensiveSupportEpisode
     ─ ClaimInput / ClaimLines[]
               │
               ▼
     ClaimSnapshotValidationCodecV2
     ─ canonical JSON write
     ─ SHA-256
               │
               ▼
     ClaimBatch + ClaimDetail[]
     ─ CalculationSnapshotJson: v2 payload
     ─ 追記型・訂正は新 revision
               │
               ▼
     GenerateClaimReportsUseCase
     ─ IClaimBatchRepository で aggregate 取得
     ─ v2 payload parse (repository を一切触らない)
     ─ IClaimReportGenerator に委譲
               │
    ┌──────────┼──────────┐
    ▼          ▼          ▼
 実績記録票    請求書      請求明細書
   PDF        PDF          PDF
```

### 5.2 依存方向（厳守）

- `Tsumugi.Infrastructure.Reporting → Application → Domain`
- 帳票 generator は snapshot payload の parse 結果（DTO）だけを受け取り、`IClaimBatchRepository` や `IOfficeRepository` を一切参照しない
- `GenerateClaimReportsUseCase` は `IClaimBatchRepository` のみに依存し、Office/Recipient/Certificate/DailyRecord の各 repository を参照しない
- `Tsumugi.App` は既存 `ClaimPreparationViewModel` に帳票コマンドを追加、`IFileSaveService` は Phase 2 で確立済のものを再利用

### 5.3 設計原則

- **単一入力源**: 帳票は `ClaimBatchAggregate` の payload だけを見る。DB のライブ状態を一切参照しない
- **決定論**: 同 payload + 同 `TimeProvider` → 同バイト。SHA-256 で二重検証可
- **fail-closed 徹底**: 21 フィールドのいずれか欠落 → readiness issue → 確定不可 → 帳票生成到達不可
- **追記型維持**: 訂正 revision は独立 payload を持ち、過去 revision の帳票は不変

---

## 6. Snapshot v2 スキーマ

`ClaimDetail.CalculationSnapshotJson` は次の canonical JSON を保持する（JsonSerializerDefaults.Web、`AllowDuplicateProperties=false`、キー順序固定）：

```jsonc
{
  "schemaVersion": "claim-snapshot-v2",
  "validationCodecId": "claim-snapshot-codec-v2",
  "snapshotKind": "finalization",
  "recipientId": "00000000-0000-0000-0000-000000000000",
  "serviceMonth": "2026-05",
  "claimMasterVersion": "r6-2026-04",
  "csvSpecificationVersion": "r7-10",
  "reportSpecificationVersion": "r1-10",

  "office": {
    "officeNumber": "0123456789",
    "officeName": "テスト事業所",
    "regionGrade": "None",
    "postalCode": "1000001",
    "address": "東京都千代田区千代田1-1",
    "phoneNumber": "03-0000-0000",
    "representativeTitleAndName": "代表取締役 山田太郎"
  },

  "recipient": {
    "kanjiName": "山田太郎",
    "kanaName": "ヤマダタロウ"
  },

  "certificate": {
    "certificateNumber": "9876543210",
    "municipalityNumber": "131016",
    "subsidyMunicipalityNumber": null,
    "monthlyCostCap": 9300,
    "upperLimitManagementProviderNumber": null,
    "upperLimitManagementProviderName": null
  },

  "claimInput": {
    "upperLimitManagementResult": null,
    "upperLimitManagedAmountYen": null,
    "municipalSubsidyAmountYen": null,
    "exceptionalUsageStartMonth": null,
    "exceptionalUsageEndMonth": null,
    "exceptionalUsageDays": null,
    "standardUsageDayTotal": null
  },

  "dailyRecords": [
    {
      "serviceDate": "2026-05-01",
      "attendance": "Present",
      "mealProvided": true,
      "transportKind": "None",
      "absenceResponseNote": null,
      "serviceStartTime": "09:00",
      "serviceEndTime": "16:00",
      "specialVisitSupportMinutes": null,
      "offsiteSupportApplied": false,
      "medicalCoordinationType": null,
      "trialUseSupportType": null,
      "regionalCollaborationApplied": false,
      "intensiveSupportApplied": false,
      "emergencyAdmissionApplied": false,
      "recipientConfirmation": true
    }
  ],

  "intensiveSupportEpisode": {
    "startDate": null
  },

  "claimLines": [
    { "kind": "Basic",    "serviceCode": "B_BASE_W1_C20_S1", "unit": 600, "count": 20, "amountYen": 6720 },
    { "kind": "Addition", "serviceCode": "MEAL_PROVISION_I", "unit":  30, "count": 20, "amountYen":  336 }
  ],

  "billedDays": 20,
  "totalUnits": 630,
  "totalCostYen": 7056,
  "benefitYen": 6351,
  "burdenYen": 705
}
```

### 6.1 設計上の約束

- `null` = モデル上未入力（fail-closedはCloseClaimUseCase側で処理、v2 codecは受理）。「未使用」と「必須未入力」の区別は v2 codec の責務にしない
- `snapshotKind` は `"finalization"` または `"calculation"`。3帳票は `finalization` のみ受理し、`calculation`（軽量プレビュー用）は拒否
- `claimLines[].kind` は `"Basic"` / `"Addition"` の2値（減算対応が入る場合は将来 `"Deduction"` を追加、v3 codec bump）
- `serviceMonth` は Phase 3-1 実装済 `ServiceMonth.ToString()` 形式（`YYYY-MM`）
- `serviceStartTime`／`serviceEndTime` は `HH:mm`（TimeOnly の canonical string）
- 全ての金額は整数円、単位数は整数、`double`/`float` を使わない（Phase 3 横断ルール §4.0）

### 6.2 21 report フィールドと payload の対応

| fieldId | payload path | 型 |
|---|---|---|
| report:benefit-claim-form:header:004 | office.postalCode | string |
| report:benefit-claim-form:header:005 | office.address | string |
| report:benefit-claim-form:header:006 | office.phoneNumber | string |
| report:benefit-claim-form:header:008 | office.representativeTitleAndName | string |
| report:benefit-claim-detail:header:001 | certificate.municipalityNumber | string |
| report:benefit-claim-detail:header:003 | certificate.subsidyMunicipalityNumber | string? |
| report:benefit-claim-detail:upper-limit-management:001 | certificate.upperLimitManagementProviderNumber | string? |
| report:benefit-claim-detail:upper-limit-management:003 | claimInput.upperLimitManagementResult | string? |
| report:benefit-claim-detail:upper-limit-management:004 | claimInput.upperLimitManagedAmountYen | int? |
| report:benefit-claim-detail:summary:015 | claimInput.municipalSubsidyAmountYen | int? |
| report:service-performance:daily:004 | dailyRecords[].serviceStartTime | string? (HH:mm) |
| report:service-performance:daily:005 | dailyRecords[].serviceEndTime | string? (HH:mm) |
| report:service-performance:daily:008 | dailyRecords[].specialVisitSupportMinutes | int? |
| report:service-performance:daily:010 | dailyRecords[].medicalCoordinationType | string? |
| report:service-performance:daily:011 | dailyRecords[].trialUseSupportType | string? |
| report:service-performance:daily:012 | dailyRecords[].regionalCollaborationApplied | bool |
| report:service-performance:daily:013 | dailyRecords[].emergencyAdmissionApplied | bool |
| report:service-performance:daily:014 | dailyRecords[].intensiveSupportApplied | bool |
| report:service-performance:daily:015 | dailyRecords[].offsiteSupportApplied | bool |
| report:service-performance:daily:016 | dailyRecords[].recipientConfirmation | bool |
| report:service-performance:intensive-support:001 | intensiveSupportEpisode.startDate | string? (yyyy-MM-dd) |

---

## 7. 帳票 DTO 契約

### 7.1 サービス提供実績記録票

```csharp
public sealed record ServiceProvisionRecordDto(
    ClaimReportOfficeDto Office,
    ClaimReportRecipientDto Recipient,
    ClaimReportCertificateDto Certificate,
    YearMonth YearMonth,
    IReadOnlyList<DailyServiceRecordDto> Days,
    IntensiveSupportEpisodeDto? IntensiveSupport,
    ClaimReportSpecVersionDto SpecVersion);
```

### 7.2 介護給付費・訓練等給付費等 請求書

```csharp
public sealed record ClaimInvoiceDto(
    ClaimReportOfficeDto Office,
    YearMonth YearMonth,
    int TotalUnit,
    int TotalCostYen,
    int TotalBenefitYen,
    int TotalBurdenYen,
    ClaimReportSpecVersionDto SpecVersion);
```

### 7.3 介護給付費・訓練等給付費等 請求明細書

```csharp
public sealed record ClaimStatementDto(
    ClaimReportOfficeDto Office,
    YearMonth YearMonth,
    IReadOnlyList<RecipientClaimDetailDto> Recipients,
    int TotalUnit, int TotalCostYen, int TotalBenefitYen, int TotalBurdenYen,
    ClaimReportSpecVersionDto SpecVersion);

public sealed record RecipientClaimDetailDto(
    ClaimReportRecipientDto Recipient,
    ClaimReportCertificateDto Certificate,
    IReadOnlyList<ClaimLineDto> Lines,
    int SubtotalUnit, int SubtotalCostYen, int SubtotalBenefitYen, int SubtotalBurdenYen,
    ClaimInputSummaryDto ClaimInput);

public sealed record ClaimLineDto(
    ClaimDetailLineKind Kind, string ServiceCode, int Unit, int Count, int AmountYen);
```

### 7.4 共通 DTO

```csharp
public sealed record ClaimReportOfficeDto(
    string OfficeNumber, string OfficeName, RegionGrade RegionGrade,
    string PostalCode, string Address, string PhoneNumber, string RepresentativeTitleAndName);

public sealed record ClaimReportRecipientDto(
    string KanjiName, string KanaName);

public sealed record ClaimReportCertificateDto(
    string CertificateNumber, string MunicipalityNumber, string? SubsidyMunicipalityNumber,
    int MonthlyCostCap,
    string? UpperLimitManagementProviderNumber, string? UpperLimitManagementProviderName);

public sealed record DailyServiceRecordDto(
    DateOnly ServiceDate, Attendance Attendance, bool MealProvided, TransportKind Transport,
    string? AbsenceResponseNote,
    TimeOnly? ServiceStartTime, TimeOnly? ServiceEndTime,
    int? SpecialVisitSupportMinutes, bool OffsiteSupportApplied,
    string? MedicalCoordinationType, string? TrialUseSupportType,
    bool RegionalCollaborationApplied, bool IntensiveSupportApplied,
    bool EmergencyAdmissionApplied, bool RecipientConfirmation);

public sealed record IntensiveSupportEpisodeDto(DateOnly StartDate);

public sealed record ClaimInputSummaryDto(
    string? UpperLimitManagementResult, int? UpperLimitManagedAmountYen,
    int? MunicipalSubsidyAmountYen);

public sealed record ClaimReportSpecVersionDto(
    string ClaimMasterVersion, string CsvSpecificationVersion, string ReportSpecificationVersion);

public enum ClaimDetailLineKind { Basic, Addition }
```

---

## 8. IClaimReportGenerator 抽象

```csharp
namespace Tsumugi.Application.Abstractions;

public interface IClaimReportGenerator
{
    /// <summary>サービス提供実績記録票（A4・利用者×月次）。</summary>
    byte[] GenerateServiceProvisionRecord(ServiceProvisionRecordDto dto);

    /// <summary>介護給付費・訓練等給付費等 請求書（事業所×月次の集計）。</summary>
    byte[] GenerateClaimInvoice(ClaimInvoiceDto dto);

    /// <summary>介護給付費・訓練等給付費等 請求明細書（事業所×月次の受給者別明細）。</summary>
    byte[] GenerateClaimStatement(ClaimStatementDto dto);
}
```

実装は `Tsumugi.Infrastructure.Reporting.ClaimReportGenerator`。既存 `WageStatementPdfGenerator` と同じ流儀で `QuestPdfLicenseConfigurator.Initialize()` を初期化、`DefaultTextStyle(x => x.FontFamily("Noto Sans JP"))` を全ドキュメントに適用。`TimeProvider` は DI 注入、フッタに `GetUtcNow()` を出力（同 `TimeProvider` で決定論）。

---

## 9. GenerateClaimReportsUseCase 契約

```csharp
namespace Tsumugi.Application.UseCases.Claim;

public sealed class GenerateClaimReportsUseCase(
    IClaimBatchRepository claimBatchRepository,
    IClaimSnapshotValidationCodecRegistry codecRegistry,
    IClaimReportGenerator generator)
{
    /// <summary>指定事業所×月の最新確定 revision について、指定受給者の実績記録票を生成。</summary>
    public Task<byte[]> GenerateServiceProvisionRecordAsync(
        Guid officeId, ServiceMonth serviceMonth, Guid recipientId, CancellationToken ct);

    /// <summary>指定事業所×月の最新確定 revision について、請求書（事業所集計）を生成。</summary>
    public Task<byte[]> GenerateClaimInvoiceAsync(
        Guid officeId, ServiceMonth serviceMonth, CancellationToken ct);

    /// <summary>指定事業所×月の最新確定 revision について、請求明細書（全受給者）を生成。</summary>
    public Task<byte[]> GenerateClaimStatementAsync(
        Guid officeId, ServiceMonth serviceMonth, CancellationToken ct);
}
```

- 「最新確定 revision」= `ClaimBatchRepository.ListHistoryAggregatesAsync` で得た revision 列のうち `Kind != Cancel` の最大 revision
- Cancel 状態や revision 不在の場合は `InvalidOperationException`（fail-closed）
- **repository は `IClaimBatchRepository` のみ参照**。`IOfficeRepository`／`IRecipientRepository`／`ICertificateRepository`／`IDailyRecordRepository` を参照しない（spec §8 準拠）

---

## 10. 入力UI補完（21フィールド）

Phase 3-1 で「モデル・migration・repository・typed requirements・readiness gate まで実装済、実UI未着手」の21フィールドについて実UIを追加：

| Task (§16 番号) | UI | フィールド | 既存モデル状態 |
|---|---|---|---|
| Task 4 | `DailyRecordView` | 10 fields | migration/model 実装済（Phase 3-1 Task 9c） |
| Task 5 | `CertificateView` | 3 fields | migration/model 実装済 |
| Task 6 | `OfficeView` | 4 fields | ⚠ Phase 3-1 未確認 — Task 開始時に model 存在を確認、無ければ migration 追加 |
| Task 7 | `ClaimPreparationView` | 3 fields (`ClaimInput.*`) | migration/model 実装済（Phase 3-1 Task 12） |
| Task 8 | `DailyRecordView`（別セクション） | 1 field (`IntensiveSupportEpisode.StartDate`) | migration/model 実装済（Phase 3-1 Task 9c） |

readiness gate は `ClaimInputRequirementProvider` を拡張：

- Office 4 フィールドはいずれか欠落で readiness issue
- Certificate の `MunicipalityNumber` は必須。`SubsidyMunicipalityNumber` は Optional（`null` 許容）。`UpperLimitManagementProviderNumber` は Optional、非 null なら `ClaimInput.UpperLimitManagementResult` / `UpperLimitManagedAmountYen` を必須化
- 対象月に `Attendance.Present` の日があるのに `ServiceStartTime` / `ServiceEndTime` / `RecipientConfirmation` が空 → readiness issue
- `MunicipalSubsidyAmountYen` は事業所が `MunicipalSubsidyEligible` プロファイルの場合のみ必須（既存プロファイル判定を再利用、無ければ Optional 扱いに留めTask 6で決定）
- `IntensiveSupportEpisode.StartDate` は当該月に `IntensiveSupportApplied=true` の日があるときのみ必須

---

## 11. UI 導線: ClaimPreparation 画面拡張

既存 `ClaimPreparationView.axaml` に「帳票出力」セクションを追加：

```
[既存: 対象月・事業所・状態表示]
[既存: プレビュー / 確定 / 取下げ ボタン群]

────────────────────────────────
[新規セクション: 帳票出力]
  表示条件: 現在の対象月×事業所に確定済 revision が存在すること
  ─ サービス提供実績記録票   [受給者選択▼] [保存...]
  ─ 介護給付費等 請求書                       [保存...]
  ─ 介護給付費等 請求明細書                   [保存...]
```

各「保存...」ボタンは `IFileSaveService.PickSaveFileAsync` で保存ダイアログを開き、`GenerateClaimReportsUseCase` の対応メソッドを呼び、返却バイト列を `File.WriteAllBytesAsync` で書き出す。

デフォルトファイル名：
- 実績記録票: `{受給者漢字氏名}_サービス提供実績記録票_{YYYY}-{MM}.pdf`
- 請求書: `{事業所名}_請求書_{YYYY}-{MM}.pdf`
- 明細書: `{事業所名}_請求明細書_{YYYY}-{MM}.pdf`

Phase 1 で固定した `AccessibilityDefaults`（ダークテーマ・低アニメーション・広い余白／行間・キーボード操作）を踏襲。

---

## 12. エラー処理

- `CloseClaimUseCase` の readiness gate 未通過 → `ClaimPreparationViewModel.Preview` に issue リスト表示（既存パターン踏襲）
- `GenerateClaimReportsUseCase` に対して確定 revision が無い → `InvalidOperationException("<officeId>×<serviceMonth> に確定 revision が存在しない")`
- v2 payload の parse エラー（schema 不一致、必須キー欠落）→ codec が `InvalidClaimSnapshotException` を throw
- v1 payload を受け取った場合 → codec が拒否（backwards-compat 無し前提。開発環境で万一遭遇したら手動で再確定する運用）
- `IFileSaveService.PickSaveFileAsync` が null（ユーザーキャンセル）→ 何もせず戻る
- ファイル書き込み失敗（disk full 等）→ ViewModel が error 状態を保持、既存 error display パターンを踏襲

ログには氏名・受給者証番号・保存先フルパスを出さない（CLAUDE.md ハード制約 §4）。

---

## 13. テスト戦略

### 13.1 Application: codec v2

- canonical bytes 決定論（同オブジェクト → 同バイト、キー順序固定）
- SHA-256 一致（同 payload → 同 hash）
- round-trip（write → parse → 同構造）
- `null` フィールド許容（optional keys）
- `snapshotKind` が `"calculation"` の payload を帳票 UseCase が拒否
- v1 schemaVersion / v1 validationCodecId を持つ payload を拒否
- 空配列 `dailyRecords: []` / `claimLines: []` を許容（fail-closed は上流の責務）

### 13.2 Application: OperationLocalSnapshotReader

- 21 フィールド全てが payload に載ることを個別 in-memory テスト（1 field / 1 assertion）
- Office / Recipient / Certificate / DailyRecord / IntensiveSupportEpisode / ClaimInput の各エンティティが `null` の場合の挙動（`ArgumentNullException` or fail-closed）
- 同 revision に対して複数回呼んでも同 payload

### 13.3 Application: Requirement gate

- 21 フィールドそれぞれについて「空なら readiness issue」の negative test
- Optional フィールド（`SubsidyMunicipalityNumber` / 上限管理3点 / `MunicipalSubsidyAmountYen` / `IntensiveSupportEpisode.StartDate`）は条件付き必須の分岐テスト

### 13.4 Application: GenerateClaimReportsUseCase

- 各 UseCase が payload を parse して generator に正しく渡すこと（`IClaimReportGenerator` mock）
- 「最新確定 revision」の選択ロジック（Cancel skipping、revision 最大値）
- 確定 revision 不在で `InvalidOperationException`
- **`IOfficeRepository`／`IRecipientRepository`／etc. が DI に無くても動作する**（依存が `IClaimBatchRepository` のみであること）

### 13.5 Reporting: PDF 生成

各帳票について：

- CJK 抽出（`KangxiRadicalNormalizer.FoldKangxiRadicals` 経由）で事業所名／受給者漢字氏名／帳票タイトル／各項目名を検出
- 決定論：同 payload + 同 `TimeProvider` → 同バイト（`bytes1.Should().Equal(bytes2)`）
- 合計値：明細行合計 = header 合計、明細行の `unit × count = amountYen`
- **21 report フィールド全てが抽出テキストに含まれる**（fieldId → 描画テキスト mapping test 1本／field）
- 空 optional フィールドの描画（null → 空欄）が例外を投げない

### 13.6 App: ClaimPreparationViewModel

- 確定済 revision が無ければ帳票コマンド全てが `CanExecute=false`
- 確定済 revision があれば実績記録票コマンドは受給者選択必須、他2つは月×事業所だけで有効
- `IFileSaveService` mock で保存パスにバイトが書き込まれること
- ユーザーキャンセル（保存ダイアログでキャンセル）で例外を投げない

### 13.7 Arch: オフライン検査

- `Tsumugi.Infrastructure.Reporting` は `System.Net.*` 参照ゼロ（既存 `OfflineComplianceTests` の対象リストに Reporting を含む）
- `Tsumugi.App` は既存通り

### 13.8 品質ゲート

- `./build/ci.sh` 緑（format / build 警告ゼロ / 全テスト / Domain ≧ 95% / Application ≧ 70%）
- 依存方向テスト緑（`Tsumugi.Infrastructure.Reporting → Application/Domain` のみ）

---

## 14. Phase 3-3 への引継ぎ

Phase 3-3（CSV生成）は本specの成果物を土台にする：

- snapshot v2 payload は CSV 出力の入力にもなる。`provider:*` 30 missing fieldIds のうち `report:*` と重複する（同 model target を持つ）ものは Phase 3-2 で埋まる
- 残る `provider:*` 30 のうち Phase 3-2 で埋まらないものは Phase 3-3 の入力UI補完スコープ
- CSV 生成 UseCase も `IClaimBatchRepository` + v2 codec だけを参照する設計を継承する（spec §8 と同じ規約）
- 3帳票の合計値ロジック（`benefit = totalCost - burden`、`municipalSubsidyAmountYen` の扱い）は CSV 側の `provider:J121:04:*` フィールドと突き合わせられるように、本specで実装する集計計算を Application 層の pure helper に切り出しておく

---

## 15. 成功基準

Phase 3-2 完了の判定は以下5項目：

1. **AC3-5**: 3帳票（実績記録票・請求書・明細書）で CJK 抽出（Kangxi 正規化後）に主要漢字が含まれ、決定論（同 payload + 同 TimeProvider → 同バイト）と合計値検証が緑
2. **AC3-6**: `ClaimPreparation` 画面から3帳票を保存できる（`IFileSaveService` で保存ダイアログ → PDF 書き出し）
3. `docs/phase3-claim-field-mapping.md` の21 `report:*` フィールド全てが snapshot v2 に凍結され、対応する PDF 描画テキストで抽出可能
4. codec v1 → v2 の破壊的置換完了、backwards-compat コード無し
5. `./build/ci.sh` 緑、依存方向不変、オフライン検査緑（Reporting 含む）

---

## 16. 実装順（着手順、writing-plans skill で計画化）

1. **Task 1** — 旧計画 archive、workingtree 浄化（Phase 3-1 再設計 §4.4 に倣う）
2. **Task 2** — codec v2 実装 + Snapshot v2 schema + payload parser（Application）
3. **Task 3** — OperationLocalSnapshotReader（Application、CloseClaimUseCase に注入）
4. **Task 4** — DailyRecord 10 フィールド入力 UI + readiness gate
5. **Task 5** — Certificate 3 フィールド入力 UI + readiness gate
6. **Task 6** — Office 4 フィールド入力 UI + readiness gate（model 存在確認込み）
7. **Task 7** — ClaimInput 3 フィールド入力 UI + readiness gate
8. **Task 8** — IntensiveSupportEpisode.StartDate 入力 UI
9. **Task 9** — DTO 群 + `IClaimReportGenerator` 抽象
10. **Task 10** — `ClaimReportGenerator.GenerateServiceProvisionRecord`（10 daily fields 網羅 TDD）
11. **Task 11** — `ClaimReportGenerator.GenerateClaimInvoice`（4 office header fields 網羅 TDD）
12. **Task 12** — `ClaimReportGenerator.GenerateClaimStatement`（7 detail fields + claim-lines 網羅 TDD）
13. **Task 13** — `GenerateClaimReportsUseCase` + DI 配線
14. **Task 14** — `ClaimPreparationViewModel` 帳票コマンド拡張 + `ClaimPreparationView.axaml` 帳票セクション
15. **Task 15** — 受け入れ証跡（`docs/phase3-2-acceptance.md`）+ CLAUDE.md 現在地更新 + open-questions.md 該当項目クローズ

---

## 17. ADR 採番

本 Phase で新規に必要な ADR：

- **ADR 0029**: snapshot codec v2 schema と v1 破壊的置換の決定（本specの §6 を根拠として起票）
- **ADR 0030**: `report:*` 21 フィールドの UI 責務分割（Certificate / DailyRecord / Office / ClaimPreparation / IntensiveSupport の各 View の責務境界）

既存 ADR 0027（R6 基本報酬）・0028（主要加算）・0022（負担上限）・0023（平均工賃）は継承。

---

## 18. 参考

- Phase 3-1 再設計 spec: `docs/superpowers/specs/2026-07-19-phase3-1-minimal-vertical-slice-redesign-design.md`
- Phase 3-1 受け入れ証跡: `docs/phase3-1-acceptance.md`
- 帳票フィールド仕様: `docs/phase3-claim-field-mapping.md`
- Phase 4/S1 フォント埋込計画: `docs/superpowers/plans/2026-07-05-phase4-s1-font-embed-and-questpdf-license.md`
- Phase 2 帳票実装: `src/Tsumugi.Infrastructure.Reporting/WageStatementPdfGenerator.cs`（reference implementation）
- Phase 3 全体設計: `docs/superpowers/specs/2026-06-29-phase3-claim-data-design.md`
- Phase 3 実装指示書: `06_ClaudeCode_Phase3実装指示_国保連請求_Tsumugi.md`
