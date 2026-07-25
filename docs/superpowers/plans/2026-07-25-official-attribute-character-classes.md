# 属性区分の文字種強制 と コントロールレコード市町村番号 Implementation Plan

**Goal:** 公式システムでの取込失敗に直結しうる 2 件を閉じる。
(1) 共通編 1.3.2(1)③ の属性区分（英数／数値／コード値／漢字）に基づく文字種規則を `CsvCellEncoder` が強制する。
(2) `common:outer:control:006`（市町村番号）の設定値を一次資料で確定し、証跡台帳に載せる。

**根拠（本計画の全判断はここから）:** `common-r7-10`（SHA-256 `f6932c52a4fd1044cbdd5aa752baf52e1c2e1eda106d9fa54991423fbd04a9a9`、
2026-07-25 に登録 URL から再取得して一致を確認）。

- 物理 6 頁 コントロールレコードフォーマット 項番 6:
  「送付元または送付先が市町村の場合は市町村番号を設定、市町村以外の場合は **0 を設定する**」
- 物理 10 頁 **1.3.2(1)③ 属性**（open-questions が「1.2.3③」と記していたのは誤り。正しくは 1.3.2(1)③）:
  「**特に記載が無い限り**、以下の形式でデータを設定することを表す」
  - 英数: 「半角の英字、数字、カナ文字１文字をそれぞれ 1 バイトで表す。**半角の英小文字は使用できない**」
  - 数値: 「0，1，2，～，9 の数字 1 桁をそれぞれ 1 バイトで表す。マイナスの数字の場合は左側に半角のマイナス（"-"）を付加する」
  - コード値: 「0，1，2，～，9 の数字 1 桁をそれぞれ１バイトで表す」
    「※1 "0" が 1 桁以上、最大桁以下で設定されている場合は未設定として取り扱う」「※2 必須項目が未設定の場合はエラーとする」
  - 漢字: 「漢字 1 文字をそれぞれ 2 バイトで表す」
- 物理 11 頁 同③ 末尾:
  「・「英数」項目には**漢字（2 バイトコード）を混在させない**。」
  「・「漢字」項目には**半角の英字、数字、カナ文字（1 バイトコード）を混在させない**。」

## 実装前に判明した仕様違反（本計画の主目的）

`provider:J121:01:008 支給決定者氏名カナ` は公式属性 **英数**（＝半角）なのに、実装は
`Recipient.KanaName` をそのまま出しており、全角カナ氏名がそのまま CSV に載っていた
（golden `csv-golden-cjk.csv` に `"ツムギタロウ"` として固定されている）。
これは 1.3.2(1)③「「英数」項目には漢字（2 バイトコード）を混在させない」に反する。
`maxBytes=25`（奇数）も 1 バイト／文字の項目であることを裏づける（2 バイト／文字なら奇数長になりえない）。

## Global Constraints

- .NET 10 / C# 14、`<Nullable>enable</Nullable>` ＋ `TreatWarningsAsErrors`
- 制度実値・CSV 仕様値を C# に置かない（CLAUDE.md §ハード制約3）。属性区分は spec JSON が運ぶ
- 出力は決定論。macOS / Windows で同一バイト（ハード制約6）
- 例外に氏名・受給者証番号・値そのものを載せない（ハード制約4）
- fail-close 優先。推測の丸め・推測の変換を入れない
- 1 コミット = 1 論理変更

---

## Task 1: spec が属性区分を運ぶ

- [x] **Step 1 (RED)**: `ItemTableCrossCheckTests.Every_official_attribute_matches_the_extraction` を追加。
      運用 spec の `officialAttribute` と機械抽出（ADR 0037）の完全一致を要求する
      （現状は spec 側にキーが無いので RED）。公式表が属性欄を空にしている
      `common:outer:data:003` だけ理由つきで宣言する。
- [x] **Step 2**: `CsvSpecificationCompletenessTests` の必須プロパティ列に `officialAttribute` を追加。
- [x] **Step 3 (GREEN)**: `CsvFieldSpecification` に `string OfficialAttribute` を **required** で追加
      （`RespectRequiredConstructorParameters` + `UnmappedMemberHandling.Disallow` により、
      443 項目すべてが宣言しないと読み込み自体が落ちる）。
- [x] **Step 4**: `build/sync_official_attributes.py` を追加し、抽出結果から運用 spec へ属性区分を注入する
      （次の施行分でも「抽出をやり直して差分をレビュー」で回る）。`common-r7-10.json` / `provider-claim-r7-10.json` を更新。
- [x] **Step 5**: `dotnet test` で Task 1 分が緑。

## Task 2: encoder が属性区分の文字種を強制する

- [x] **Step 1 (RED)**: `CsvCellEncoderTests` に追加。
      - 英数項目に全角カナ → fail（`InvalidCharacterForOfficialAttribute`）
      - 英数項目に半角英小文字 → fail
      - 英数項目に半角カナ・半角英大文字・数字・スペース → 通る
      - 漢字項目に半角数字／半角英字／半角カナ → fail
      - 漢字項目に全角文字のみ → 通る
      - コード値項目に非数字 → fail（従来 `dataType=code` は無検証だった）
      - 未知の属性区分 → fail（`UnknownOfficialAttribute`）
- [x] **Step 2 (GREEN)**: `CsvOfficialAttribute`（Writer）に 4 種の閉じた語彙と文字集合判定を実装し、
      `CsvCellEncoder` から呼ぶ。既存の `dataType` 検証は残す（属性検証と矛盾しない上位互換）。
- [x] **Step 3**: `CsvEncodingReason` に `InvalidCharacterForOfficialAttribute` / `UnknownOfficialAttribute` を追加。

### 数値属性のマイナス符号について（意図的に採らない）

仕様は数値属性に左側の半角マイナスを許す（※2）。しかしどの項目が負値を取り得るかは項目単位で
宣言されておらず（※4 は「マイナス値を取り得る項目」という区別を前提にする）、本アプリの生成器は
負値を出さない。**許容範囲を広げず現状の「ASCII 数字のみ」を維持**し、負値が来たら fail-close する。

## Task 3: 氏名カナを半角へ（宣言された変換）

- [x] **Step 1 (RED)**: `OfficialAttributeConformanceTests` を追加。
      - 全角カナ氏名が半角カナで出力される（`ツムギタロウ` → `ﾂﾑｷﾞﾀﾛｳ`、2 バイト文字を含まないので引用符も付かない）
      - 半角化できない文字（ひらがな・漢字・康熙部首）は fail-close し、例外に fieldId だけが載る
- [x] **Step 2 (GREEN)**: `HalfWidthKana`（Generation）を追加。写像表は手書きせず、
      半角側（ASCII 0x20〜0x7E ＋ U+FF61〜U+FF9F）の各文字の NFKD から**逆写像を実行時に導出**する。
      入力を NFKD 正規化して 1 文字ずつ引き当て、引けない文字があれば fail-close。
- [x] **Step 3**: `ClaimCsvModelPath` に単位接尾辞 `halfWidthKana` を追加（閉じた語彙 `KnownUnitSuffixes` に登録）。
- [x] **Step 4**: `field-mapping-r7-10.json` の `provider:J121:01:008` の `modelPath` を
      `Recipient.KanaName:halfWidthKana` に変更（`Recipient.KanaName` 素の path は帳票側が使うため残す）。
- [x] **Step 5**: golden を `UPDATE_GOLDEN=1` で再生成し、差分をレビューする。
      変わるのは `csv-golden-cjk.csv` のみ（他 3 種の fixture は既に半角カナ）。

## Task 4: コントロールレコードの市町村番号を証跡化する

- [x] **Step 1 (RED)**: `OfficialAttributeConformanceTests` に外側フレームの固定を追加。
      `006`（市町村番号）= `0` / `007`（事業所番号）= 事業所番号 / `008`（都道府県番号）= `0`。
- [x] **Step 2**: 実装は変更しない（`const(value=0)` が公式指示どおりであることを確認済み）。
- [x] **Step 3**: `spec-evidence-r7-10.json` に claim を追加。
      - `common:outer:control:006` / `common:outer:control:008` — 項目の内容欄が 0 を明記
      - `rule:code-value-zero-is-unset` — 一般規則（全桁 0 は未設定・必須未設定はエラー）を記録し、
        属性の説明が「特に記載が無い限り」であるため**項目の内容欄が優先する**ことを明示。
        よって全桁ゼロの一律 guard は入れない（入れると公式指示に反して自作のエラーになる）
      - `rule:official-attribute-character-classes` — 1.3.2(1)③ の文字種規則
      - `provider:J121:01:008` — 英数属性のため半角カナで出す
      `p.N;item=M` 形式の locator は `SpecEvidenceLedgerTests` が抽出結果の頁と突合する。

## Task 5: ドキュメント

- [x] **Step 1**: ADR 0043 を追加（属性区分の文字種強制／項目記載の優先／氏名カナの半角化／
      NFKD 由来の写像に依存すること）。
- [x] **Step 2**: `docs/open-questions.md` の 2 件（属性区分の文字種強制・市町村番号）をクローズ。
      節番号の誤記（1.2.3③ → 1.3.2(1)③）も訂正する。
- [x] **Step 3**: `docs/phase3-3-acceptance.md` に §13 として追記（発見した仕様違反と修正、golden の変化）。
- [x] **Step 4**: CLAUDE.md ハード制約3 の一文を更新（属性区分まで spec が運ぶこと）。

## Task 6: 品質ゲート

- [x] `./build/ci.sh` を緑にする（format / warnings-as-errors / test / coverage / arch / offline）。
