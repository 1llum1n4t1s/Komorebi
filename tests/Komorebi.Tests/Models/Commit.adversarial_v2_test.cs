using Komorebi.Models;

namespace Komorebi.Tests.Models;

/// <summary>
/// Commit クラスの敵対的テスト（第2弾）。
/// 既存の CommitTests.cs でカバーされていないギャップを狙う。
/// @adversarial @boundary @state @chaos
/// </summary>
public class CommitAdversarialV2Tests
{
    #region GetFriendlyName - SHAが10文字未満のバグ検出

    #endregion

    #region ParseDecorators - 複数回呼び出し時のアキュムレータ動作

    /// <summary>
    /// @adversarial @state
    /// ParseDecorators() を複数回呼ぶとデコレータが蓄積される。
    /// 呼び出し側がリセットを期待している場合のバグ源。
    /// </summary>
    [Fact]
    public void ParseDecorators_CalledMultipleTimes_AccumulatesDecorators()
    {
        var commit = new Commit();

        commit.ParseDecorators("refs/heads/main");
        Assert.Single(commit.Decorators);

        // 2回目の呼び出しで追加される（リセットされない）
        commit.ParseDecorators("tag: refs/tags/v1.0");
        Assert.Equal(2, commit.Decorators.Count);

        // 3回目
        commit.ParseDecorators("refs/remotes/origin/develop");
        Assert.Equal(3, commit.Decorators.Count);

        // Commit.adversarial_test.cs から統合: タグを続けて渡しても蓄積される
        commit.ParseDecorators("tag: refs/tags/v2.0");
        Assert.Equal(4, commit.Decorators.Count);
    }

    /// <summary>
    /// @adversarial @state
    /// ParseDecorators() を複数回呼ぶと IsMerged が一度 true になったら戻らない。
    /// </summary>
    [Fact]
    public void ParseDecorators_IsMergedSticksOnceTrue()
    {
        var commit = new Commit();

        // HEAD → IsMerged = true
        commit.ParseDecorators("HEAD -> refs/heads/main");
        Assert.True(commit.IsMerged);

        // その後タグだけ追加しても IsMerged は true のまま
        commit.ParseDecorators("tag: refs/tags/v1.0");
        Assert.True(commit.IsMerged);
    }

    /// <summary>
    /// @adversarial @state
    /// 複数回呼び出し後のソート順は最後の呼び出し時のみ適用される。
    /// 以前の呼び出しで追加されたデコレータとの整合性に注意。
    /// </summary>
    [Fact]
    public void ParseDecorators_MultipleCallsSortOnlyAppliesPerCall()
    {
        var commit = new Commit();

        // 1回目: タグを追加
        commit.ParseDecorators("tag: refs/tags/v1.0");
        Assert.Single(commit.Decorators);

        // 2回目: ブランチを追加 → ソートはこの呼び出し内のみ
        commit.ParseDecorators("refs/heads/main");

        // 全体のソートは2回目の呼び出しで行われるが、
        // 1回目のタグは既にリストにあり、2回目のソートは全リストに適用される
        Assert.Equal(2, commit.Decorators.Count);
        // Decorators.Sort() は全リストに対して動作する
        Assert.Equal(DecoratorType.LocalBranchHead, commit.Decorators[0].Type);
        Assert.Equal(DecoratorType.Tag, commit.Decorators[1].Type);
    }

    #endregion

    #region ParseDecorators - プレフィックス長より短い文字列

    /// <summary>
    /// @adversarial @boundary
    /// "tag: refs/tags/" は15文字。ちょうどプレフィックスだけで名前部分が空になるケース。
    /// </summary>
    [Fact]
    public void ParseDecorators_TagPrefixOnly_NoName_CreatesEmptyNameDecorator()
    {
        var commit = new Commit();
        commit.ParseDecorators("tag: refs/tags/");

        // StartsWith は true になり、d[15..] で空文字列が名前になる
        Assert.Single(commit.Decorators);
        Assert.Equal(DecoratorType.Tag, commit.Decorators[0].Type);
        Assert.Equal("", commit.Decorators[0].Name);
    }

    /// <summary>
    /// @adversarial @boundary
    /// "HEAD -> refs/heads/" (19文字) はプレフィックスのみで名前が空。
    /// </summary>
    [Fact]
    public void ParseDecorators_CurrentBranchPrefixOnly_CreatesEmptyNameDecorator()
    {
        var commit = new Commit();
        commit.ParseDecorators("HEAD -> refs/heads/");

        Assert.Single(commit.Decorators);
        Assert.Equal(DecoratorType.CurrentBranchHead, commit.Decorators[0].Type);
        Assert.Equal("", commit.Decorators[0].Name);
    }

    /// <summary>
    /// @adversarial @boundary
    /// "refs/heads/" (11文字) はプレフィックスのみ。
    /// </summary>
    [Fact]
    public void ParseDecorators_LocalBranchPrefixOnly_CreatesEmptyNameDecorator()
    {
        var commit = new Commit();
        commit.ParseDecorators("refs/heads/");

        Assert.Single(commit.Decorators);
        Assert.Equal(DecoratorType.LocalBranchHead, commit.Decorators[0].Type);
        Assert.Equal("", commit.Decorators[0].Name);
    }

    /// <summary>
    /// @adversarial @boundary
    /// "refs/remotes/" (13文字) はプレフィックスのみ。
    /// </summary>
    [Fact]
    public void ParseDecorators_RemoteBranchPrefixOnly_CreatesEmptyNameDecorator()
    {
        var commit = new Commit();
        commit.ParseDecorators("refs/remotes/");

        Assert.Single(commit.Decorators);
        Assert.Equal(DecoratorType.RemoteBranchHead, commit.Decorators[0].Type);
        Assert.Equal("", commit.Decorators[0].Name);
    }

    #endregion

    #region IsCurrentHead / HasDecorators - エッジケース

    #endregion

    #region ParseDecorators - 特殊文字を含むブランチ名

    /// <summary>
    /// @adversarial @chaos
    /// 空のカンマ区切りエントリ（連続カンマ）。
    /// </summary>
    [Fact]
    public void ParseDecorators_ConsecutiveCommas_IgnoresEmptyEntries()
    {
        var commit = new Commit();
        commit.ParseDecorators("refs/heads/main,,,,tag: refs/tags/v1.0");

        // StringSplitOptions.RemoveEmptyEntries により空エントリは除去される
        Assert.Equal(2, commit.Decorators.Count);
    }

    #endregion

    #region IsCommitterVisible - エッジケース

    #endregion
}
