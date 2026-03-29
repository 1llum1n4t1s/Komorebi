using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
/// git blameビューのViewModel。
/// ファイルの各行について、最後に変更したコミット情報を表示する。
/// 履歴ナビゲーション機能（戻る・進む）を備える。
/// </summary>
public class Blame : ObservableObject
{
    /// <summary>
    /// blame対象のファイルパス。
    /// </summary>
    public string File
    {
        get => _file;
        private set => SetProperty(ref _file, value);
    }

    /// <summary>
    /// 空白の差分を無視するかどうかのフラグ。変更時にblameデータを再取得する。
    /// </summary>
    public bool IgnoreWhitespace
    {
        get => _ignoreWhitespace;
        set
        {
            if (SetProperty(ref _ignoreWhitespace, value))
                // フラグ変更時に最初のリビジョンでblameを再実行する
                SetBlameData(_navigationHistory[0]);
        }
    }

    /// <summary>
    /// 現在表示中のリビジョン（コミット）。
    /// </summary>
    public Models.Commit Revision
    {
        get => _revision;
        private set => SetProperty(ref _revision, value);
    }

    /// <summary>
    /// 現在のリビジョンの1つ前のコミット。
    /// </summary>
    public Models.Commit PrevRevision
    {
        get => _prevRevision;
        private set => SetProperty(ref _prevRevision, value);
    }

    /// <summary>
    /// blame結果データ。設定時にIsBinaryプロパティの変更も通知する。
    /// </summary>
    public Models.BlameData Data
    {
        get => _data;
        private set
        {
            if (SetProperty(ref _data, value))
                OnPropertyChanged(nameof(IsBinary));
        }
    }

    /// <summary>
    /// ファイルがバイナリかどうかを示すフラグ。
    /// </summary>
    public bool IsBinary
    {
        get => _data?.IsBinary ?? false;
    }

    /// <summary>
    /// ナビゲーション履歴で戻れるかどうか。
    /// </summary>
    public bool CanBack
    {
        get => _navigationActiveIndex > 0;
    }

    /// <summary>
    /// ナビゲーション履歴で進めるかどうか。
    /// </summary>
    public bool CanForward
    {
        get => _navigationActiveIndex < _navigationHistory.Count - 1;
    }

    /// <summary>
    /// コンストラクタ。リポジトリパス・ファイル・コミットを受け取り、blameデータを取得する。
    /// </summary>
    /// <param name="repo">リポジトリのフルパス</param>
    /// <param name="file">blame対象のファイルパス</param>
    /// <param name="commit">対象コミット</param>
    public Blame(string repo, string file, Models.Commit commit)
    {
        // SHAの先頭10文字を使用する
        var sha = commit.SHA[..10];
        _repo = repo;
        // ナビゲーション履歴に最初のリビジョンを追加する
        _navigationHistory.Add(new RevisionInfo(file, sha));
        SetBlameData(_navigationHistory[0]);
    }

    /// <summary>
    /// 指定SHAのコミットメッセージ全文を取得する。結果はキャッシュされる。
    /// </summary>
    /// <param name="sha">コミットのSHA</param>
    /// <returns>コミットメッセージ全文</returns>
    public string GetCommitMessage(string sha)
    {
        // キャッシュにあればそれを返す
        if (_commitMessages.TryGetValue(sha, out var msg))
            return msg;

        // git logでコミットメッセージを取得してキャッシュに保存する
        msg = new Commands.QueryCommitFullMessage(_repo, sha).GetResult();
        _commitMessages[sha] = msg;
        return msg;
    }

    /// <summary>
    /// ナビゲーション履歴を1つ戻る。
    /// </summary>
    public void Back()
    {
        if (_navigationActiveIndex <= 0)
            return;

        // インデックスを1つ前に移動する
        _navigationActiveIndex--;
        OnPropertyChanged(nameof(CanBack));
        OnPropertyChanged(nameof(CanForward));
        NavigateToCommit(_navigationHistory[_navigationActiveIndex]);
    }

    /// <summary>
    /// ナビゲーション履歴を1つ進む。
    /// </summary>
    public void Forward()
    {
        if (_navigationActiveIndex >= _navigationHistory.Count - 1)
            return;

        // インデックスを1つ次に移動する
        _navigationActiveIndex++;
        OnPropertyChanged(nameof(CanBack));
        OnPropertyChanged(nameof(CanForward));
        NavigateToCommit(_navigationHistory[_navigationActiveIndex]);
    }

    /// <summary>
    /// 1つ前のリビジョンに移動する。
    /// </summary>
    public void GotoPrevRevision()
    {
        if (_prevRevision is not null)
            NavigateToCommit(_file, _prevRevision.SHA[..10]);
    }

    /// <summary>
    /// 指定したファイルとSHAのコミットに移動する。ナビゲーション履歴に追加される。
    /// </summary>
    /// <param name="file">対象ファイルパス</param>
    /// <param name="sha">コミットSHA（短縮形）</param>
    public void NavigateToCommit(string file, string sha)
    {
        // ランチャーのページからリポジトリを探して、コミットグラフ上でも移動する
        if (App.GetLauncher() is { Pages: { } pages })
        {
            foreach (var page in pages)
            {
                if (page.Data is Repository repo && repo.FullPath.Equals(_repo))
                {
                    repo.NavigateToCommit(sha);
                    break;
                }
            }
        }

        // 既に同じリビジョンを表示中なら何もしない
        if (Revision.SHA.StartsWith(sha, StringComparison.Ordinal))
            return;

        // 現在位置より後の履歴を削除してから新しいリビジョンを追加する
        var count = _navigationHistory.Count;
        if (_navigationActiveIndex < count - 1)
            _navigationHistory.RemoveRange(_navigationActiveIndex + 1, count - _navigationActiveIndex - 1);

        var rev = new RevisionInfo(file, sha);
        _navigationHistory.Add(rev);
        _navigationActiveIndex++;
        OnPropertyChanged(nameof(CanBack));
        OnPropertyChanged(nameof(CanForward));
        SetBlameData(rev);
    }

    /// <summary>
    /// ナビゲーション履歴内のリビジョン情報でコミットに移動する（内部用）。
    /// </summary>
    /// <param name="rev">移動先のリビジョン情報</param>
    private void NavigateToCommit(RevisionInfo rev)
    {
        // リポジトリのコミットグラフ上でも移動する
        if (App.GetLauncher() is { Pages: { } pages })
        {
            foreach (var page in pages)
            {
                if (page.Data is Repository repo && repo.FullPath.Equals(_repo))
                {
                    repo.NavigateToCommit(rev.SHA);
                    break;
                }
            }
        }

        // 異なるリビジョンの場合のみblameデータを再取得する
        if (!Revision.SHA.StartsWith(rev.SHA, StringComparison.Ordinal))
            SetBlameData(rev);
    }

    /// <summary>
    /// 指定リビジョンのblameデータをバックグラウンドで取得・設定する。
    /// </summary>
    /// <param name="rev">対象のリビジョン情報</param>
    private void SetBlameData(RevisionInfo rev)
    {
        // 前回の取得処理をキャンセル・破棄する
        if (_cancellationSource is not null)
        {
            _cancellationSource.Cancel();
            _cancellationSource.Dispose();
        }

        _cancellationSource = new CancellationTokenSource();
        var token = _cancellationSource.Token;

        File = rev.File;

        // コミット情報を取得するタスク（現在と前のリビジョン）
        Task.Run(async () =>
        {
            // git logで該当ファイルの直近2コミットを取得する
            var argsBuilder = new StringBuilder();
            argsBuilder
                .Append("--date-order -n 2 ")
                .Append(rev.SHA)
                .Append(" -- ")
                .Append(rev.File.Quoted());

            var commits = await new Commands.QueryCommits(_repo, argsBuilder.ToString(), false)
                .GetResultAsync()
                .ConfigureAwait(false);

            // UIスレッドでリビジョン情報を更新する
            Dispatcher.UIThread.Post(() =>
            {
                if (!token.IsCancellationRequested)
                {
                    Revision = commits.Count > 0 ? commits[0] : null;
                    PrevRevision = commits.Count > 1 ? commits[1] : null;
                }
            });
        }, token);

        // git blameを実行してデータを取得するタスク
        Task.Run(async () =>
        {
            var result = await new Commands.Blame(_repo, rev.File, rev.SHA, _ignoreWhitespace)
                .ReadAsync()
                .ConfigureAwait(false);

            // UIスレッドでblameデータを更新する
            Dispatcher.UIThread.Post(() =>
            {
                if (!token.IsCancellationRequested)
                    Data = result;
            });
        }, token);
    }

    /// <summary>
    /// ナビゲーション履歴で使用するリビジョン情報を保持する内部クラス。
    /// </summary>
    private class RevisionInfo
    {
        /// <summary>対象ファイルパス</summary>
        public string File { get; set; } = string.Empty;
        /// <summary>コミットSHA（短縮形）</summary>
        public string SHA { get; set; } = string.Empty;

        /// <summary>
        /// コンストラクタ。ファイルパスとSHAを受け取る。
        /// </summary>
        /// <param name="file">対象ファイルパス</param>
        /// <param name="sha">コミットSHA</param>
        public RevisionInfo(string file, string sha)
        {
            File = file;
            SHA = sha;
        }
    }

    /// <summary>リポジトリのフルパス</summary>
    private string _repo;
    /// <summary>blame対象のファイルパス</summary>
    private string _file;
    /// <summary>空白を無視するかどうか</summary>
    private bool _ignoreWhitespace = false;
    /// <summary>現在のリビジョン</summary>
    private Models.Commit _revision;
    /// <summary>1つ前のリビジョン</summary>
    private Models.Commit _prevRevision;
    /// <summary>非同期処理のキャンセルトークン</summary>
    private CancellationTokenSource _cancellationSource = null;
    /// <summary>ナビゲーション履歴の現在位置</summary>
    private int _navigationActiveIndex = 0;
    /// <summary>ナビゲーション履歴リスト</summary>
    private List<RevisionInfo> _navigationHistory = [];
    /// <summary>blameデータ</summary>
    private Models.BlameData _data = null;
    /// <summary>コミットメッセージのキャッシュ（SHA→メッセージ）</summary>
    private Dictionary<string, string> _commitMessages = new();
}
