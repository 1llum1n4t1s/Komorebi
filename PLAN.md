# テーマ切り替え修正計画

## 問題
1. Light と Actipro Light を切り替えても見た目が変わらない
2. Light/Dark テーマで Fluent ネイティブの色ではなく独自カスタム色が使われている

## 原因
`Themes.axaml` が全テーマ共通で読み込まれ、`Styles.axaml` が `Brush.Window`, `Brush.FG1` 等の独自ブラシキーでUI色を固定上書きしている。Light/Actipro Light はどちらも `ThemeVariant.Light` を使うため、同じ独自色が適用されて見分けがつかない。

## 修正方針
テーマ色リソースを3つに分離し、選択テーマに応じて動的にスワップする。

### ファイル分割

| ファイル | 内容 | 読み込み |
|---------|------|---------|
| `Themes.axaml` (改修) | diff, conflict, link, inline-code 等のアプリ固有色 + Accent + Font | 常時 (App.axaml に静的定義) |
| `Themes.Default.axaml` (新規) | Window, TitleBar, ToolBar, Popup, Contents, FG1/FG2, Border, FlatButton, Badge, DataGridHeader の独自カスタム色 | Default テーマ選択時のみ動的ロード |
| `Themes.Fluent.axaml` (新規) | 同じブラシキーを Avalonia Fluent システムリソース (`SystemAltHighColor`, `SystemBaseHighColor` 等) にマッピング | Light/Dark/Actipro テーマ選択時のみ動的ロード |

### 変更ファイル一覧

1. **`src/Resources/Themes.axaml`** — 構造色(Window, FG, Border等)とそのブラシ定義を削除。アプリ固有色 + Accent + Font のみ残す
2. **`src/Resources/Themes.Default.axaml`** (新規) — 削除した構造色とブラシ定義をここに移動
3. **`src/Resources/Themes.Fluent.axaml`** (新規) — 構造ブラシを Fluent システムリソースにマッピング
4. **`src/App.axaml.cs`** — `ApplyThemeCore` でテーマに応じて Themes.Default / Themes.Fluent を動的にスワップ。フィールド `_structuralThemeResources` 追加

### Fluent システムリソースマッピング

| カスタムキー | 用途 | Fluent リソース |
|-----------|------|---------------|
| Brush.Window | ウィンドウ背景 | SystemAltHighColor |
| Brush.WindowBorder | ウィンドウ枠 | SystemChromeHighColor |
| Brush.TitleBar | タイトルバー | SystemChromeMediumColor |
| Brush.ToolBar | ツールバー | SystemChromeLowColor |
| Brush.Popup | ポップアップ背景 | SystemChromeMediumLowColor |
| Brush.Contents | コンテンツ領域 | SystemAltHighColor |
| Brush.Badge | バッジ背景 | SystemListMediumColor |
| Brush.BadgeFG | バッジ文字 | SystemBaseHighColor |
| Brush.FG1 | 主テキスト | SystemBaseHighColor |
| Brush.FG2 | 副テキスト | SystemBaseMediumColor |
| Brush.Border0 | 細い境界線 | SystemBaseLowColor |
| Brush.Border1 | 中間境界線 | SystemBaseMediumLowColor |
| Brush.Border2 | 太い境界線 | SystemBaseLowColor |
| Brush.FlatButton.Background | ボタン背景 | SystemChromeLowColor |
| Brush.FlatButton.BackgroundHovered | ボタンホバー | SystemChromeMediumLowColor |
| Brush.FlatButton.FloatingBorder | ボタン枠 | SystemBaseMediumLowColor |
| Brush.DataGridHeaderBG | DataGridヘッダ | SystemChromeLowColor |

### 検証手順

1. **ビルド確認** — `dotnet build` で 0 エラー・0 警告
2. **ユニットテスト** — `dotnet test` で 978 テスト全通過
3. **起動テスト** — アプリを起動して Default テーマが従来通りの独自カスタム色で表示されることを確認
4. **テーマ切り替えテスト** — 設定画面で以下の切り替えを実施し、各テーマで色が異なることを目視確認:
   - Default → Light (Fluent ネイティブの明るい色に変わる)
   - Light → Actipro Light (Actipro の色に変わる、Light と異なる見た目)
   - Actipro Light → Dark (Fluent ネイティブの暗い色に変わる)
   - Dark → Actipro Dark (Actipro の暗い色に変わる)
   - Actipro Dark → Default (独自カスタム色に戻る)
5. **アプリ固有色の確認** — どのテーマでも diff ビュー、conflict 表示、リンク色がテーマの Light/Dark に応じた適切な色で表示される
