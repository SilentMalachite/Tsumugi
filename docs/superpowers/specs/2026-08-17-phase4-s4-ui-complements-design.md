# Phase 4 S4 設計spec — UI 補完 3 点＋ContractedProvider 運用 ADR

> **Source**: `docs/superpowers/specs/2026-07-05-phase4-remaining-roadmap.md` §8.4 から派生。親文書 `07_ClaudeCode_Phase4実装指示_リリース準備_Tsumugi.md` はリポジトリに存在しない（`01`〜`06` のみ。git 履歴にも追加痕跡なし）。実質の正本は roadmap。
> **Status**: 設計合意済（2026-08-17）／実装完了（2026-08-17、`feature/phase4-s4-ui-complements`）。
> **対応 AC**: AC4-5（精神手帳更新アラート）／AC4-6（フェースシート差分表示）／AC4-7（障害種別整合警告）／AC4-8（ContractedProvider 運用 ADR）。
> **想定サイズ**: 中大（1〜2 PR）。スキーマ変更なし。

---

## 1. 目的

Phase 1 で入れた障害者手帳・フェースシート・受給者証の基盤に対し、運用上欠けている **読み取り系 UI 3 点** と、請求導入後に未決着のまま残った **Contract / ContractedProvider 運用方針** を閉じる。

報酬算定・CSV 生成・スキーマ変更は対象外。既存の受給者証期限アラート（`CertificatePolicy.FindExpiring`）と同型の薄い Query 層を足す。

---

## 2. 現状調査（2026-08-17 実施）

### 2.1 コードベースの実測

| 対象 | 実測 |
|---|---|
| `DisabilityCertificate` / `FaceSheet` | append-only（ADR 0011）。`AppendOnlyGuard` 対象 |
| `DisabilityCertificate.NextRenewalDate` | 任意。精神は 2 年更新を ADR 0011 で固定 |
| `IDisabilityCertificateRepository` | `Add` + `ListByRecipient` のみ。**`ListAllAsync` 無し** |
| `IFaceSheetRepository` | `Add` + `FindLatest` のみ。**履歴一覧 API 無し** |
| 受給者証期限アラート | `CertificatePolicy.FindExpiring` → `ListExpiringCertificatesUseCase` → `CertificateView`（しきい値既定 30 日） |
| 障害種別の二重表現 | 受給者証 `DisabilityCategories`（身体/知的/精神/**難病**）と手帳 `DisabilityCertificateType`（身体/療育/精神）。難病に対応する手帳種別は無い |
| `Contract` | 自社利用契約（期間マスタ）。工賃の当月対象選定に使用 |
| `ContractedProvider` | 受給者証「サービス事業者記入欄」。**請求 CSV 契約情報の正本**（ADR 0032） |
| MainWindow タブ | `AppSection` 連番固定。ナビテストがタブ順をピン留め |

### 2.2 ロードマップ §8.4 との差分（本 spec で破棄／修正する前提）

| ロードマップ記載 | 本 spec の扱い |
|---|---|
| MainWindow にタブ追加（View × 3） | **破棄**。既存タブへ埋め込み（決定2） |
| AC4-8 推奨「自社は Contract のみ」 | **破棄**。ADR 0032 と矛盾するため決定5で置き換え |
| `DisabilityCertificatePolicy` / `FaceSheetDiff` / `DisabilityConsistencyPolicy` | **採用**（命名・責務は維持） |

### 2.3 open-questions（S4 でクローズする 4 項）

- 精神障害者保健福祉手帳の更新通知
- フェースシート履歴の差分表示
- 障害者手帳と受給者証の障害種別整合
- ContractedProvider と Contract の整理

**S4 対象外のまま残す近傍項**: 療育等級表記の ComboBox 外部化、フェースシート自治体/事業所独自項目、証訂正後 ContractedProvider staleness の自動修復。

---

## 3. 決定

### 決定1: 実装は「薄い埋め込み」に限定する

Domain 純粋関数 3 本 + Query UseCase + 既存画面へのパネル／バナーのみ。登録フロー（`Register*`）への Warnings 追記はしない。スキーマ変更・migration・`AppSection` 変更はしない。

### 決定2: UI は既存タブに埋め込む（独立タブ・通知センターは採らない）

| 機能 | 配置 |
|---|---|
| 精神手帳更新アラート | `DisabilityCertificateView` 内パネル（受給者証アラートと同型: しきい値・基準日・一覧） |
| フェースシート履歴＋差分 | `FaceSheetView` 内パネル（版一覧＋選択版と直前版の差分） |
| 障害種別整合警告 | `CertificateView` と `DisabilityCertificateView` のバナー（同一 Query） |

理由: 1 画面 1 責務は既存タブ内のサブパネルで満たせる。ナビ連番と `AppNavigationServiceTests` を動かさない。

### 決定3: `FindRenewalDue` の対象は精神手帳のみ

`Type == Mental` かつ `NextRenewalDate` があるものだけ。残日数 = 更新日 − `asOf` が 0〜`thresholdDays`。失効済み（残日数 &lt; 0）と日付なしは除外。残日数昇順。

身体・療育は `NextRenewalDate` があっても対象外。更新有無は自治体運用差があり公式一意に確定できないため、製品ポリシーとして精神に限定する（AC 名とも一致）。

しきい値既定は受給者証と同じ **30 日**。法令の「何日前通知」規定は無いため製品パラメータとする。

### 決定4: フェースシート差分は「選択版と直前版」

`FaceSheetDiff.Compare(older, newer)` は業務プロパティのみ比較する（`Id` / `RecipientId` / `CreatedBy` / `CreatedAt` / `ConcurrencyToken` は除外）。変更項目だけ `(PropertyName, OldValue, NewValue)` で返す。null と空文字は別値（正規化しない）。

UI: 履歴一覧で版を 1 つ選ぶと、`CreatedAt` 昇順で **直前の 1 版** との差分を表示する。最古版は差分なし。任意の 2 版比較 UI は YAGNI で採らない。

### 決定5: 障害種別整合は双方向・難病除外・警告のみ

対応表:

| 受給者証フラグ | 対応手帳 Type |
|---|---|
| Physical | Physical |
| Intellectual | Intellectual |
| Mental | Mental |
| Intractable | **検査しない**（対応手帳が制度上ない） |

現行手帳の定義: 利用者ごとに Type 別の最新 1 件（`IssuedDate` 降順、同日なら `CreatedAt` 降順）。

警告方向:

1. 証フラグ ON かつ対応手帳なし
2. 対応手帳あり かつ証フラグ OFF

Domain の戻り値は**構造化**する（種別＋方向）。表示文言は Application／App で組み立てる。保存は妨げない。`Register*` に Warnings を足さない（読み取り時再評価のみ）。

`FindEffectiveAsync` が null（有効な受給者証なし）のとき: 証側フラグはすべて OFF として扱い、**手帳あり・証なし**方向だけ検出しうる。専用の「証未登録」警告は出さない（障害種別整合の範囲外）。

### 決定6: Contract / ContractedProvider は責務分離（ADR 0053 想定）

ロードマップの「自社は Contract のみ」は **採らない**。

| 系統 | 正本 | 根拠 |
|---|---|---|
| 工賃の当月対象選定 | `Contract` | Phase 2 既存 |
| 請求 CSV 契約情報 | `ContractedProvider`（自事業所行必須） | ADR 0032 |

したがって **自事業所も請求用に `ContractedProvider` 行が必要**。`Contract` と内容が重なる二重入力は運用上の注意事項とし、**警告 UI は S4 外**（本スライスの成果は ADR + open-questions クローズ）。証訂正後の `CertificateId` staleness 自動修復も対象外。

次 ADR 番号は現状最大 0052 の次 **0053**（着手時に再確認）。

---

## 4. アーキテクチャ

```
DisabilityCertificateView
  → QueryDisabilityCertificateRenewalsUseCase
  → IDisabilityCertificateRepository.ListAllAsync
  → DisabilityCertificatePolicy.FindRenewalDue

FaceSheetView
  → QueryFaceSheetHistoryUseCase
  → IFaceSheetRepository.ListByRecipientAsync
  → FaceSheetDiff.Compare(previous, selected)

CertificateView / DisabilityCertificateView (banner)
  → QueryDisabilityConsistencyUseCase
  → ICertificateRepository.FindEffectiveAsync + IDisabilityCertificateRepository.ListByRecipientAsync
  → DisabilityConsistencyPolicy.Detect
```

依存方向は既存どおり `App → Application → Domain`。UI は `DbContext` を触らない。

### 4.1 Domain（新規）

| 型 | 契約 |
|---|---|
| `DisabilityCertificatePolicy.FindRenewalDue(certs, asOf, thresholdDays)` | 決定3。`CertificatePolicy.FindExpiring` と同型 |
| `FaceSheetDiff.Compare(older, newer)` | 決定4 |
| `DisabilityConsistencyPolicy.Detect(disabilities, currentCertificatesByType)` | 決定5。`(Category, Direction)` 等の構造化リスト。文言は上位層 |

### 4.2 Application

| 追加 | 内容 |
|---|---|
| `IDisabilityCertificateRepository.ListAllAsync` | アラート用全件 |
| `IFaceSheetRepository.ListByRecipientAsync` | 履歴（`CreatedAt` 昇順を契約で固定） |
| DTO 3 系統 | 更新アラート行／版一覧＋変更項目／整合警告 |
| Query UseCase 3 | 上記データフロー |

既存 `Register*` / `SaveFaceSheet` / `GetLatestFaceSheet` は変更しない。

### 4.3 Infrastructure

| 追加 | 内容 |
|---|---|
| `DisabilityCertificateRepository.ListAllAsync` | `AsNoTracking` |
| `FaceSheetRepository.ListByRecipientAsync` | Recipient 絞り込み＋`CreatedAt` 昇順 |

Migration 不要。

### 4.4 App

既存 ViewModel／View にパネル・バナーを追加。`CompositionRoot` に UseCase 登録。`MainWindow` / `AppSection` は変更しない。しきい値既定 30・F5 更新・アクセシビリティ既定は既存パターンに従う。

---

## 5. テスト方針

| 層 | 内容 |
|---|---|
| Domain | 3 純粋関数を Theory／テーブル駆動。境界（残日数 0 / threshold / 失効）、Mental 限定、難病除外、双方向、最古版は差分なし、null≠空文字 |
| Application | fake repo 拡張。3 Query UseCase |
| App | 既存 `CertificateViewModelTests` 同型でパネル／バナーの読込 |
| ナビ | `AppNavigationServiceTests` は**変更しない**（タブ追加しないため） |

意図的違反で赤になる歯は本スライスでは必須ではない（S2 の bulk スキャナとは性質が異なる）。

---

## 6. 文書成果物

| 文書 | 内容 |
|---|---|
| 本 spec | 設計正本 |
| 実装計画 | `docs/superpowers/plans/2026-08-17-phase4-s4-ui-complements.md`（本 spec 承認後） |
| ADR 0053（想定） | 決定6。結論→背景→選択肢→決定→影響 |
| `docs/open-questions.md` | 4 項クローズ |
| `CHANGELOG.md` | Phase 4 S4 節（実装完了時） |
| roadmap | S4 完了マーク（実装完了時） |

専用 acceptance doc は S2/S3a と同様、必須としない（CHANGELOG + ADR + open-questions で足りる）。

---

## 7. スコープ外

- 独立タブ／通知センター
- Contract / ContractedProvider 二重入力の警告 UI
- `Register*` への整合 Warnings 追記
- 療育等級 ComboBox 外部化、フェースシート独自項目
- ContractedProvider staleness の自動修復
- S3b（暗号化バックアップ）、S5（配布・初回セットアップ）

---

## 8. 参照

- `docs/superpowers/specs/2026-07-05-phase4-remaining-roadmap.md` §8.4
- `docs/decisions/0011-disability-certificate-and-face-sheet.md`
- `docs/decisions/0032-contract-information-as-individual-input.md`
- `docs/open-questions.md`（手帳更新／差分／整合／ContractedProvider）
- `src/Tsumugi.Domain/Logic/CertificatePolicy.cs`（アラート原型）
- `05_ClaudeCode_Phase2実装指示_工賃計算_Tsumugi.md` §3.4（本機能の Phase 2 外先送り経緯）
