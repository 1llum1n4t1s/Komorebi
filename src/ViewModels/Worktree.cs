using System;
using System.Collections.Generic;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
/// ワークツリーの表示情報を管理するViewModel。
/// gitワークツリーの名前、ブランチ、パス、ロック状態などを提供する。
/// </summary>
public class Worktree : ObservableObject
{
    /// <summary>ワークツリーのモデルデータ。</summary>
    public Models.Worktree Backend { get; private set; }
    /// <summary>メインワークツリーかどうか。</summary>
    public bool IsMain { get; private set; }
    /// <summary>現在のリポジトリディレクトリと一致するワークツリーかどうか。</summary>
    public bool IsCurrent { get; private set; }
    /// <summary>リスト内の最後のワークツリーかどうか（UI表示用）。</summary>
    public bool IsLast { get; private set; }
    /// <summary>リポジトリからの相対パス表示。カレントの場合は空文字。</summary>
    public string DisplayPath { get; private set; }
    /// <summary>ワークツリーの表示名。</summary>
    public string Name { get; private set; }
    /// <summary>ワークツリーのブランチ名表示。</summary>
    public string Branch { get; private set; }

    /// <summary>
    /// ワークツリーがロックされているかどうか。
    /// </summary>
    public bool IsLocked
    {
        get => _isLocked;
        set => SetProperty(ref _isLocked, value);
    }

    /// <summary>ワークツリーのフルパス。</summary>
    public string FullPath => Backend.FullPath;
    /// <summary>HEADコミットのSHA。</summary>
    public string Head => Backend.Head;

    /// <summary>
    /// コンストラクタ。ワークツリーモデルから表示情報を初期化する。
    /// </summary>
    public Worktree(DirectoryInfo repo, Models.Worktree wt, bool isMain, bool isLast)
    {
        Backend = wt;
        IsMain = isMain;
        IsCurrent = IsCurrentWorktree(repo, wt);
        IsLast = isLast;
        // カレントワークツリーの場合は相対パスを表示しない
        DisplayPath = IsCurrent ? string.Empty : Path.GetRelativePath(repo.FullName, wt.FullPath);
        Name = GenerateName();
        Branch = GenerateBranchName();
        IsLocked = wt.IsLocked;
    }

    /// <summary>
    /// ワークツリーモデルリストからViewModel リストを構築する。
    /// ワークツリーが2つ以上ある場合のみリストを生成する。
    /// </summary>
    public static List<Worktree> Build(string repo, List<Models.Worktree> worktrees)
    {
        if (worktrees is not { Count: > 1 })
            return [];

        var repoDir = new DirectoryInfo(repo);
        List<Worktree> nodes = [];
        // 最初のエントリはメインワークツリー
        nodes.Add(new(repoDir, worktrees[0], true, false));
        for (int i = 1; i < worktrees.Count; i++)
            nodes.Add(new(repoDir, worktrees[i], false, i == worktrees.Count - 1));

        return nodes;
    }

    /// <summary>
    /// 指定されたブランチがこのワークツリーにアタッチされているか判定する。
    /// </summary>
    public bool IsAttachedTo(Models.Branch branch)
    {
        if (string.IsNullOrEmpty(branch.WorktreePath))
            return false;

        var wtDir = new DirectoryInfo(Backend.FullPath);
        var test = new DirectoryInfo(branch.WorktreePath);
        return test.FullName.Equals(wtDir.FullName, StringComparison.Ordinal);
    }

    /// <summary>
    /// リポジトリディレクトリとワークツリーのパスを比較して現在のワークツリーか判定する。
    /// </summary>
    private static bool IsCurrentWorktree(DirectoryInfo repo, Models.Worktree wt)
    {
        var wtDir = new DirectoryInfo(wt.FullPath);
        return wtDir.FullName.Equals(repo.FullName, StringComparison.Ordinal);
    }

    /// <summary>
    /// ワークツリーの表示名を生成する。
    /// メイン、デタッチドHEAD、ローカル/リモートブランチに応じて名前を決定する。
    /// </summary>
    private string GenerateName()
    {
        if (IsMain)
            return Path.GetFileName(Backend.FullPath);

        if (Backend.IsDetached)
            return $"detached HEAD at {Backend.Head.AsSpan(10)}";

        var b = Backend.Branch;

        // refs/heads/ プレフィックスを除去してローカルブランチ名を取得
        if (b.StartsWith("refs/heads/", StringComparison.Ordinal))
            return b.Substring(11);

        // refs/remotes/ プレフィックスを除去してリモートブランチ名を取得
        if (b.StartsWith("refs/remotes/", StringComparison.Ordinal))
            return b.Substring(13);

        return b;
    }

    /// <summary>
    /// ワークツリーのブランチ表示名を生成する。
    /// ベア、デタッチド、不明、通常ブランチに応じた文字列を返す。
    /// </summary>
    private string GenerateBranchName()
    {
        if (Backend.IsBare)
            return "-- (default)";

        if (Backend.IsDetached)
            return "-- (detached)";

        if (string.IsNullOrEmpty(Backend.Branch))
            return "-- (unknown)";

        var b = Backend.Branch;

        if (b.StartsWith("refs/heads/", StringComparison.Ordinal))
            return b.Substring(11);

        if (b.StartsWith("refs/remotes/", StringComparison.Ordinal))
            return b.Substring(13);

        return b;
    }

    private bool _isLocked = false;
}
