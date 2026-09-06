# Komorebi の設計

Komorebi は SourceGit を基にした、Windows・macOS・Linux 向けの Git GUI クライアントである。Git CLI を通じてローカルリポジトリとリモートを操作し、履歴・差分・作業ツリーを可視化する。利用方法は [README.md](README.md)、開発規約と検証コマンドは [AGENTS.md](AGENTS.md)、実装上の注意は [docs/PITFALLS.md](docs/PITFALLS.md) を参照する。

## コンポーネントと境界

| コンポーネント | 責務・境界 | 主な実装 |
|---|---|---|
| アプリ起動 | 更新フック、データ保存先・ログ初期化、通常 GUI と Git のリベースエディタ起動の分岐 | `src/App.axaml.cs`、`src/App.Commands.cs` |
| UI | Avalonia の View とバインディング、入力・描画・ウィンドウ操作 | `src/Views/`、`src/Converters/` |
| アプリ状態 | Launcher のタブ、Repository の履歴・作業コピー・スタッシュ、Popup の検証と操作進捗 | `src/ViewModels/` |
| Git 実行 | 引数・作業ディレクトリ・SSH 環境の構築、プロセス実行、出力解析・中断 | `src/Commands/Command.cs` と各派生コマンド |
| データモデル | コミット・参照・差分・グラフ、ファイル監視、リポジトリ UI 状態 | `src/Models/` |
| OS 連携 | データ保存先、外部ツール・ターミナル等のプラットフォーム差異 | `src/Native/OS.cs` の `IBackend` と各 OS 実装 |
| AI 生成 | サービス設定と差分取得ツールを使ったコミットメッセージ生成 | `src/AI/Agent.cs`、`OpenAISdkStrategy.cs`、`AnthropicHttpStrategy.cs`、`ChatTools.cs` |
| 配信 | Windows のローカル署名と、他 OS・standalone の CI パッケージ作成、R2 配信 | `scripts/release-local.ps1`、`.github/workflows/release.yml` |

## データフローと寿命

1. View の操作を ViewModel が受け取り、Git コマンドを構築して実行する。Popup は入力検証と進捗ログを担当し、中断可能な処理では操作スコープのトークンをコマンドに渡す。
2. Git の出力を Models に変換し、Repository が履歴・ブランチ・作業コピー等を更新する。ファイル監視も更新を起動する。非同期更新ではキャンセルを確認し、UI スレッドに結果を反映することで古い取得結果や Close 後の反映を防ぐ。
3. Repository は Histories・WorkingCopy・StashesPage の VM を保持する。Launcher と Repository の `ContentControl + DataTemplate` が選択中の VM を表示する。詳細・比較の独立ウィンドウは対応する VM を表示する。
4. 全体設定は `Preferences` が DataDir の `preference.json` に保存する。Git 固有の設定は Git config とコマンド層を通じて扱い、UI 状態とは分離する。
5. AI 生成では変更一覧と現在ブランチ等をプロンプトへ組み込み、`ChatTools` 経由で差分を取得して設定先サービスへ送る。`Agent` はプロバイダー別 Strategy に委譲し、結果をコミットメッセージ欄へ返す。

## 不変条件と採用済み設計判断

- **Git CLI を操作の正本にする。** GUI は Git の結果と終了状態に従う。Git の導入やバージョン差への対応が必要になる一方、既存の認証・リポジトリ設定を利用できる。
- **表示を切り替えても VM の状態を保持する。** View の生成・再利用は ContentControl に任せ、上流と整合する構造を使う。全 View の常駐キャッシュに伴うレイアウト問題を避ける代わりに、保持すべき状態は VM 側が担う。
- **SSH 鍵選択と引数の引用を分離する。** リモート個別 → グローバル → ssh-agent/config の順に解決する。旧 `__NONE__` 値の明示的なフォールバック拒否は読み取り互換として保持する。SSH のシェル用引用と Git 引数用の引用は別の処理である。
- **プロセスの中断を共通化する。** `Native.CommandCancellation` が OS 差を吸収し、キャンセル済みの結果は成功として扱わない。関連契約は `tests/Komorebi.Tests/Commands/CommandCancellationTests.cs` と `ReadToEndCancellationTests.cs` が検証する。
- **バイナリ表示は必要な範囲を読む。** `Models.BinaryFile` はバッファを用い、履歴から抽出した一時ファイルの削除も Dispose に結び付ける。全内容の常時メモリ保持を避ける代わりに、表示中のファイル資源を管理する。
- **起動診断は通常ロガーから独立させる。** `StartupDiagnostics` の同期ログと到達ステージのマーカーで、ロガー初期化前やログを書けない終了を補足する。最初のアイドル後も60秒観察し、正常終了経路ではマーカーを消して誤検出を抑える。
- **更新先を固定する。** `Preferences.CanonicalUpdateBaseUrl` を正本とし、JSON からの更新 URL 上書きを受け付けない。Velopack の取得・適用経路と R2 の配信経路を接続する。Windows は対話認証が必要な署名のためローカルで配信し、CI は Windows 更新フィードを生成しない。両経路の削除処理は他方の manifest を保持対象へ取り込む。
- **テーマと翻訳を資源として切り替える。** `Themes.axaml` の色・ブラシと各 locale 辞書を利用し、英語辞書を翻訳キー集合の基準とする。差分エディタは追跡済みの `depends/AvaloniaEdit` を利用する。

上流との機能差と同期時の採否は [docs/UPSTREAM-SYNC.md](docs/UPSTREAM-SYNC.md) に記録される。上流追従の保守性と Komorebi 固有機能の維持を両立するため、採否の作業規約は AGENTS.md に集約する。
