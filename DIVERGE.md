# DIVERGE.md — Komorebi と Upstream SourceGit との意図的乖離

このドキュメントは、Komorebi が Upstream `sourcegit-scm/sourcegit` と意図的に乖離している箇所を一覧化する。
cherry-pick による上流追従戦略 (`Upstream-Faithful Policy`、CLAUDE.md 参照) を維持する上で、
3-way merge 衝突発生時にどちらを優先するかの判断材料として使う。

毎回のレビューや cherry-pick バッチで参照すること。新しい意図的乖離が発生したらこのファイルを更新する。

---

## 1. 通知・例外ハンドリング

| 領域 | Upstream | Komorebi | 理由 |
|---|---|---|---|
| 例外通知 API | `Models.Notification.Send` | `App.RaiseException` | 単一エントリポイント化＋ AOT 最適化。コンテキスト引数を Repository パスに統一 |
| ロガー | NLog ベース | **SuperLightLogger** | NLog 互換 API + AOT セーフ + パッケージ軽量化 |

## 2. ウィンドウ・UI 構造

| 領域 | Upstream | Komorebi | 理由 |
|---|---|---|---|
| Toolbar | `RepositoryToolbar` 個別 | **WelcomeToolbar 統合**（Repository 直下に展開） | Welcome / Repository ビュー間のチップ重複排除 |
| Avatar 描画 | キャッシュなし毎フレーム new | **フォールバック描画キャッシュ**（`_cachedFallbackText` / `_cachedFallbackBrush`） | スクロール時のアロケーション圧軽減 |
| ChromelessWindow | 共通機能集約 | 共通機能集約 + `Screens.Changed` 監視で再最大化 | リモートデスクトップ解像度変化への追従 |

## 3. SSH キー管理

| 領域 | Upstream | Komorebi | 理由 |
|---|---|---|---|
| UI | リモート設定ダイアログにフィールド追加 | **SSHKeyPicker 専用 UserControl** | 「None / Key / CustomKey / Browse」を一貫した UX で扱う |
| Sentinel | なし | **`__NONE__` 凍結レガシー定数** (`LegacySSHKeyOptOutSentinel`) | 旧バージョンが書き込んだ値を尊重する後方互換 |
| 値検証 | エスケープのみ | **`IsSafeSshKeyPath` で実在 + 危険文字チェック** | `.git/config` 経由のシェルインジェクション多層防御 |
| GIT_SSH_COMMAND | 親環境を尊重 (C 案) | **per-remote/global 設定が明示的なら親環境より優先** | UI 設定がサイレント無視される問題の修正 |

## 4. AWS CodeCommit 対応

| 領域 | Upstream | Komorebi | 理由 |
|---|---|---|---|
| URL 形式 | なし | **HTTPS / SSH / GRC の 3 形式パース** (`Remote.cs`) | 国内 AWS ユーザー向け |
| Console URL 変換 | なし | `TryGetVisitURL` / `TryGetCreatePullRequestURL` で 3 形式を AWS Console URL に変換 | AWS マネジメントコンソールへの遷移 |

## 5. AI 統合

| 領域 | Upstream | Komorebi | 理由 |
|---|---|---|---|
| Provider | OpenAI / AzureOpenAI / Gemini | **+ Anthropic** (`AnthropicHttpStrategy`) | Claude モデル対応 |
| API Key 保護 | 平文保存 | **AES-GCM 暗号化 + Windows DPAPI** | 同一マシン他ユーザーからの鍵窃取防御 |
| Anthropic ループ | なし | **`MaxToolCallIterations = 20`** | プロンプトインジェクションでの無限ループ防止 |

## 6. ローカリゼーション

| 領域 | Upstream | Komorebi | 理由 |
|---|---|---|---|
| 進捗ロケール | Linux のみ `LANG=C` | **全 OS で `LANG=C / LC_ALL=C`** | macOS 日本語ロケールでの偽エラー報告防止 |
| Locales | en, zh_CN, zh_TW, etc. | + ja_JP, fil_PH 等の追加 | 多言語サポート強化 |

## 7. コードスタイル・全般

| 領域 | Upstream | Komorebi | 理由 |
|---|---|---|---|
| ファイルスコープ namespace | block-scope | **file-scoped namespace** | 1 段インデント削減 |
| XML doc コメント | 英語 | **日本語 + WHY 中心** | 日本語チームの可読性 |
| Collection expression | `new List<T>()` | **`[]` 構文** (C# 12+) | 短縮記法 |
| ロック解除 → Mark*DirtyManually | 同期実装 | **ブロックスコープ + ロック解除後に Mark** | FSW バッファイベント競合の根絶（CLAUDE.md "Common Pitfalls" 参照） |

## 8. CI / サプライチェーン保護

| 領域 | Upstream | Komorebi | 理由 |
|---|---|---|---|
| Actions 参照 | `actions/*@vN` 浮動タグ | **SHA ピン止め + Dependabot** | タグ付け替え攻撃対策 |
| RestoreLockedMode | なし | **`$(CI) == true` で有効化** | NuGet リパッケージ攻撃の検知 |
| homebrew-notify | 文字列補間 JSON | **`jq` 構造化 JSON 生成** | タグ名 JSON インジェクション防御 |

---

## 運用ルール

1. **新規 cherry-pick で衝突発生時**: このドキュメントの該当行を確認し、Komorebi 側の理由が今でも有効か判断する
2. **意図的乖離を解消する場合**: 該当行を削除し、対応する Komorebi 側コードも upstream に合わせて簡略化する
3. **新しい意図的乖離が発生したら**: 該当行を追加（領域 / Upstream / Komorebi / 理由 の 4 列）
4. **Upstream への提案候補**: ここに列挙した乖離のうち上流に PR で還元できそうなものは、別途 issue で議論する

最終更新: 2026-05-10 (`/rere` 6 人分隊レビュー対応で全面整備)
