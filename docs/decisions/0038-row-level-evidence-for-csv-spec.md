# 0038 CSV 仕様の判断に行単位の出典を付け、文書が変わったら名指しで落とす

- 状態: 採用（2026-07-25）
- 関連: ADR 0024（国保連CSVと項目マッピング）/ ADR 0033（登録済み一次資料からの仕様適合）/ ADR 0037（項目表の機械抽出）
- 一次資料: `common-r7-10`（共通編）/ `provider-r7-10`（事業所編）/ `r8-calculation-note`（留意事項通知）/ `r6-employment-guidance`（就労系留意事項通知）

## 結論

制度マスタ側（`ClaimMasters/Seed/*.json`）が行単位に持っている `sourceRefs` と**同型の出典**を
CSV 仕様側にも与える。置き場所は新規の証跡台帳 `spec-evidence-r7-10.json` で、
**各 ref は文書 ID と SHA-256 を持ち、本番の loader が `sources.json` の登録値との一致を検証する**。

## 背景

`sources.json` の `liveCheck` は「文書が変わった」までしか分からない。**どの判断の根拠が
再検証を要するか**が分からないため、施行分が更新されるたびに全体を読み直すことになっていた。

ADR 0037 の機械抽出は項目表（項目名・桁数・属性・項目の説明）を突合できるようにしたが、
次の 2 種類はカバーできない:

1. **本文（規則）由来の判断** — 引用規則（共通編 1.2.2(4)）・使用不可能文字（同 (3)①）・
   数値ゼロの設定（同 (4)）・ファイル名規則（同 1.2.1）・データ種別（同 1.6）・
   地域区分コード表（同 1.4）。項目表には無い。
2. **他文書に依拠する判断** — 訪問支援特別加算の算定回数・算定時間数が日次実績から
   導出できない根拠（留意事項通知 2(6)⑨）、施設外支援の累計が年度累計である根拠
   （就労系留意事項通知 1(1)①）。

## 決定

1. **証跡台帳** `spec-evidence-r7-10.json`。1 件（claim）は
   `claimId`（`rule:*` / fieldId / recordId）＋ `claimKind`＋`decision`（その出典で何を決めたか）＋
   `sourceRefs[]`（`documentId` / `sha256` / `locator` / `evidenceRole` / `supports` / 任意の `quote`）。
   `quote`（原文引用）を持たせるのは、文書が差し替わったときに「まだ同じことを言っているか」を
   人がその場で判断できるようにするため。
2. **検証は本番の loader で行う**（`CsvSpecificationCatalog.ValidateEvidence`）。テストだけに置くと
   「テストを消せば通る」状態になる。検証内容:
   - `documentId` が `sources.json` に登録されていること
   - **`sha256` が登録値と一致すること** → 文書を差し替えて `sources.json` を更新すると、
     その文書に依拠する claim が「根拠を再検証せよ」として fail-close し、例外が claimId を名指しする
   - `claimKind` / `evidenceRole` / `supports` が閉じた語彙であること
   - `field` / `record` の claimId が実在する項目・レコードを指すこと
3. **機械的に検証できる出典は全件検証する。**
   - `p.N;item=M` 形式の locator は ADR 0037 の抽出結果（項目単位の `sourcePage`）と突合する。
   - `generatorRule` に埋め込まれた `source=doc:pNN:itemNN` アンカー（**372 件**）も同様に突合する。
     事業所編の 355 件が一致し、共通編の 17 件は抽出未対応のため未検証（既知ギャップとして件数を固定）。
4. **出典が無い対象は隠さず宣言する**（`knownGaps`）。件数の上限をテストで固定し、増やすときに
   意図的な判断を強いる。現状 3 件（共通編の外側レコード19項目／例外利用日4項目の
   crossFieldGroup 判断／Phase 3-1 に status を missing とした項目群の判断根拠）。
5. 留意事項通知 2 件を CSV 側の `sources.json` にも登録した（SHA-256 は制度マスタ側と同一）。
   両レジストリの一致は `SpecEvidenceLedgerTests.Documents_registered_in_both_registries_agree` が固定する。

## 影響

- 初期投入は 14 claim（横断規則 6・項目 7・レコード 1）。今回のフェーズで一次資料まで遡って
  確認した判断がすべて構造化された出典を持つ。
- ADR 0024 の出典カタログ表に 2 件追記した（同 ADR は出典カタログの正本で、テストが突合している）。
- **副産物として実在のレジストリ不整合が判明し、解消した**: `r8-grant-decision-administration-202606` は
  CSV 側が厚労省 PDF（現在 404・historical としてバイト列保持）、claim-master 側が北九州市の再配布 PDF を
  指しており、**同じ ID で別ファイルを検証していた**。利用者判断により**正本を厚労省発出 PDF に統一**し
  （2026-07-25）、page 付けの同一性は「正本について記録済みの physical pages 233〜235 のテキスト SHA-256 を
  再配布 PDF が再現すること」＋「page 数 262 の一致」で確認した。除外リストは持たない。
- 歯の確認: locator の頁を 1 ずらすと突合テストが RED（`p.72` vs 抽出 `p.73` を名指し）。
  `sources.json` の SHA-256 を書き換えると loader が claimId を名指しして fail-close。
