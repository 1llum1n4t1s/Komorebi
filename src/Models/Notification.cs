namespace Komorebi.Models
{
    /// <summary>
    ///     UIに表示する通知メッセージを保持するクラス
    /// </summary>
    public class Notification
    {
        /// <summary>エラー通知かどうか</summary>
        public bool IsError { get; set; } = false;
        /// <summary>通知メッセージの内容</summary>
        public string Message { get; set; } = string.Empty;
    }
}
