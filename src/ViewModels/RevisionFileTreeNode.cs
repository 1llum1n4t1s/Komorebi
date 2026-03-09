using System.Collections.Generic;
using System.IO;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     リビジョン（コミット）のファイルツリー内のノードを表すViewModel。
    ///     コミット内のファイルやディレクトリをツリー構造で表示するために使用される。
    /// </summary>
    public class RevisionFileTreeNode : ObservableObject
    {
        /// <summary>
        ///     このノードに対応するGitオブジェクト（Blob/Tree）。
        /// </summary>
        public Models.Object Backend { get; set; } = null;

        /// <summary>
        ///     ツリー内のネスト深度。インデント表示に使用。
        /// </summary>
        public int Depth { get; set; } = 0;

        /// <summary>
        ///     子ノードのリスト。ディレクトリノードの場合にファイルやサブディレクトリを保持する。
        /// </summary>
        public List<RevisionFileTreeNode> Children { get; set; } = new List<RevisionFileTreeNode>();

        /// <summary>
        ///     ノードの表示名。パスからファイル名部分を取得する。
        /// </summary>
        public string Name
        {
            get => Backend == null ? string.Empty : Path.GetFileName(Backend.Path);
        }

        /// <summary>
        ///     このノードがフォルダ（Treeオブジェクト）かどうか。
        /// </summary>
        public bool IsFolder
        {
            get => Backend?.Type == Models.ObjectType.Tree;
        }

        /// <summary>
        ///     ツリー上でこのノードが展開されているかどうか。
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        private bool _isExpanded = false;
    }
}
