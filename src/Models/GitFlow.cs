namespace Komorebi.Models
{
    /// <summary>
    ///     Git Flowのブランチ種別を表すenum
    /// </summary>
    public enum GitFlowBranchType
    {
        /// <summary>種別なし</summary>
        None = 0,
        /// <summary>機能開発ブランチ</summary>
        Feature,
        /// <summary>リリース準備ブランチ</summary>
        Release,
        /// <summary>緊急修正ブランチ</summary>
        Hotfix,
    }

    /// <summary>
    ///     Git Flow設定を管理するクラス。
    ///     マスター/開発ブランチ名やプレフィックスを保持する。
    /// </summary>
    public class GitFlow
    {
        /// <summary>マスターブランチ名（例: main, master）</summary>
        public string Master { get; set; } = string.Empty;
        /// <summary>開発ブランチ名（例: develop）</summary>
        public string Develop { get; set; } = string.Empty;
        /// <summary>featureブランチのプレフィックス（例: feature/）</summary>
        public string FeaturePrefix { get; set; } = string.Empty;
        /// <summary>releaseブランチのプレフィックス（例: release/）</summary>
        public string ReleasePrefix { get; set; } = string.Empty;
        /// <summary>hotfixブランチのプレフィックス（例: hotfix/）</summary>
        public string HotfixPrefix { get; set; } = string.Empty;

        /// <summary>
        ///     Git Flow設定が有効かどうか（全フィールドが入力済み）
        /// </summary>
        public bool IsValid
        {
            get
            {
                return !string.IsNullOrEmpty(Master) &&
                    !string.IsNullOrEmpty(Develop) &&
                    !string.IsNullOrEmpty(FeaturePrefix) &&
                    !string.IsNullOrEmpty(ReleasePrefix) &&
                    !string.IsNullOrEmpty(HotfixPrefix);
            }
        }

        /// <summary>
        ///     指定されたブランチ種別に対応するプレフィックスを取得する
        /// </summary>
        /// <param name="type">ブランチ種別</param>
        /// <returns>対応するプレフィックス文字列</returns>
        public string GetPrefix(GitFlowBranchType type)
        {
            return type switch
            {
                GitFlowBranchType.Feature => FeaturePrefix,
                GitFlowBranchType.Release => ReleasePrefix,
                GitFlowBranchType.Hotfix => HotfixPrefix,
                _ => string.Empty,
            };
        }
    }
}
