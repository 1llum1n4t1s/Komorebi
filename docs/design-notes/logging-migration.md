# ログ抽象化への移行設計メモ

## 背景

現在 Komorebi は `SuperLightLogger` (NuGet 1.0.6) を直接使用している (`src/Models/Logger.cs`)。
`/rere` レビューの Black Ops 隊員 (D) から以下の指摘を受けた:

1. **観測性の欠如**: 構造化ログ (JSON) 出力が無く、Datadog / Seq / ELK 等との連携が困難
2. **トレース統合不在**: OpenTelemetry に乗らないため、git コマンド毎の実行時間や
   どの操作でタイムアウトしたかのトレースが取れない
3. **メンテナンス停止リスク**: SuperLightLogger は NuGet ダウンロード数が極小、
   .NET 11 / 12 への追従が止まる可能性がある

## なぜ今回は実装しないか

`Microsoft.Extensions.Logging` 互換アダプタへの移行は以下を伴う:
- 新規 NuGet 依存 (`Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Logging.File` 等) の追加
- `Models.Logger` の API 互換性を維持しつつ内部実装を入れ替え
- 既存の `LogCrash` / `LogException` / 各 ILog 派生の動作再検証
- Velopack / SuperLightLogger の File Target を再現する Sink 探索 or 自前実装
- AOT 互換性の確認 (`Microsoft.Extensions.Logging` の reflection 利用箇所)

これは独立 PR として扱うべきスコープであり、コードレビュー一括修正と同 PR にすると
影響範囲が広すぎる。よって本ドキュメントを移行計画として残し、**別チケットで実装** する。

## 移行ステップ案

### Phase 1: アダプタ層の追加
1. `src/Models/ILog.cs` を `Microsoft.Extensions.Logging.ILogger` 互換シグネチャに揃える
2. `src/Models/Logger.cs` の `Log` / `LogException` / `LogCrash` を `ILogger` への薄ラップに置き換え
3. SuperLightLogger Sink を `Microsoft.Extensions.Logging.File` Sink に置換
4. `LogManager.Configure` を `LoggerFactory.Create` に置換

### Phase 2: 構造化ログ対応
1. JSON ペイロード生成のために `Serilog.Sinks.File` + `Serilog.Formatting.Json.JsonFormatter` への
   切り替え（or `Microsoft.Extensions.Logging.Console` での構造化出力）
2. ログレベル別 Sink 分離（Error 以上は別ファイル）
3. ログローテーション設定（時間 / サイズ）

### Phase 3: トレース統合
1. `OpenTelemetry.Extensions.Hosting` 追加
2. `Commands.Command.ExecAsync` に Activity 計装
3. exporter は OTLP / Console を切替可能に

## 影響範囲

- `src/Models/Logger.cs` - 全面書き換え
- `src/Models/ILog.cs` - シグネチャ変更
- `src/Models/CommandLog.cs` - 影響あり
- `src/App.axaml.cs` - LogManager.Configure 呼び出し箇所
- `src/Komorebi.csproj` - PackageReference 追加
- 全ての `Models.Logger.Log(...)` 呼び出し (~50 箇所) - シグネチャ互換ならノータッチ

## リスク

- AOT 互換性: `Microsoft.Extensions.Logging` の DI コンテナ統合は reflection を使うが、
  `LoggerFactory.Create` の直接利用なら回避可能
- パッケージサイズ: AOT publish のバイナリサイズが増加する可能性
- 動作変更: ログフォーマット / ファイル名 / ローテーション挙動が変わるとユーザーの
  ログ解析スクリプトが影響を受ける（README に互換性メモを書く必要あり）

## 関連

- `/rere` レビュー報告: バッチ I3 / D IMPORTANT-4
- 既存実装: [src/Models/Logger.cs](../../src/Models/Logger.cs)
