# Upstream Sync Log

Komorebi は [sourcegit-scm/sourcegit](https://github.com/sourcegit-scm/sourcegit) のフォーク。
上流に追従しつつ Komorebi 独自機能を保つため、定期的に cherry-pick batch を実施する。

このドキュメントは「**なぜこのコミットを採用 / スキップしたか**」のチーム共有メモ。
個人の `~/.claude/plans/` から脱却し、リポジトリ内に来歴を集約することで、
将来の開発者・新規参画者が「再評価から始める」コストを削減する。

## 同期方針

- **期間**: 上流の master を最低 1 ヶ月に 1 回フェッチして cherry-pick batch を作る
- **タグ運用**: `upstream/<YYYY-MM-DD>` のタグで上流時点をマーク
- **競合の指針**: 「Komorebi 独自実装は守る、上流バグ修正は採用、純粋スタイル変更は declined」
- **Komorebi 独自実装（competing 注意）**:
  - `App.RaiseException`（vs 上流 `Notification.Send`）
  - 統合 `WelcomeToolbar`（上流 `RepositoryToolbar` は削除）
  - `SSHKeyPicker` + `LegacySSHKeyOptOutSentinel`
  - CodeCommit URL handling（HTTPS / SSH / GRC 3 形式）
  - Anthropic AI provider
  - `SuperLightLogger` 連携
  - file-scoped namespace + 日本語 XML doc コメント
  - collection expression `[]`
  - `[DynamicDependency]` で AOT 安全な View 解決（移行中）

## 形式

各エントリは以下を含めること:

| 項目 | 説明 |
|---|---|
| 上流 SHA | `git log --oneline` 形式の短縮 SHA |
| 上流コミットメッセージ | 1 行サマリ |
| ステータス | `applied` / `declined` / `deferred` / `superseded` |
| Komorebi コミット | 取り込み先の Komorebi 側 SHA（applied のみ） |
| 理由 | declined / deferred の場合、なぜそうしたか |

## ログ

### 2026-04-XX バッチ（template）

| 上流 SHA | サマリ | ステータス | Komorebi SHA | 理由 |
|---|---|---|---|---|
| _例_: `abc1234` | `Fix push --force-with-lease NRE` | applied | `def5678` | 実バグ修正 |
| _例_: `efg9abc` | `Rename Notification.Send → ShowToast` | declined | — | Komorebi では `App.RaiseException` を採用しているため |
| _例_: `1234567` | `Refactor: extract IBranchRepository` | deferred | — | アーキテクチャ判断要、次回 sync 時に再評価 |

### 過去ログ（移行）

過去のバッチログは `~/.claude/plans/goofy-finding-ullman.md` 等の個人 plan ファイルに散在していた。
新規バッチからこのファイルにマージしていく。

## 上流取り込み手順

```bash
# 1. 上流 fetch
git remote add upstream https://github.com/sourcegit-scm/sourcegit.git
git fetch upstream

# 2. 上流の master 時点を mark
git tag upstream/$(date +%Y-%m-%d) upstream/master

# 3. 前回 sync 以降のコミット一覧
git log --oneline upstream/<前回タグ>..upstream/<今回タグ>

# 4. 各コミットを評価して cherry-pick
git cherry-pick -x <SHA>

# 5. このファイルに記録
```

## トラブルシューティング

- **大規模 conflict が発生した場合**: 一時的に main から分岐した `sync/<date>` ブランチで作業する
- **特定機能の上流リファクタが大きい場合**: Komorebi 独自実装を上流に PR する道を検討する（メンテナンスコスト圧縮）
- **CVE / セキュリティ修正の取り込み遅延**: 該当パッチを最優先で cherry-pick し、batch 全体を待たずに main にマージする
