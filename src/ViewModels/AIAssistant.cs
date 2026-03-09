using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     AIコミットメッセージアシスタントのViewModel。
    ///     OpenAI APIを使用して変更内容からコミットメッセージを自動生成する。
    /// </summary>
    public class AIAssistant : ObservableObject
    {
        /// <summary>
        ///     メッセージ生成中かどうかを示すフラグ。
        /// </summary>
        public bool IsGenerating
        {
            get => _isGenerating;
            private set => SetProperty(ref _isGenerating, value);
        }

        /// <summary>
        ///     生成されたコミットメッセージのテキスト。
        /// </summary>
        public string Text
        {
            get => _text;
            private set => SetProperty(ref _text, value);
        }

        /// <summary>
        ///     コンストラクタ。リポジトリ・AIサービス・変更リストを受け取り、即座に生成を開始する。
        /// </summary>
        /// <param name="repo">対象のリポジトリViewModel</param>
        /// <param name="service">OpenAIサービス設定</param>
        /// <param name="changes">コミットメッセージ生成対象の変更リスト</param>
        public AIAssistant(Repository repo, Models.OpenAIService service, List<Models.Change> changes)
        {
            _repo = repo;
            _service = service;
            _changes = changes;
            _cancel = new CancellationTokenSource();

            // コンストラクタ呼び出し時に自動的にメッセージ生成を開始する
            Gen();
        }

        /// <summary>
        ///     コミットメッセージを再生成する。実行中の生成をキャンセルしてから新たに開始する。
        /// </summary>
        public void Regen()
        {
            // 前回の生成がまだ実行中なら先にキャンセルする
            if (_cancel is { IsCancellationRequested: false })
                _cancel.Cancel();

            Gen();
        }

        /// <summary>
        ///     生成されたコミットメッセージをリポジトリに適用する。
        /// </summary>
        public void Apply()
        {
            // 生成テキストをリポジトリのコミットメッセージとして設定する
            _repo.SetCommitMessage(Text);
        }

        /// <summary>
        ///     生成処理をキャンセルする。
        /// </summary>
        public void Cancel()
        {
            _cancel?.Cancel();
        }

        /// <summary>
        ///     バックグラウンドタスクでAIコミットメッセージ生成を実行する。
        /// </summary>
        private void Gen()
        {
            // テキストをクリアして生成中フラグを立てる
            Text = string.Empty;
            IsGenerating = true;

            _cancel = new CancellationTokenSource();
            Task.Run(async () =>
            {
                // AIサービスを使ってコミットメッセージを生成し、UIスレッドでテキストを更新する
                await new Commands.GenerateCommitMessage(_service, _repo.FullPath, _changes, _cancel.Token, message =>
                {
                    Dispatcher.UIThread.Post(() => Text = message);
                }).ExecAsync().ConfigureAwait(false);

                // 生成完了後にUIスレッドでフラグを解除する
                Dispatcher.UIThread.Post(() => IsGenerating = false);
            }, _cancel.Token);
        }

        /// <summary>対象リポジトリへの参照</summary>
        private readonly Repository _repo = null;
        /// <summary>OpenAIサービス設定</summary>
        private Models.OpenAIService _service = null;
        /// <summary>コミットメッセージ生成対象の変更リスト</summary>
        private List<Models.Change> _changes = null;
        /// <summary>生成処理のキャンセルトークンソース</summary>
        private CancellationTokenSource _cancel = null;
        /// <summary>生成中フラグ</summary>
        private bool _isGenerating = false;
        /// <summary>生成されたテキスト</summary>
        private string _text = string.Empty;
    }
}
