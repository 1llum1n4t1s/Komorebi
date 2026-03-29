using Avalonia.Media;

namespace Komorebi.Models;

/// <summary>
/// コミットのGPG/SSH署名検証情報を保持するクラス。
/// </summary>
public class CommitSignInfo
{
    /// <summary>
    /// 署名検証結果の文字コード（G=有効, B=不正, N=なし 等）。
    /// </summary>
    public char VerifyResult { get; init; } = 'N';

    /// <summary>
    /// 署名者の名前。
    /// </summary>
    public string Signer { get; init; } = string.Empty;

    /// <summary>
    /// 署名に使用された鍵のID。
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// 署名者情報が存在するかどうか。
    /// </summary>
    public bool HasSigner => !string.IsNullOrEmpty(Signer);

    /// <summary>
    /// 検証結果に応じた表示色のブラシを取得する。
    /// </summary>
    public IBrush Brush
    {
        get
        {
            // 検証結果に基づいて色を返す
            return VerifyResult switch
            {
                'G' or 'U' => Brushes.Green,           // 有効な署名（緑）
                'X' or 'Y' or 'R' => Brushes.DarkOrange, // 期限切れ・失効（橙）
                'B' or 'E' => Brushes.Red,              // 不正・検証不可（赤）
                _ => Brushes.Transparent,                // 署名なし（透明）
            };
        }
    }

    /// <summary>
    /// 検証結果の説明文を取得する。
    /// </summary>
    public string ToolTip
    {
        get
        {
            // 検証結果コードに対応する説明を返す
            return VerifyResult switch
            {
                'G' => "Good signature.",
                'U' => "Good signature with unknown validity.",
                'X' => "Good signature but has expired.",
                'Y' => "Good signature made by expired key.",
                'R' => "Good signature made by a revoked key.",
                'B' => "Bad signature.",
                'E' => "Signature cannot be checked.",
                _ => "No signature.",
            };
        }
    }
}
