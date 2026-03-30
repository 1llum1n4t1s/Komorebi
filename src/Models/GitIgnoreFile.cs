using System.Collections.Generic;
using System.IO;
using Avalonia.Media;

namespace Komorebi.Models;

/// <summary>
/// Git無視ファイル（.gitignoreまたはexclude）の種別と操作を管理するクラス
/// </summary>
public class GitIgnoreFile
{
    /// <summary>サポートされている無視ファイルの種別一覧（共有/プライベート）</summary>
    public static readonly List<GitIgnoreFile> Supported = [new(true), new(false)];

    /// <summary>共有（.gitignore）かプライベート（exclude）か</summary>
    public bool IsShared { get; set; }
    /// <summary>表示用のファイルパス</summary>
    public string File => IsShared ? ".gitignore" : "<git_dir>/info/exclude";
    /// <summary>表示用の説明文（Shared/Private）</summary>
    public string Desc => IsShared ? "Shared" : "Private";
    /// <summary>種別に対応する表示色</summary>
    public IBrush Brush => IsShared ? Brushes.Green : Brushes.Gray;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="isShared">共有ファイル(.gitignore)かどうか</param>
    public GitIgnoreFile(bool isShared)
    {
        IsShared = isShared;
    }

    /// <summary>
    /// 実際のファイルシステム上のフルパスを取得する
    /// </summary>
    /// <param name="repoPath">リポジトリのルートパス</param>
    /// <param name="gitDir">.gitディレクトリのパス</param>
    /// <returns>無視ファイルのフルパス</returns>
    public string GetFullPath(string repoPath, string gitDir)
    {
        return IsShared ? Path.Combine(repoPath, ".gitignore") : Path.Combine(gitDir, "info", "exclude");
    }
}
