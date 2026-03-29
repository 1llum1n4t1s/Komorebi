using System;

namespace Komorebi.Models;

/// <summary>
/// リポジトリの変更状態を表すフラグ列挙型。
/// ローカル変更やプッシュ/プル待ちの状態を示す。
/// </summary>
[Flags]
public enum DirtyState
{
    /// <summary>
    /// 変更なしのクリーンな状態。
    /// </summary>
    None = 0,

    /// <summary>
    /// ローカルに未コミットの変更がある。
    /// </summary>
    HasLocalChanges = 1 << 0,

    /// <summary>
    /// プルまたはプッシュ待ちの変更がある。
    /// </summary>
    HasPendingPullOrPush = 1 << 1,
}
