namespace Komorebi.Models
{
    /// <summary>
    ///     サブモジュールの状態を表す列挙型
    /// </summary>
    public enum SubmoduleStatus
    {
        /// <summary>正常</summary>
        Normal = 0,
        /// <summary>未初期化</summary>
        NotInited,
        /// <summary>リビジョンが変更された</summary>
        RevisionChanged,
        /// <summary>マージ未完了</summary>
        Unmerged,
        /// <summary>変更あり</summary>
        Modified,
    }

    /// <summary>
    ///     Gitサブモジュールの情報を保持するクラス
    /// </summary>
    public class Submodule
    {
        /// <summary>サブモジュールのパス</summary>
        public string Path { get; set; } = string.Empty;
        /// <summary>サブモジュールが指すコミットSHA</summary>
        public string SHA { get; set; } = string.Empty;
        /// <summary>サブモジュールのリモートURL</summary>
        public string URL { get; set; } = string.Empty;
        /// <summary>サブモジュールが追跡するブランチ名</summary>
        public string Branch { get; set; } = string.Empty;
        /// <summary>サブモジュールの現在の状態</summary>
        public SubmoduleStatus Status { get; set; } = SubmoduleStatus.Normal;
        /// <summary>サブモジュールにダーティな変更があるかどうか（未初期化より上の状態）</summary>
        public bool IsDirty => Status > SubmoduleStatus.NotInited;
    }
}
