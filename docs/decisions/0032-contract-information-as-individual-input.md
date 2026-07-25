# 0032 明細書「契約情報」は導出せず契約ごとに個別入力する

- 状態: 採用（2026-07-25）
- 関連: ADR 0024（国保連CSVと項目マッピング）/ ADR 0026（claim-batch snapshot）/ ADR 0030（入力UIの責務）
- 関連 open-question: 「[Phase3-3/blocking] 明細書「契約情報」レコードと開始年月日に必要な契約情報が snapshot v2 に無い」（本ADRで解消）

## 結論

明細書「契約情報」レコード（`provider:J121:05`）と開始年月日（`provider:J121:02:008`）が必要とする値は、
**受給者証「サービス事業者記入欄」（`ContractedProvider`）の個別入力**を正本とし、日次記録から導出しない。
確定時に `ClaimFinalizationContractedProviderSnapshot` として snapshot へ焼き込み、CSV はそれだけを読む。

| CSV 項目 | 正本 | 入力面 |
|---|---|---|
| `provider:J121:05:008` 契約支給量 | `ContractedProvider.ContractedSupplyDays` | `CertificateView` |
| `provider:J121:05:009` 契約開始年月日 | `ContractedProvider.ContractDate` | `CertificateView` |
| `provider:J121:05:010` 契約終了年月日 | `ContractedProvider.TerminationDate` | `CertificateView` |
| `provider:J121:05:011` 事業者記入欄番号 | `ContractedProvider.CertificateEntryNumber` | `CertificateView` |
| `provider:J121:02:008` 開始年月日 | `ContractedProvider.FirstServiceDate`（本ADRで新規追加） | `CertificateView` |

## 背景

Phase 3-3 の初期実装は 2 つの誤りを抱えていた。いずれも Codex レビューで検出した。

1. **`provider:J121:05` を 0 レコードで出力していた。** 設計spec §3.4 の表がこのレコードを「経過措置」と
   ラベルしていたのを検証せずに受け入れたが、実際の項目名は契約支給量・契約開始年月日・事業者記入欄番号で、
   **契約情報レコード**であり受給者ごとに必須である。必須レコードが丸ごと欠落した CSV を出していた。
2. **開始年月日を当月の日次記録の最小サービス提供日で代用していた。** `field-mapping-r7-10.json` の
   generatorRule は `min(selector=DailyRecord.ServiceDate; filter=first-service-date-in-effective-continuous-contract)`
   だが、確定 snapshot は当月分の日次記録しか持たない。前月以前から継続する契約では誤った日付になる
   （＝継続利用者という最も多いケースで誤る）。

## 選択肢

1. 日次記録から導出を続ける（＋当月最小日フォールバック）
2. 契約情報を snapshot へ載せ、値は既存の `ContractedProvider` 入力を正本にする（本ADR）
3. 月次の請求固有入力（`ClaimInput`）へ 5 項目を追加し、毎月入力させる

## 決定

**選択肢2**。理由:

- 契約支給量・契約日・契約終了日・事業者記入欄番号は**すでに `ContractedProvider` に存在し、
  受給者証の「サービス事業者記入欄」として個別入力できる**（`CertificateEntryNumber` の doc-comment は
  `J121:05:011` を明記している）。新しい入力面を作る必要はない。
- 契約内容は契約単位の事実であり、月次請求入力（`ClaimInput`）の性質ではない。毎月の再入力は
  転記ミスを増やす。
- 初回サービス提供日だけがどこにも無かったため `ContractedProvider.FirstServiceDate` を追加した。
  契約ごとに実情が異なり導出できないため、`contractDate` 以降であることだけを検証して個別入力とする。
- 確定時に snapshot へ焼き込むことで、Phase 3-2 の doctrine（確定済み請求は現行エンティティを
  読み直さず snapshot だけから決定論的に再現する）を保つ。

## 影響

- `ContractedProvider.FirstServiceDate: DateOnly?` を追加（migration `Phase33ContractedProviderFirstServiceDate`）。
  `contractDate` より前の日付は拒否する。
- `ClaimFinalizationSnapshot.ContractedProvider`（末尾の任意プロパティ）を追加。
  **Phase 3-3 より前に確定した snapshot は本プロパティを持たないため null になり、CSV 生成は fail-close する。**
  過去分を CSV 化するには再確定が必要。
- `OperationLocalSnapshotReader` が「サービス事業者記入欄」から**自事業所（事業所番号一致）の行**を選ぶ。
  該当行が無ければ null のまま確定を通し、CSV 側で fail-close する（確定操作は妨げない）。
- `field-mapping-r7-10.json` の `provider:J121:02:008` を `generated`（min 導出）から
  `missing`（`ContractedProvider.FirstServiceDate` / `uiSurface: CertificateView`）へ変更した。
  これにより readiness gate が確定前に未入力を検出する。generatorRule は 375 → 374 件、
  missing は 30 → 31 件になり、`CsvSpecificationCompletenessTests` の期待値を更新した。
- `min(selector=...)` 形式の評価器を撤去した（`min(fields=...)` のみ残る）。導出への逆戻りは fail-close する。
