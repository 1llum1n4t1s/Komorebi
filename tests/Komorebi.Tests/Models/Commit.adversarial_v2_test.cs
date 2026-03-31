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

    /// <summary>
    /// @adversarial @bug
    /// GetFriendlyName() はデコレータ無しの場合 SHA[..10] を返すが、
    /// SHAが10文字未満だと IndexOutOfRangeException が発生するバグ。
    /// </summary>
    [Theory]
    [InlineData("")]        // 空文字
    [InlineData("a")]       // 1文字
    [InlineData("abcdef")]  // 6文字
    [InlineData("123456789")] // 9文字（ギリギリ足りない）
    public void GetFriendlyName_SHAShorterThan10Chars_ThrowsIndexOutOfRange(string shortSha)
    {
        // SHAが10文字未満の場合、SHA[..10] でクラッシュする
        var commit = new Commit { SHA = shortSha };

        // バグの証明: 例外が投げられることを検証
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => commit.GetFriendlyName());
    }

    /// <summary>
    /// @adversarial @boundary
    /// SHAがちょうど10文字の場合は正常動作する境界値。
    /// </summary>
    [Fact]
    public void GetFriendlyName_SHAExactly10Chars_ReturnsFullSHA()
    {
        var commit = new Commit { SHA = "abcdef1234" };
        Assert.Equal("abcdef1234", commit.GetFriendlyName());
    }

    /// <summary>
    /// @adversarial @boundary
    /// SHAが11文字の場合は先頭10文字を返す。
    /// </summary>
    [Fact]
    public void GetFriendlyName_SHA11Chars_ReturnsTruncated()
    {
        var commit = new Commit { SHA = "abcdef12345" };
        Assert.Equal("abcdef1234", commit.GetFriendlyName());
    }

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
    }

    /// <summary>
    /// @adversarial @state
    /// ParseDecorators() 複数回呼び出しで同じデコレータが重複登録される。
    /// </summary>
    [Fact]
    public void ParseDecorators_SameDecoratorTwice_CreatesDuplicates()
    {
        var commit = new Commit();

        commit.ParseDecorators("refs/heads/main");
        commit.ParseDecorators("refs/heads/main");

        // 重複チェックなし → 2個になる
        Assert.Equal(2, commit.Decorators.Count);
        Assert.All(commit.Decorators, d => Assert.Equal("main", d.Name));
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

    /// <summary>
    /// @adversarial @boundary
    /// "tag: " だけ（8文字）→ "tag: refs/tags/" にマッチしない。
    /// </summary>
    [Fact]
    public void ParseDecorators_TagColonOnly_NoMatch()
    {
        var commit = new Commit();
        commit.ParseDecorators("tag: ");
        Assert.Empty(commit.Decorators);
    }

    #endregion

    #region Decorators.Sort - null Name のクラッシュテスト

    /// <summary>
    /// @adversarial @chaos
    /// Decorator.Name が null の場合、NumericSort.Compare() での挙動を確認。
    /// NumericSort.Compare は null を安全に処理する（s1 is null → return -1）。
    /// </summary>
    [Fact]
    public void DecoratorSort_WithNullName_DoesNotCrash()
    {
        var commit = new Commit();
        commit.Decorators.Add(new Decorator { Type = DecoratorType.Tag, Name = null! });
        commit.Decorators.Add(new Decorator { Type = DecoratorType.Tag, Name = "v1.0" });

        // ParseDecorators のソートロジックを手動で呼ぶ
        // NumericSort.Compare は null を -1 として処理するのでクラッシュしないはず
        var exception = Record.Exception(() =>
        {
            commit.Decorators.Sort((l, r) =>
            {
                var delta = (int)l.Type - (int)r.Type;
                if (delta != 0)
                    return delta;
                return NumericSort.Compare(l.Name, r.Name);
            });
        });

        Assert.Null(exception);
    }

    /// <summary>
    /// @adversarial @chaos
    /// 両方の Name が null の場合のソート。
    /// </summary>
    [Fact]
    public void DecoratorSort_BothNullNames_DoesNotCrash()
    {
        var commit = new Commit();
        commit.Decorators.Add(new Decorator { Type = DecoratorType.Tag, Name = null! });
        commit.Decorators.Add(new Decorator { Type = DecoratorType.Tag, Name = null! });

        var exception = Record.Exception(() =>
        {
            commit.Decorators.Sort((l, r) =>
            {
                var delta = (int)l.Type - (int)r.Type;
                if (delta != 0)
                    return delta;
                return NumericSort.Compare(l.Name, r.Name);
            });
        });

        Assert.Null(exception);
    }

    #endregion

    #region IsCurrentHead / HasDecorators - エッジケース

    /// <summary>
    /// @adversarial @boundary
    /// Decorators にタグだけがある場合の IsCurrentHead。
    /// </summary>
    [Fact]
    public void IsCurrentHead_OnlyTagDecorators_ReturnsFalse()
    {
        var commit = new Commit();
        commit.Decorators.Add(new Decorator { Type = DecoratorType.Tag, Name = "v1.0" });
        commit.Decorators.Add(new Decorator { Type = DecoratorType.Tag, Name = "v2.0" });

        Assert.False(commit.IsCurrentHead);
    }

    /// <summary>
    /// @adversarial @boundary
    /// Decorators を手動でクリアした後の HasDecorators。
    /// </summary>
    [Fact]
    public void HasDecorators_AfterClear_ReturnsFalse()
    {
        var commit = new Commit();
        commit.ParseDecorators("refs/heads/main, tag: refs/tags/v1.0");
        Assert.True(commit.HasDecorators);

        commit.Decorators.Clear();
        Assert.False(commit.HasDecorators);
    }

    /// <summary>
    /// @adversarial @state
    /// IsCurrentHead は DecoratorType.None を無視する。
    /// </summary>
    [Fact]
    public void IsCurrentHead_DecoratorTypeNone_ReturnsFalse()
    {
        var commit = new Commit();
        commit.Decorators.Add(new Decorator { Type = DecoratorType.None, Name = "something" });

        Assert.True(commit.HasDecorators); // デコレータはある
        Assert.False(commit.IsCurrentHead); // だが CurrentHead ではない
    }

    #endregion

    #region ParseDecorators - 特殊文字を含むブランチ名

    /// <summary>
    /// @adversarial @chaos
    /// ブランチ名にカンマを含む場合（git では許可されないが、パーサーの堅牢性テスト）。
    /// カンマはセパレータとして使われるため分割される。
    /// </summary>
    [Fact]
    public void ParseDecorators_BranchNameWithCommaLikePattern_SplitsByComma()
    {
        // "refs/heads/feat,ure" はカンマで分割されてしまう
        var commit = new Commit();
        commit.ParseDecorators("refs/heads/feat,ure");

        // "refs/heads/feat" と "ure" に分割される
        // "refs/heads/feat" → LocalBranchHead Name="feat"
        // "ure" → 3文字だがどのプレフィックスにもマッチしない
        Assert.Single(commit.Decorators);
        Assert.Equal("feat", commit.Decorators[0].Name);
    }

    /// <summary>
    /// @adversarial @chaos
    /// Unicode文字を含むブランチ名。
    /// </summary>
    [Fact]
    public void ParseDecorators_UnicodeBranchName_ParsesCorrectly()
    {
        var commit = new Commit();
        commit.ParseDecorators("refs/heads/機能/日本語ブランチ");

        Assert.Single(commit.Decorators);
        Assert.Equal(DecoratorType.LocalBranchHead, commit.Decorators[0].Type);
        Assert.Equal("機能/日本語ブランチ", commit.Decorators[0].Name);
    }

    /// <summary>
    /// @adversarial @chaos
    /// 非常に長いデコレータ文字列。
    /// </summary>
    [Fact]
    public void ParseDecorators_VeryLongBranchName_ParsesCorrectly()
    {
        var longName = new string('a', 10000);
        var commit = new Commit();
        commit.ParseDecorators($"refs/heads/{longName}");

        Assert.Single(commit.Decorators);
        Assert.Equal(longName, commit.Decorators[0].Name);
    }

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

    /// <summary>
    /// @adversarial @boundary
    /// 同じ Author/Committer かつ同じ時刻 → 非表示。
    /// </summary>
    [Fact]
    public void IsCommitterVisible_SameAuthorAndCommitter_ReturnsFalse()
    {
        var user = new User { Name = "Test", Email = "test@test.com" };
        var commit = new Commit
        {
            Author = user,
            Committer = user,
            AuthorTime = 1000,
            CommitterTime = 1000
        };

        Assert.False(commit.IsCommitterVisible);
    }

    /// <summary>
    /// @adversarial @boundary
    /// 同じ Author/Committer だが時刻が異なる → 表示。
    /// </summary>
    [Fact]
    public void IsCommitterVisible_SameUserDifferentTime_ReturnsTrue()
    {
        var user = new User { Name = "Test", Email = "test@test.com" };
        var commit = new Commit
        {
            Author = user,
            Committer = user,
            AuthorTime = 1000,
            CommitterTime = 1001
        };

        Assert.True(commit.IsCommitterVisible);
    }

    #endregion
}
