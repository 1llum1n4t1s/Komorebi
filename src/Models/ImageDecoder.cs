namespace Komorebi.Models;

/// <summary>
///     画像デコーダーの種別。画像diff表示時に使用するデコーダーを指定する。
/// </summary>
public enum ImageDecoder
{
    /// <summary>デコーダーなし</summary>
    None = 0,
    /// <summary>組み込みデコーダー（標準画像フォーマット用）</summary>
    Builtin,
    /// <summary>Pfimデコーダー（DDS/TGA等のゲームテクスチャ用）</summary>
    Pfim,
    /// <summary>TIFFデコーダー</summary>
    Tiff,
}
