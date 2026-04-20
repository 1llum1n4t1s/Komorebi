namespace Komorebi.Models;

/// <summary>
/// リポジトリノードのリモート到達可能性状態。
/// Welcome 画面で「サーバー側リモートが消失しているか」を示すバッジに使用する。
/// </summary>
public enum RemoteReachability
{
    /// <summary>未チェック。スキャンが一度も走っていない初期状態。</summary>
    Unknown,

    /// <summary>リモートが 1 件も設定されていない。</summary>
    NoRemotes,

    /// <summary>すべてのリモートに到達可能。</summary>
    AllReachable,

    /// <summary>一部のリモートが到達不可。</summary>
    SomeUnreachable,

    /// <summary>すべてのリモートが到達不可。</summary>
    AllUnreachable,
}
