# C# / Avalonia / EF Core 規律 — Tsumugi

> 本書は `CLAUDE.md` から参照される **コーディング規律の本体**。
> このプロジェクトで最も事故が起きやすい3点（C#の世代ズレ・AvaloniaへのWPF/MAUI混入・EF Coreの反映タイミングの暗黙化）を、具体名で「やらないこと」として固定する。
> 前提は **.NET 10 / C# 14 / Avalonia 11.x / EF Core 10.x**。指示が矛盾する場合は `CLAUDE.md` の §ハード制約が最優先。

なぜこの3点を明文化するか:

- C# は互換性重視で20年分の書き方が積層しているため、放っておくと **世代の古い構文**が混ざりやすい。
- Avalonia は WPF/MAUI と表面が似ているため、**別フレームワークのAPIが紛れ込みやすい**。
- EF Core はチェンジトラッキングにより **状態の反映タイミングが暗黙**になりやすい。

---

## 1. C# は現代構文のみ（レガシー構文を持ち込まない）

- **null 判定**は `is null` / `is not null` / `??` / `?.` を使う。`== null` や `ReferenceEquals(x, null)` を新規に書かない。
- **DTO・値の入れ物**は `record`（または `record struct`）で定義する。`Equals`/`GetHashCode`/`ToString` を手書きしない。プロパティ手書きのボイラープレートDTOを新規に作らない。
- **分岐**は可能な限り `switch` 式 ＋ パターンマッチで書く。`if`-`else` の連鎖や、`(T)x` / `as` による手動キャスト＋null確認を新規に増やさない。
- **I/O・DB・ファイルは原則 async**。同期版（`.Result` / `.Wait()` / `.GetAwaiter().GetResult()`）でブロックしない。`async`/`await` を最後まで貫く。
- 変換処理は **LINQ ＋ 再代入なし**で書く。ループ内でカウンタや累積変数を書き換える手続き的スタイルを新規に持ち込まない。
- 迷ったら **「.NET 10 / C# 14 で今書くならどう書くか」** を基準にする。古い記事由来の書き方を採用しない。

### 早見表

| 避ける（レガシー/別世代） | 使う（C# 14 / .NET 10） |
| --- | --- |
| `if (x == null)` / `if (x != null)` | `if (x is null)` / `if (x is not null)` |
| 手書きプロパティ＋`Equals`/`GetHashCode` のDTO | `record` / `record struct` |
| `if`-`else if` の型分岐、`(T)x`＋null確認 | `switch` 式 ＋ パターンマッチ |
| `.Result` / `.Wait()` でブロック | `await ...Async(...)` |
| `for`/`foreach` ＋ 累積変数の書き換え | `Select` / `Where` / `Aggregate` など LINQ |

---

## 2. Avalonia（WPF/MAUI の知識を混ぜない）

- ここは **Avalonia 11.x**。WPF・MAUI・UWP の API/名前空間/XAML 方言を**混入させない**。
- 具体的に避ける混入例: `System.Windows.*`（WPF）、`Microsoft.Maui.*`、WPFの `DependencyProperty` 流儀、WPF専用マークアップ拡張、`x:Static` の WPF 前提の用法など。
- バインディングは Avalonia の流儀（`CompiledBinding` 既定、`x:DataType` 指定）に従う。WPFの暗黙 `DataContext` 前提で書かない。
- コントロール名・添付プロパティは Avalonia のものを使う（例: `TextBlock`/`TextBox` の差異、`Classes` ベースのスタイリング）。WPFのStyle/Triggerの記法を持ち込まない。
- 不確かなときは**推測でAPIを書かず**、Avalonia 11 のドキュメント準拠を明示し、確証がなければ `docs/open-questions.md` に起票する。

### 混入チェック

| WPF/MAUI の癖（混入NG） | Avalonia 11 で正しい形 |
| --- | --- |
| `System.Windows.*` / `Microsoft.Maui.*` | `Avalonia.*` 名前空間 |
| WPF `DependencyProperty` 流儀 | Avalonia `StyledProperty` / `AttachedProperty` |
| 暗黙 `DataContext` 前提のバインディング | `x:DataType` 明示 ＋ `CompiledBinding` |
| WPF の Style Trigger | Avalonia の `Classes` / セレクタベースのスタイル |

---

## 3. EF Core（チェンジトラッキングの反映タイミングを明示する）

- エンティティを変更したら、**いつ DB に反映されるか（`SaveChangesAsync` の位置）をコード上で明示**する。「いつの間にか保存される/されない」を作らない。
- 読み取り専用クエリは `AsNoTracking()` を既定とし、追跡が必要な箇所だけ追跡する。意図を明示する。
- 追記型（append-only）方針のため、**既存行の更新でなく訂正レコードの追加**が原則（`CLAUDE.md` §コーディング規約）。トラッキングで既存エンティティを書き換える実装に流れない。
- 楽観的同時実行は更新トークン方式（SQLiteに `rowversion` なし）。並行更新時の競合検出を握りつぶさない。
- マイグレーションは生成物を必ず目視レビューしてからコミットする（自動生成を無検査で通さない）。

---

## レビュー時の依頼文（Codex／Claude Code 共通）

- 変更は「状態がどう遷移するか」ではなく **「何が何に変換されたか」** で説明させる。
- EF Core 絡みは「この変更はいつ DB に反映される想定か」を必ず明示させる。
- 詰まったら、原因が本書 1／2／3 のどれに該当するかを切り分けてから直す（C# は Elixir と比べ、世代ズレ・Avalonia/EF Core の知識の薄さ・規律を強制しない言語特性のため、古い/別フレームワークの書き方が紛れやすい）。
