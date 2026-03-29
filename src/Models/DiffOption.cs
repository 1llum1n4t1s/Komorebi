using System;
using System.Collections.Generic;
using System.Text;

namespace Komorebi.Models;

/// <summary>
/// git diffコマンドの引数を構築するクラス。
/// ワーキングコピー、コミット間、ファイル履歴など様々なdiffシナリオに対応する。
/// </summary>
public class DiffOption
{
    /// <summary>
    /// 行末のCR（キャリッジリターン）を無視するかどうか（デフォルトで有効）
    /// </summary>
    public static bool IgnoreCRAtEOL
    {
        get;
        set;
    } = true;

    /// <summary>ワーキングコピーの変更情報（ワーキングコピーdiff時のみ使用）</summary>
    public Change WorkingCopyChange => _workingCopyChange;
    /// <summary>ステージング前の変更かどうか</summary>
    public bool IsUnstaged => _isUnstaged;
    /// <summary>比較対象のリビジョンリスト</summary>
    public List<string> Revisions => _revisions;
    /// <summary>対象ファイルのパス</summary>
    public string Path => _path;
    /// <summary>リネーム前の元ファイルパス</summary>
    public string OrgPath => _orgPath;

    /// <summary>
    /// ワーキングコピーの変更用のdiffオプションを構築する
    /// </summary>
    /// <param name="change">変更情報</param>
    /// <param name="isUnstaged">ステージング前の変更かどうか</param>
    public DiffOption(Change change, bool isUnstaged)
    {
        _workingCopyChange = change;
        _isUnstaged = isUnstaged;
        _path = change.Path;
        _orgPath = change.OriginalPath;

        if (isUnstaged)
        {
            // 未追跡/新規ファイルは /dev/null との比較（--no-index）
            switch (change.WorkTree)
            {
                case ChangeState.Added:
                case ChangeState.Untracked:
                    _extra = "--no-index";
                    _orgPath = "/dev/null";
                    break;
            }
        }
        else
        {
            // ステージング済みの変更はキャッシュとの比較
            if (change.DataForAmend is not null)
                _extra = $"--cached {change.DataForAmend.ParentSHA}";
            else
                _extra = "--cached";
        }
    }

    /// <summary>
    /// コミット内の変更用のdiffオプションを構築する
    /// </summary>
    /// <param name="commit">対象コミット</param>
    /// <param name="change">コミット内の変更情報</param>
    public DiffOption(Commit commit, Change change)
    {
        // 親がないルートコミットの場合は空ツリーを基準にする
        var baseRevision = commit.Parents.Count == 0 ? Commit.EmptyTreeSHA1 : $"{commit.SHA}^";
        _revisions.Add(baseRevision);
        _revisions.Add(commit.SHA);
        _path = change.Path;
        _orgPath = change.OriginalPath;
    }

    /// <summary>
    /// ファイル履歴用のdiffオプションを構築する（ファイルパス指定）
    /// </summary>
    /// <param name="commit">対象コミット</param>
    /// <param name="file">ファイルパス</param>
    public DiffOption(Commit commit, string file)
    {
        var baseRevision = commit.Parents.Count == 0 ? Commit.EmptyTreeSHA1 : $"{commit.SHA}^";
        _revisions.Add(baseRevision);
        _revisions.Add(commit.SHA);
        _path = file;
    }

    /// <summary>
    /// ファイル履歴の単一バージョン用diffオプションを構築する
    /// </summary>
    /// <param name="ver">ファイルバージョン情報</param>
    public DiffOption(FileVersion ver)
    {
        if (string.IsNullOrEmpty(ver.OriginalPath))
        {
            // リネームなし: 親コミット（または空ツリー）との比較
            _revisions.Add(ver.HasParent ? $"{ver.SHA}^" : Commit.EmptyTreeSHA1);
            _revisions.Add(ver.SHA);
            _path = ver.Path;
        }
        else
        {
            // リネームあり: 旧パスと新パスを明示的に指定して比較
            _revisions.Add($"{ver.SHA}^:{ver.OriginalPath.Quoted()}");
            _revisions.Add($"{ver.SHA}:{ver.Path.Quoted()}");
            _path = ver.Path;
            _orgPath = ver.Change.OriginalPath;
            _ignorePaths = true;
        }
    }

    /// <summary>
    /// ファイル履歴の2バージョン間のdiffオプションを構築する
    /// </summary>
    /// <param name="start">開始バージョン</param>
    /// <param name="end">終了バージョン</param>
    public DiffOption(FileVersion start, FileVersion end)
    {
        if (start.Change.Index == ChangeState.Deleted)
        {
            // 開始バージョンが削除済み: 空ツリーと終了バージョンを比較
            _revisions.Add(Commit.EmptyTreeSHA1);
            _revisions.Add(end.SHA);
            _path = end.Path;
        }
        else if (end.Change.Index == ChangeState.Deleted)
        {
            // 終了バージョンが削除済み: 開始バージョンと空ツリーを比較
            _revisions.Add(start.SHA);
            _revisions.Add(Commit.EmptyTreeSHA1);
            _path = start.Path;
        }
        else if (!end.Path.Equals(start.Path, StringComparison.Ordinal))
        {
            // パスが異なる（リネーム）: 各バージョンのパスを明示指定
            _revisions.Add($"{start.SHA}:{start.Path.Quoted()}");
            _revisions.Add($"{end.SHA}:{end.Path.Quoted()}");
            _path = end.Path;
            _orgPath = start.Path;
            _ignorePaths = true;
        }
        else
        {
            // 同一パス: リビジョン間の直接比較
            _revisions.Add(start.SHA);
            _revisions.Add(end.SHA);
            _path = start.Path;
        }
    }

    /// <summary>
    /// 2つのリビジョン間の差分を表示するdiffオプションを構築する
    /// </summary>
    /// <param name="baseRevision">基準リビジョン（空の場合は逆方向diff）</param>
    /// <param name="targetRevision">対象リビジョン</param>
    /// <param name="change">変更情報</param>
    public DiffOption(string baseRevision, string targetRevision, Change change)
    {
        // 基準リビジョンが空の場合は -R（逆方向）で比較
        _revisions.Add(string.IsNullOrEmpty(baseRevision) ? "-R" : baseRevision);
        _revisions.Add(targetRevision);
        _path = change.Path;
        _orgPath = change.OriginalPath;
    }

    /// <summary>
    /// git diffコマンドの引数文字列に変換する
    /// </summary>
    /// <returns>diffコマンド引数文字列</returns>
    public override string ToString()
    {
        var builder = new StringBuilder();

        // 追加オプション（--cached, --no-index等）を先頭に追加
        if (!string.IsNullOrEmpty(_extra))
            builder.Append($"{_extra} ");

        // リビジョン指定を追加
        foreach (var r in _revisions)
            builder.Append($"{r} ");

        // パス指定を無視する場合（リネーム時の明示的パス指定）はここで終了
        if (_ignorePaths)
            return builder.ToString();

        // ファイルパスを「-- パス」形式で追加
        builder.Append("-- ");
        if (!string.IsNullOrEmpty(_orgPath))
            builder.Append($"{_orgPath.Quoted()} ");
        builder.Append(_path.Quoted());

        return builder.ToString();
    }

    /// <summary>ワーキングコピーの変更情報</summary>
    private readonly Change _workingCopyChange = null;
    /// <summary>ステージング前の変更かどうか</summary>
    private readonly bool _isUnstaged = false;
    /// <summary>対象ファイルのパス</summary>
    private readonly string _path;
    /// <summary>リネーム前の元パス</summary>
    private readonly string _orgPath = string.Empty;
    /// <summary>追加オプション文字列（--cached等）</summary>
    private readonly string _extra = string.Empty;
    /// <summary>比較対象リビジョンのリスト</summary>
    private readonly List<string> _revisions = [];
    /// <summary>パス指定を省略するかどうか</summary>
    private readonly bool _ignorePaths = false;
}
