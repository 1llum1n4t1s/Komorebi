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
    /// 最初のbadコミットの指定待ち状態。
    /// </summary>
    WaitingForFirstBad,

    /// <summary>
    /// 現在のHEADはマーク済みで、別のコミットのチェックアウト待ち状態。
    /// </summary>
    WaitingForCheckoutAnother,

    /// <summary>
    /// 最初のgoodコミットの指定待ち状態。
    /// </summary>
    WaitingForFirstGood,

    /// <summary>
    /// 現在のHEADに対するgood/badマーク待ち状態。
    /// </summary>
    WaitingForMark,
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
    Good,

    /// <summary>
    /// badとしてマークされたコミット。
    /// </summary>
    Bad,

    /// <summary>
    /// skipとしてマークされたコミット。
    /// </summary>
    Skipped,
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

    /// <summary>
    /// skipとしてマークされたコミットSHAのセット。
    /// </summary>
    public HashSet<string> Skipped
    {
        get;
        set;
    } = [];
}
