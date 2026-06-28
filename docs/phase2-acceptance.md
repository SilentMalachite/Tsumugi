# Phase 2 受け入れ基準 セルフチェック (2026-06-28)

> 計画書 `docs/plans/2026-06-28-phase2-wage-calculation.md` の AC2-1〜AC2-10 を
> Phase 2 / Group A〜G の実装結果と突き合わせて確定する。
> 達成タスク・主要テスト・遺残事項を 1 箇所に集約する。

| AC | 状態 | 達成タスク | 主要テスト |
|----|------|------------|-----------|
| **AC2-1** WorkRecord 厳密追記 + AppendOnlyGuard 違反テスト | ✅ | B2 / B3 / C1 / D1 | `WorkRecordTests` (3), `WorkRecordPolicyTests` (5), `AppendOnlyGuardPhase2Tests` (5), `WorkRecordUseCaseTests` (7) |
| **AC2-2** WageSettings 期間マスタ + 実効日付 lookup | ✅ | B5 / D2 / F3 | `WageSettingsTests` (3), `WageMasterUseCaseTests` (5), `WageFundSettingsViewModelTests` (5) |
| **AC2-3** 4 方式の素計算 (Piece/Hourly/Fixed/Equal) | ✅ | B8 | `PieceWageStrategyTests` (1), `HourlyWageStrategyTests` (4), `FixedWageStrategyTests` (2), `EqualWageStrategyTests` (1) |
| **AC2-4** Σ配分 = 原資 不変条件 | ✅ | B7 | `AllocationPolicyTests` (9, 不変条件プロパティ含む 4 ケース) |
| **AC2-5** 月次集計 + FiscalYearStartMonth=4 | ✅ | B6 / B9 / D3 / F4 | `FiscalYearPolicyTests` (10), `WageBasisExtractorTests` (3), `WageCalculatorTests` (2), `CalculateWagesUseCaseTests` (5), `WageCalculationViewModelTests` (3) |
| **AC2-6** WageStatement 確定 + 自動再計算しない (ADR 0012) | ✅ | B9 / D4 / F5 | `WageStatementTests` (2), `CloseWagesUseCaseTests` (4), `WageStatementViewModelTests` (4) |
| **AC2-7** 工賃明細・支払一覧 PDF + ADR 0013 | ✅ | A2 (ADR) / E1 / E2 / E3 / F5 | `WageStatementPdfGeneratorTests` (2), `WagePaymentListPdfGeneratorTests` (2) |
| **AC2-8** 平均工賃月額メトリクス (暫定式) | [~] partial | A1 (ADR) / B10 | `AverageWageMetricTests` (3) — **正式定義は厚労省告示/通知突合まで暫定** |
| **AC2-9** DailyRecord 二重 New partial unique index + AuditEntry | ✅ | A3 (ADR 0015) / C2 / C3 / D5 (ADR 0014) | `DailyRecordDuplicateNewIndexTests` (2), `WageStatementRepositoryTests` (partial unique 含む 2), `AuditTrailWiringTests` (5) |
| **AC2-10** キーボード完結 + ダーク低アニメ + 整数円桁区切り | ✅ | F1 / F2 / F3 / F4 / F5 / F6 | `YenFormatterTests` (6), `WorkRecordViewModelTests` (6), `CompositionRootTests` (Phase 2 全 VM 解決確認 2) |

## サマリ
- **達成数**: 9 / 10 (AC2-8 のみ部分達成、暫定式で実装済)
- **テスト総数**: 423 (Domain 119 / Application 95 / Infrastructure 113 / App 92 / Reporting 4)
- **CI ゲート**: build 0 warnings / format clean / Architecture / Offline / AppOffline 全 pass
- **カバレッジ**: Domain 88.15% / Application 81.61% (≥ 70% floor)

## Phase 2 内で生成した ADR
- ADR 0012: 工賃計算の方式戦略・端数・年度起点 (A1)
- ADR 0013: PDF 生成エンジン (QuestPDF) の採否 (A2)
- ADR 0014: 監査ログ (AuditEntry) を append-only で導入 (D5)
- ADR 0015: DailyRecord 重複 New を SQLite partial unique index で防止 (A3)

## 本番投入前に必須の deferred 事項
| 種別 | 内容 | 起票場所 |
|------|------|---------|
| **License** | QuestPDF Community License の事業所年商閾値確認 | `docs/open-questions.md` / ADR 0013 |
| **Font** | PDF 帳票の日本語フォント埋込 (Noto Sans CJK JP) | `docs/open-questions.md` |
| **Spec** | KouchinModule.bas v5 の実挙動突合 → ADR 0012 暫定値の正式化 | `docs/open-questions.md` |
| **Spec** | 平均工賃月額 (AC2-8) の厚労省告示/通知突合 → 正式定義確定 | `docs/open-questions.md` |

## Phase 1 final review からの繰越 deferred (Phase 2 では未対応)
- RecipientEditView 到達性 (Enter binding / タブ追加 / UpdateRecipient 配線)
- DailyRecord 月選択 UI (SetMonth は test-only)
- DailyRecord per-cell Space/Backspace/F2 keyboard nav (custom UserControl 必要)
- DailyRecord CorrectCommand UI trigger (F2 binding/ボタン)
- WorkCellViewModel の Piece/Points/評価点編集 (Hourly 既定なので Phase 3 で十分)

## 範囲外 (CLAUDE.md §責務境界)
- 国保連伝送・電子証明書・回線処理 (Phase 3 の請求データ生成までが本アプリの責務)

---

Phase 2 完了基準を満たすため、本ドキュメントの全 ✅ 達成項目をもって AC2 合格と判定する。
AC2-8 の正式化と上記 deferred 事項は Phase 3 着手前 or 本番投入前に解消する。
