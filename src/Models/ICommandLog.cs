namespace Komorebi.Models;

/// <summary>
/// コマンドログの受信を処理するインターフェース
/// </summary>
public interface ICommandLogReceiver
{
    /// <summary>
    /// コマンドログの1行を受信した際に呼び出される
    /// </summary>
    /// <param name="line">ログ行の内容</param>
    void OnReceiveCommandLog(string line);
}

/// <summary>
/// コマンドログへの書き込みインターフェース
/// </summary>
public interface ICommandLog
{
    /// <summary>
    /// ログに1行追加する
    /// </summary>
    /// <param name="line">追加する行の内容</param>
    void AppendLine(string line);
}
