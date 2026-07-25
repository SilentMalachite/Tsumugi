# 0031 R8.6 サービスコード表の独立取り込みを見送る

- 状態: 採用（2026-07-25）
- 関連: ADR 0020（出典と版管理）/ ADR 0024（国保連CSVと項目マッピング）/ ADR 0027（R6 基本報酬・サービスコード・地域単価の実値）
- 関連 open-question: 「[Phase3-3] R8.6 サービスコード表の一次資料 URL / SHA256 / 取得日 未確定」

## 結論

Phase 3-3 では **`Tsumugi.Infrastructure.Csv` に独立した R8.6 サービスコード表 seed（`service-code-r8-06.json`）と `IServiceCodeCatalog` を追加しない**。
請求CSVのサービスコードは、確定済み `ClaimBatch` の finalization snapshot v2 が持つ
`ClaimLines[].ServiceCode`（= 確定時に既存の制度マスタで解決済みの値）をそのままコピーする。

## 背景

Phase 3-3 の spec §5 と plan Task 3 は、`06_ClaudeCode_Phase3実装指示_国保連請求_Tsumugi.md` §4.3
（「CSV 項目構造は令和7年10月事業所編を基準にし、令和8年6月のサービスコード表を組み合わせる」）を根拠に、
CSV アセンブリ側へ R8.6 サービスコード表の seed を新設する計画だった。

実装着手時に判明した事実:

1. **CSV 生成はサービスコードを解決しない。** `field-mapping-r7-10.json` において、サービスコードを扱う項目は
   `provider:J121:03:007` の `copy(selector=ClaimServiceLine.ServiceCode)` だけであり、既に解決済みの値の転記である。
   カタログ参照を要する項目は存在しない。
2. **サービスコードの正本は既に存在する。** `src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json` に
   151 行が出典（documentId / sha256 / locator / evidenceRole）付きで登録済みで、ADR 0020・0027 の管理下にある。
3. **計画に記載された seed の実値は創作値だった。** plan Task 3 の例（`636211` / 566 単位）は、実在するコード体系
   （`462980` 等）と一致しない。令和8年6月サービスコード表の公表 URL・SHA256・取得日は現時点で未入手である。

## 選択肢

1. 計画どおり `service-code-r8-06.json` を新設し、実値が確定するまでプレースホルダを置く
2. `IServiceCodeCatalog` を作り、既存 claim-master の `service-codes.json` で裏打ちする
3. 独立カタログを作らず、確定済み snapshot の値をコピーする（本 ADR）

## 決定

**選択肢3**。理由:

- CSV 生成の要件を満たすのに独立カタログが不要である（背景1）。
- 未入手の一次資料に対してプレースホルダや推測値を seed へ投入することは、CLAUDE.md §ハード制約3
  （公式資料から一意に確定できない値は推測で埋めず `docs/open-questions.md` へ）に反する。
- 既存 claim-master と二重に持つと、出典・版・訂正関係の追跡点が増えるだけで整合性リスクが上がる（背景2）。
- 「確定時点で snapshot へ焼き込んだ値だけを正本として使う」という Phase 3-2 の doctrine
  （`GenerateClaimReportsUseCase` の XML doc）と一致する。確定後にマスタが差し替わっても、
  確定済み請求の CSV と帳票は同じ値を出す。

## 影響

- 新規ファイルなし。`Tsumugi.Infrastructure.Csv` は spec JSON 以外の制度データを持たない。
- R8.6 サービスコード表そのものの取り込みは **claim-master 側（ADR 0020 の管理下）の課題として残る**。
  一次資料が入手できた時点で `service-codes.json` の release chain へ追加し、別 ADR で実値を確定する。
- `docs/open-questions.md` の該当項目は「Phase 3-3 のスコープからは外し、claim-master 側の課題として継続」へ更新する。

## 一次資料

本 ADR は「独立 seed を作らない」決定であり、R8.6 サービスコード表の一次資料を確定するものではない。
既存の登録済み出典は ADR 0020 / `src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json` を参照。
