using System.Collections.Generic;
using System.Text;

namespace Komorebi.Models
{
    /// <summary>
    ///     git diffコマンドのオプションを構築するクラス。
    ///     ワーキングコピーの変更、コミット間の差分、リビジョン比較など各種diffに対応する。
    /// </summary>
    public class DiffOption
    {
        /// <summary>
        ///     デフォルトで `--ignore-cr-at-eol` を有効にするかどうか
        /// </summary>
        public static bool IgnoreCRAtEOL
        {
            get;
            set;
        } = true;

        /// <summary>ワーキングコピーの変更情報</summary>
        public Change WorkingCopyChange => _workingCopyChange;
        /// <summary>ステージングされていない変更かどうか</summary>
        public bool IsUnstaged => _isUnstaged;
        /// <summary>比較対象のリビジョンリスト</summary>
        public List<string> Revisions => _revisions;
        /// <summary>対象ファイルパス</summary>
        public string Path => _path;
        /// <summary>リネーム前の元ファイルパス</summary>
        public string OrgPath => _orgPath;

        /// <summary>
        ///     ワーキングコピーの変更用コンストラクタ
        /// </summary>
        /// <param name="change">変更情報</param>
        /// <param name="isUnstaged">ステージされていない変更かどうか</param>
        public DiffOption(Change change, bool isUnstaged)
        {
            _workingCopyChange = change;
            _isUnstaged = isUnstaged;
            _path = change.Path;
            _orgPath = change.OriginalPath;

            if (isUnstaged)
            {
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
                if (change.DataForAmend != null)
                    _extra = $"--cached {change.DataForAmend.ParentSHA}";
                else
                    _extra = "--cached";
            }
        }

        /// <summary>
        ///     コミットの変更用コンストラクタ
        /// </summary>
        /// <param name="commit">対象コミット</param>
        /// <param name="change">変更情報</param>
        public DiffOption(Commit commit, Change change)
        {
            var baseRevision = commit.Parents.Count == 0 ? Commit.EmptyTreeSHA1 : $"{commit.SHA}^";
            _revisions.Add(baseRevision);
            _revisions.Add(commit.SHA);
            _path = change.Path;
            _orgPath = change.OriginalPath;
        }

        /// <summary>
        ///     ファイルパス指定のdiffコンストラクタ。FileHistories（ファイル履歴）で使用。
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
        ///     2つのリビジョン間の差分を表示するためのコンストラクタ
        /// </summary>
        /// <param name="baseRevision">基準リビジョン</param>
        /// <param name="targetRevision">比較先リビジョン</param>
        /// <param name="change">変更情報</param>
        public DiffOption(string baseRevision, string targetRevision, Change change)
        {
            _revisions.Add(string.IsNullOrEmpty(baseRevision) ? "-R" : baseRevision);
            _revisions.Add(targetRevision);
            _path = change.Path;
            _orgPath = change.OriginalPath;
        }

        /// <summary>
        ///     diffコマンドの引数文字列に変換する
        /// </summary>
        public override string ToString()
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrEmpty(_extra))
                builder.Append($"{_extra} ");
            foreach (var r in _revisions)
                builder.Append($"{r} ");

            builder.Append("-- ");
            if (!string.IsNullOrEmpty(_orgPath))
                builder.Append($"{_orgPath.Quoted()} ");
            builder.Append(_path.Quoted());

            return builder.ToString();
        }

        private readonly Change _workingCopyChange = null;
        private readonly bool _isUnstaged = false;
        private readonly string _path;
        private readonly string _orgPath = string.Empty;
        private readonly string _extra = string.Empty;
        private readonly List<string> _revisions = [];
    }
}
