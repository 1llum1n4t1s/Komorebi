using System.Collections.Generic;
using System.Text;

namespace Komorebi.Commands
{
    /// <summary>
    ///     ブランチのマージを実行するgitコマンド。
    ///     git merge --progress を実行する。
    /// </summary>
    public class Merge : Command
    {
        /// <summary>
        ///     単一ソースからのマージコマンドを初期化する。
        /// </summary>
        /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
        /// <param name="source">マージ元のブランチ名またはコミットSHA。</param>
        /// <param name="mode">マージモード（--ff, --no-ff, --squash 等）。</param>
        /// <param name="edit">マージメッセージを編集するかどうか。</param>
        public Merge(string repo, string source, string mode, bool edit)
        {
            WorkingDirectory = repo;
            Context = repo;

            // マージメッセージ編集が有効な場合はコアエディタを使用する
            Editor = EditorType.CoreEditor;

            var builder = new StringBuilder();

            // git merge --progress: 進捗表示付きでマージを実行する
            builder.Append("merge --progress ");

            // --edit / --no-edit: マージメッセージの編集を制御する
            builder.Append(edit ? "--edit " : "--no-edit ");

            // マージ元とマージモードを指定する
            builder.Append(source);
            builder.Append(' ');
            builder.Append(mode);

            Args = builder.ToString();
        }

        /// <summary>
        ///     複数ターゲットのマージコマンドを初期化する（オクトパスマージ対応）。
        /// </summary>
        /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
        /// <param name="targets">マージ対象のブランチ名またはコミットSHAのリスト。</param>
        /// <param name="autoCommit">マージ後に自動コミットするかどうか。</param>
        /// <param name="strategy">マージ戦略（ort, recursive, octopus 等）。</param>
        public Merge(string repo, List<string> targets, bool autoCommit, string strategy)
        {
            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder();

            // git merge --progress: 進捗表示付きでマージを実行する
            builder.Append("merge --progress ");

            // --strategy: マージ戦略を指定する
            if (!string.IsNullOrEmpty(strategy))
                builder.Append("--strategy=").Append(strategy).Append(' ');

            // --no-commit: マージ結果を自動コミットしない
            if (!autoCommit)
                builder.Append("--no-commit ");

            // 各マージターゲットを引数に追加する
            foreach (var t in targets)
            {
                builder.Append(t);
                builder.Append(' ');
            }

            Args = builder.ToString();
        }
    }
}
