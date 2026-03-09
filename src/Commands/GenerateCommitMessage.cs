using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Komorebi.Commands
{
    /// <summary>
    ///     AIを使用してコミットメッセージを自動生成するクラス。
    ///     各ファイルのdiff内容をAIに送信し、変更の要約とコミットサブジェクトを生成する。
    ///     https://github.com/anjerodev/commitollama のC#実装。
    /// </summary>
    /// <remarks>
    ///     A C# version of https://github.com/anjerodev/commitollama
    /// </remarks>
    public class GenerateCommitMessage
    {
        /// <summary>
        ///     変更ファイルのdiff内容を取得するgitコマンド。
        ///     git diff --no-color --no-ext-diff --diff-algorithm=minimal を実行する。
        /// </summary>
        public class GetDiffContent : Command
        {
            /// <summary>
            ///     GetDiffContentコマンドを初期化する。
            /// </summary>
            /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
            /// <param name="opt">diff対象を指定するオプション。</param>
            public GetDiffContent(string repo, Models.DiffOption opt)
            {
                WorkingDirectory = repo;
                Context = repo;

                // git diff --diff-algorithm=minimal: 最小限のdiffアルゴリズムを使用して差分を取得する
                Args = $"diff --no-color --no-ext-diff --diff-algorithm=minimal {opt}";
            }

            /// <summary>
            ///     diff内容を非同期で読み取る。
            /// </summary>
            /// <returns>コマンドの実行結果。</returns>
            public async Task<Result> ReadAsync()
            {
                return await ReadToEndAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     GenerateCommitMessageを初期化する。
        /// </summary>
        /// <param name="service">AIサービスの設定情報。</param>
        /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
        /// <param name="changes">コミット対象の変更ファイルリスト。</param>
        /// <param name="cancelToken">キャンセルトークン。</param>
        /// <param name="onResponse">AIからの応答を受け取るコールバック。</param>
        public GenerateCommitMessage(Models.OpenAIService service, string repo, List<Models.Change> changes, CancellationToken cancelToken, Action<string> onResponse)
        {
            _service = service;
            _repo = repo;
            _changes = changes;
            _cancelToken = cancelToken;
            _onResponse = onResponse;
        }

        /// <summary>
        ///     コミットメッセージの生成を非同期で実行する。
        ///     各変更ファイルのdiffをAIに送信して分析し、最終的にサブジェクト行を生成する。
        /// </summary>
        public async Task ExecAsync()
        {
            try
            {
                // ユーザーに分析開始を通知する
                _onResponse?.Invoke("Waiting for pre-file analyzing to completed...\n\n");

                var responseBuilder = new StringBuilder();
                var summaryBuilder = new StringBuilder();

                // 各変更ファイルのdiffをAIに送信して分析する
                foreach (var change in _changes)
                {
                    // キャンセルが要求された場合は処理を中断する
                    if (_cancelToken.IsCancellationRequested)
                        return;

                    responseBuilder.Append("- ");
                    summaryBuilder.Append("- ");

                    // ファイルのdiff内容を取得する
                    var rs = await new GetDiffContent(_repo, new Models.DiffOption(change, false)).ReadAsync();
                    if (rs.IsSuccess)
                    {
                        // AIにdiffの分析を依頼し、ストリーミングで応答を受け取る
                        await _service.ChatAsync(
                            _service.AnalyzeDiffPrompt,
                            $"Here is the `git diff` output: {rs.StdOut}",
                            _cancelToken,
                            update =>
                            {
                                responseBuilder.Append(update);
                                summaryBuilder.Append(update);

                                // 進捗状況をリアルタイムでユーザーに表示する
                                _onResponse?.Invoke($"Waiting for pre-file analyzing to completed...\n\n{responseBuilder}");
                            });
                    }

                    responseBuilder.AppendLine();
                    // ファイルパスを要約に追記する
                    summaryBuilder.Append("(file: ").Append(change.Path).AppendLine(")");
                }

                if (_cancelToken.IsCancellationRequested)
                    return;

                // 全ファイルの分析結果からコミットサブジェクトを生成する
                var responseBody = responseBuilder.ToString();
                var subjectBuilder = new StringBuilder();
                await _service.ChatAsync(
                    _service.GenerateSubjectPrompt,
                    $"Here are the summaries changes:\n{summaryBuilder}",
                    _cancelToken,
                    update =>
                    {
                        subjectBuilder.Append(update);
                        // サブジェクトと詳細を組み合わせて表示する
                        _onResponse?.Invoke($"{subjectBuilder}\n\n{responseBody}");
                    });
            }
            catch (Exception e)
            {
                App.RaiseException(_repo, $"Failed to generate commit message: {e}");
            }
        }

        /// <summary>
        ///     使用するAIサービスの設定。
        /// </summary>
        private Models.OpenAIService _service;

        /// <summary>
        ///     リポジトリの作業ディレクトリパス。
        /// </summary>
        private string _repo;

        /// <summary>
        ///     コミット対象の変更ファイルリスト。
        /// </summary>
        private List<Models.Change> _changes;

        /// <summary>
        ///     処理のキャンセルトークン。
        /// </summary>
        private CancellationToken _cancelToken;

        /// <summary>
        ///     AIからの応答を受け取るコールバック。
        /// </summary>
        private Action<string> _onResponse;
    }
}
