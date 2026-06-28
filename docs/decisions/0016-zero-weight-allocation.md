# ADR 0016: ゼロ重み時の按分挙動

## 結論
- `AllocationPolicy.Allocate(shares, totalYen, rounding, remainder, officeReserveKey)` における重み総和 `totalWeight <= 0` のケースを、`RemainderPolicy` ごとに決定論で分岐させる。
- 分岐定義:
  1. `totalYen == 0` → 全員 0 円を返す（既存挙動を維持）。
  2. `totalYen > 0` ∧ `remainder == RemainderPolicy.ReserveToOffice` → 全額 `totalYen` を `officeReserveKey` に集約。他の `shares` は 0 円。
  3. `totalYen > 0` ∧ `remainder == RemainderPolicy.LargestRemainder` → `InvalidOperationException` を投げる（メッセージは「配分対象の総重みが 0 のため、原資 X 円を最大剰余法で配分できません。事業所留保へ切り替えるか、原資を 0 円に設定してください。」）。
- 上記により AC2-4「按分方式で Σ配分＝原資 が常に成立」を保つ：配分が返る場合は必ず Σ=原資、決定論的配分が定義できないケースは例外で拒否する（vacuously に成立）。

## 背景
- Codex レビュー H-2 で「全員 0 分」入力時に `AllocationPolicy.Allocate` が `Σ配分 = 0 ≠ 原資` を返してしまうことが指摘された。
- 仕様 `05_ClaudeCode_Phase2実装指示_工賃計算_Tsumugi.md:91` は按分方式の **配分後合計＝原資** を不変条件として要求。
- 仕様 `05_ClaudeCode_Phase2実装指示_工賃計算_Tsumugi.md:122` は AC2-4 の試験ケースに「全員 0 分」を明記。
- B 型工賃は労働対価であるため「働かなかった人へ均等に支払う」既定はとらない。一方で `WageFund` が既に正の値で登録されている以上、整合性として Σ=原資 を保つ必要がある。

## 選択肢
1. **重みゼロを等価重みとみなしてフォールバック均等割** — 「全員 0 分でも一律支給」となり、B 型の労働対価原則に反する。出席者ゼロでも資金が消える。却下。
2. **常に事業所留保（policy を無視して office に全額）** — `RemainderPolicy.LargestRemainder` を明示選択しているユーザの意図を無視する。policy が事実上 1 種類に退化する。却下。
3. **policy 別に分岐（採用）** — `ReserveToOffice` 選択時は全額留保（自然な拡張）。`LargestRemainder` 選択時は決定論的順序が定義できないため例外で拒否し、UI から原資修正 or policy 切替を促す。policy の意味論を保持しつつ Σ=原資 不変条件を守れる。
4. **暗黙の `ReserveToOffice` フォールバック** — 仕様にないルールを暗黙導入することになり、設定意図と挙動の乖離を生む。却下。

## 決定
選択肢 3 を採用。`AllocationPolicy.Allocate` の `totalWeight <= 0m` 分岐を上記「結論」の 3 ケースで実装する。例外メッセージは結論セクションに明記した文言で固定する（テストでメッセージパターンを検証）。

## 影響
- `AllocationPolicy.Allocate` の例外契約に `InvalidOperationException`（重み 0 ∧ 原資>0 ∧ LargestRemainder）を追加。呼び出し側（`HourlyWageStrategy`, `EqualWageStrategy`, `CalculateWagesUseCase`）は伝搬のみで追加処理なし。
- UseCase 経由で UI に届く `InvalidOperationException` は既存の「`WageFund` 必須」「`WageSettings` 未登録」と同型で扱える。
- 既存テスト `AllocationPolicyTests.All_zero_weights_yield_zero_amounts` および `HourlyWageStrategyTests.All_zero_minutes_yields_all_zero` は意味分割して書き換える（旧期待は本 ADR で正式に上書き）。
- `EqualWageStrategy` は「出席者ゼロ ∧ 原資>0」で同じ例外を伝搬する。
- 将来 `ReserveToOffice` を既定にしたい事業所が現れた場合は `WageSettings.Remainder` を切り替えるだけで挙動が決まる（ADR 0012 既存スコープで吸収）。
