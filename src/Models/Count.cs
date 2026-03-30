using System;

namespace Komorebi.Models;

/// <summary>
/// カウント値を保持するDisposableなラッパークラス。
/// using文でスコープ管理しながらカウント値を受け渡すために使用される。
/// </summary>
public class Count : IDisposable
{
    /// <summary>
    /// カウント値。
    /// </summary>
    public int Value { get; set; } = 0;

    /// <summary>
    /// 指定されたカウント値でインスタンスを初期化する。
    /// </summary>
    /// <param name="value">カウント値。</param>
    public Count(int value)
    {
        Value = value;
    }

    /// <summary>
    /// リソースの解放（特に処理なし）。
    /// </summary>
    public void Dispose()
    {
        // Ignore
    }
}
