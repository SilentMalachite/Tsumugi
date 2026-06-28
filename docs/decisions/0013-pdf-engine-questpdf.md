# ADR 0013: PDF 生成エンジン（QuestPDF）の採否

## 結論
- QuestPDF Community License を採用する（採用条件: 事業所の年商が QuestPDF の Community 閾値を下回ること。閾値超過時は Avalonia 印刷経路へフォールバック）。
- フォールバック手順: View に `Print` コマンドを置き、Avalonia の `PrintDialog` → ビジュアル Print に切り替える（Reporting 層の IF を切り替える）。
- ライセンス設定は `Tsumugi.Infrastructure.Reporting.QuestPdfLicenseConfigurator` に集約し、構成は `appsettings`/環境変数ではなく**コードでハード設定**（オフライン保証）。

## 背景
- 仕様 §4.4: 「QuestPDF を採用。ライセンス条件を確認し ADR 化。不可なら Avalonia 印刷経路にフォールバック」。
- 完全オフライン制約のため、PDF 生成は決定的・依存ファイル最小・日本語フォント対応必須。

## 選択肢
1. QuestPDF（Community License）→ **採用**。
2. iText7 → AGPL/商用の二重ライセンス、商用が高額。却下。
3. PdfSharpCore → 機能/レイアウト表現力が弱い。却下。
4. Avalonia 印刷のみ → フォールバックとして保持。

## 決定
QuestPDF Community License を選択する。事業所が Community 閾値を下回ることを導入前に確認する運用とし、閾値超過が判明した場合は選択肢 4（Avalonia 印刷経路）へ切り替える。ライセンス確認はネットワーク接続を必要としない（コードでハード設定するため、ライセンスサーバへの問い合わせは発生しない）。

## 影響
- `Tsumugi.Infrastructure.Reporting` を新 csproj として分離（App 直リンクで合成）。
- テストは抽出テキスト + 合計金額で検証（バイト一致は環境差で揺れるため避ける）。
