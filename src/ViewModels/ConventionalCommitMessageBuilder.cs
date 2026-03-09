using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     Conventional CommitsメッセージビルダーのViewModel。
    ///     Conventional Commits仕様に従ったコミットメッセージを構築するためのフォーム。
    ///     タイプ、スコープ、説明、詳細、破壊的変更、クローズイシューを入力する。
    /// </summary>
    public class ConventionalCommitMessageBuilder : ObservableValidator
    {
        /// <summary>
        ///     利用可能なコミットタイプのリスト（feat, fix, docs等）。
        /// </summary>
        public List<Models.ConventionalCommitType> Types
        {
            get;
            private set;
        } = [];

        /// <summary>
        ///     選択されたコミットタイプ。変更時にプリフィル説明があれば自動設定する。
        /// </summary>
        [Required(ErrorMessage = "Type of changes can not be null")]
        public Models.ConventionalCommitType SelectedType
        {
            get => _selectedType;
            set
            {
                if (SetProperty(ref _selectedType, value, true) && value is { PrefillShortDesc: { Length: > 0 } })
                    // タイプにプリフィル説明が設定されている場合は説明に自動設定する
                    Description = value.PrefillShortDesc;
            }
        }

        /// <summary>
        ///     コミットのスコープ（影響範囲）。オプション。
        /// </summary>
        public string Scope
        {
            get => _scope;
            set => SetProperty(ref _scope, value);
        }

        /// <summary>
        ///     変更の短い説明。必須項目。
        /// </summary>
        [Required(ErrorMessage = "Short description can not be empty")]
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value, true);
        }

        /// <summary>
        ///     変更の詳細説明。オプション。
        /// </summary>
        public string Detail
        {
            get => _detail;
            set => SetProperty(ref _detail, value);
        }

        /// <summary>
        ///     破壊的変更の説明。設定するとタイプの後に「!」が付加される。
        /// </summary>
        public string BreakingChanges
        {
            get => _breakingChanges;
            set => SetProperty(ref _breakingChanges, value);
        }

        /// <summary>
        ///     クローズするイシュー番号。
        /// </summary>
        public string ClosedIssue
        {
            get => _closedIssue;
            set => SetProperty(ref _closedIssue, value);
        }

        /// <summary>
        ///     コンストラクタ。コミットタイプのオーバーライド設定と適用コールバックを受け取って初期化する。
        /// </summary>
        /// <param name="conventionalTypesOverride">カスタムコミットタイプのJSON文字列（nullで標準タイプ）</param>
        /// <param name="onApply">メッセージ適用時のコールバック</param>
        public ConventionalCommitMessageBuilder(string conventionalTypesOverride, Action<string> onApply)
        {
            // コミットタイプを読み込み、先頭をデフォルト選択する
            Types = Models.ConventionalCommitType.Load(conventionalTypesOverride);
            SelectedType = Types.Count > 0 ? Types[0] : null;
            _onApply = onApply;
        }

        /// <summary>
        ///     入力内容からConventional Commits形式のメッセージを構築して適用する。
        ///     形式: type(scope)!: description
        /// </summary>
        /// <returns>成功した場合はtrue、バリデーションエラーがある場合はfalse</returns>
        [UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL2026:RequiresUnreferencedCode")]
        public bool Apply()
        {
            // バリデーションエラーがある場合は適用しない
            if (HasErrors)
                return false;

            // 全プロパティのバリデーションを実行する
            ValidateAllProperties();
            if (HasErrors)
                return false;

            var builder = new StringBuilder();

            // タイプを追加する（例: feat, fix）
            builder.Append(_selectedType.Type);

            // スコープがある場合は括弧付きで追加する（例: (auth)）
            if (!string.IsNullOrEmpty(_scope))
            {
                builder.Append("(");
                builder.Append(_scope);
                builder.Append(")");
            }

            // 破壊的変更がある場合は「!」を追加する
            if (!string.IsNullOrEmpty(_breakingChanges))
                builder.Append("!");
            builder.Append(": ");

            // 短い説明を追加する
            builder.Append(_description);
            builder.AppendLine().AppendLine();

            // 詳細説明がある場合は追加する
            if (!string.IsNullOrEmpty(_detail))
            {
                builder.Append(_detail);
                builder.AppendLine().AppendLine();
            }

            // 破壊的変更の説明がある場合はフッターに追加する
            if (!string.IsNullOrEmpty(_breakingChanges))
            {
                builder.Append("BREAKING CHANGE: ");
                builder.Append(_breakingChanges);
                builder.AppendLine().AppendLine();
            }

            // クローズイシューがある場合はフッターに追加する
            if (!string.IsNullOrEmpty(_closedIssue))
            {
                builder.Append("Closed ");
                builder.Append(_closedIssue);
            }

            // コールバックでメッセージを適用する
            _onApply?.Invoke(builder.ToString());
            return true;
        }

        /// <summary>メッセージ適用時のコールバック</summary>
        private Action<string> _onApply = null;
        /// <summary>選択されたコミットタイプ</summary>
        private Models.ConventionalCommitType _selectedType = null;
        /// <summary>コミットスコープ</summary>
        private string _scope = string.Empty;
        /// <summary>短い説明</summary>
        private string _description = string.Empty;
        /// <summary>詳細説明</summary>
        private string _detail = string.Empty;
        /// <summary>破壊的変更の説明</summary>
        private string _breakingChanges = string.Empty;
        /// <summary>クローズイシュー番号</summary>
        private string _closedIssue = string.Empty;
    }
}
