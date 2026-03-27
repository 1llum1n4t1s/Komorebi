namespace Komorebi.ViewModels;

/// <summary>
///     テキスト差分ビューの表示行範囲を表すレコード。
///     Start=開始行インデックス、End=終了行インデックス。
/// </summary>
public record TextLineRange(int Start, int End);
