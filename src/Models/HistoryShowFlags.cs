using System;

namespace Komorebi.Models;

/// <summary>
/// コミット履歴の表示オプションフラグ
/// </summary>
[Flags]
public enum HistoryShowFlags
{
    /// <summary>デフォルト（フラグなし）</summary>
    None = 0,
    /// <summary>reflogを表示する</summary>
    Reflog = 1 << 0,
    /// <summary>最初の親コミットのみ表示する</summary>
    FirstParentOnly = 1 << 1,
    /// <summary>デコレーション（ブランチ・タグ）付きコミットのみ表示する</summary>
    SimplifyByDecoration = 1 << 2,
}
