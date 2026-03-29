using System;
using System.Collections.Generic;

namespace Komorebi.Models;

/// <summary>
/// git bisectの現在の状態を表す列挙型。
/// </summary>
public enum BisectState
{
    /// <summary>
    /// bisect未実行状態。
    /// </summary>
    None = 0,

    /// <summary>
    /// good/bad範囲の指定待ち状態。
    /// </summary>
    WaitingForRange,

    /// <summary>
    /// 二分探索実行中の状態。
    /// </summary>
    Detecting,
}

/// <summary>
/// bisectにおけるコミットのフラグ（good/bad）を表すフラグ列挙型。
/// </summary>
[Flags]
public enum BisectCommitFlag
{
    /// <summary>
    /// フラグなし。
    /// </summary>
    None = 0,

    /// <summary>
    /// goodとしてマークされたコミット。
    /// </summary>
    Good = 1 << 0,

    /// <summary>
    /// badとしてマークされたコミット。
    /// </summary>
    Bad = 1 << 1,
}

/// <summary>
/// git bisectの状態データを保持するクラス。
/// good/badとしてマークされたコミットSHAのセットを管理する。
/// </summary>
public class Bisect
{
    /// <summary>
    /// badとしてマークされたコミットSHAのセット。
    /// </summary>
    public HashSet<string> Bads
    {
        get;
        set;
    } = [];

    /// <summary>
    /// goodとしてマークされたコミットSHAのセット。
    /// </summary>
    public HashSet<string> Goods
    {
        get;
        set;
    } = [];
}
