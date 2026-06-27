# ADR 0006: 事業所体制の加算フラグは「拡張可能な文字列キーのマップ」として保持する

- 結論: `OfficeCapability.Flags` を `IReadOnlyDictionary<string,bool>` として保持し、SQLite には JSON テキスト列で永続化する。キーの正式コード体系・単位数は**フェーズ1では未確定**とし、`docs/open-questions.md` で管理する。
- 背景: 報酬告示の加算コードはフェーズ3で公式仕様と突合して確定する必要がある。フェーズ1で列挙体に固定すると、フェーズ3で破壊的変更が避けられない。
- 選択肢:
  - (a) `Dictionary<string,bool>`＋JSON保存（採用）。
  - (b) 列挙体 `AdditionFlag : long` でビットフラグ。
  - (c) `OfficeCapabilityFlag` テーブルを正規化。
- 決定: (a)。フェーズ3で正式コードが揃った時点で、(c) への移行を Application 層の DTO で吸収する。
- 影響:
  - フラグ追加はデータ駆動（マイグレーション不要）。
  - クエリ性能はフェーズ1の規模では問題にならない（事業所体制は少行）。
  - 既知キーのドキュメント化は `docs/open-questions.md` で行う。
