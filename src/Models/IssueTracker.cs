using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.Models
{
    /// <summary>
    ///     課題トラッカーの設定を表すクラス。
    ///     コミットメッセージ中の課題番号パターンをリンクに変換する。
    /// </summary>
    public class IssueTracker : ObservableObject
    {
        /// <summary>
        ///     この課題トラッカー設定が共有設定かどうか。
        /// </summary>
        public bool IsShared
        {
            get => _isShared;
            set => SetProperty(ref _isShared, value);
        }

        /// <summary>
        ///     課題トラッカーの名前。
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>
        ///     課題番号を検出する正規表現パターン文字列。
        ///     設定時に正規表現のコンパイルを試みる。
        /// </summary>
        public string RegexString
        {
            get => _regexString;
            set
            {
                if (SetProperty(ref _regexString, value))
                {
                    if (string.IsNullOrWhiteSpace(_regexString))
                    {
                        _regex = null;
                    }
                    else
                    {
                        try
                        {
                            // 正規表現をコンパイル（複数行モード）
                            _regex = new Regex(_regexString, RegexOptions.Multiline);
                        }
                        catch
                        {
                            // 無効な正規表現の場合はnullに設定
                            _regex = null;
                        }
                    }
                }

                // 正規表現の有効性の変更を通知
                OnPropertyChanged(nameof(IsRegexValid));
            }
        }

        /// <summary>
        ///     正規表現が有効かどうかを判定する。
        /// </summary>
        public bool IsRegexValid
        {
            get => _regex != null;
        }

        /// <summary>
        ///     課題URLのテンプレート。$1, $2等のプレースホルダーが正規表現のグループに置換される。
        /// </summary>
        public string URLTemplate
        {
            get => _urlTemplate;
            set => SetProperty(ref _urlTemplate, value);
        }

        /// <summary>
        ///     メッセージ中の課題番号パターンを検索し、リンク要素として追加する。
        /// </summary>
        /// <param name="outs">検出されたインライン要素を追加するコレクタ。</param>
        /// <param name="message">検索対象のメッセージ文字列。</param>
        public void Matches(InlineElementCollector outs, string message)
        {
            // 正規表現またはURLテンプレートが未設定の場合はスキップ
            if (_regex == null || string.IsNullOrEmpty(_urlTemplate))
                return;

            // メッセージ中のすべてのマッチを検索
            var matches = _regex.Matches(message);
            foreach (Match match in matches)
            {
                var start = match.Index;
                var len = match.Length;

                // 既存の要素と重複する場合はスキップ
                if (outs.Intersect(start, len) != null)
                    continue;

                // URLテンプレートのプレースホルダーをマッチグループで置換
                var link = _urlTemplate;
                for (var j = 1; j < match.Groups.Count; j++)
                {
                    var group = match.Groups[j];
                    if (group.Success)
                        link = link.Replace($"${j}", group.Value);
                }

                // リンク要素として追加
                outs.Add(new InlineElement(InlineElementType.Link, start, len, link));
            }
        }

        private bool _isShared;
        private string _name;
        private string _regexString;
        private string _urlTemplate;
        private Regex _regex = null;
    }
}
