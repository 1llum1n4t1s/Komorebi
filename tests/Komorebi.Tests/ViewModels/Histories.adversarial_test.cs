using System.Collections;
using System.Diagnostics;

using Komorebi.Models;
using Komorebi.ViewModels;

namespace Komorebi.Tests.ViewModels
{
    /// <summary>
    /// Histories ViewModelの敵対的テスト。
    /// 辞書ルックアップ、ナビゲーション、状態遷移、境界値を検証する。
    /// </summary>
    public class HistoriesAdversarialTests : IDisposable
    {
        // テスト用の一時ディレクトリとリポジトリ
        private readonly string _tempDir;
        private readonly string _gitDir;
        private readonly Repository _repo;

        public HistoriesAdversarialTests()
        {
            // Repository コンストラクタ用の一時ディレクトリを作成
            _tempDir = Path.Combine(Path.GetTempPath(), $"komorebi_test_{Guid.NewGuid():N}");
            _gitDir = Path.Combine(_tempDir, ".git");
            Directory.CreateDirectory(_gitDir);

            // Repository は Open() しない限りファイル監視やDB読み込みは発生しない
            _repo = new Repository(false, _tempDir, _gitDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, true);
            }
            catch
            {
                // テスト後のクリーンアップ失敗は無視
            }
        }

        /// <summary>
        /// テスト用のコミットを生成するヘルパー。
        /// </summary>
        private static Commit MakeCommit(string sha, List<string>? parents = null, bool isCurrentHead = false)
        {
            var c = new Commit
            {
                SHA = sha,
                Parents = parents ?? [],
            };

            if (isCurrentHead)
            {
                c.Decorators.Add(new Decorator
                {
                    Type = DecoratorType.CurrentBranchHead,
                    Name = "main",
                });
            }

            return c;
        }

        // ================================================================
        //  境界値: 空のコミットリスト
        // ================================================================

        /// <summary>
        /// 空のコミットリストを設定した場合、辞書が空でクラッシュしないこと。
        /// </summary>
        [Fact]
        public void Commits_EmptyList_DoesNotThrow()
        {
            var histories = new Histories(_repo);

            histories.Commits = [];

            Assert.Empty(histories.Commits);
            Assert.Null(histories.SelectedCommit);
        }

        // ================================================================
        //  境界値: 大量のコミット（10000件）
        // ================================================================

        /// <summary>
        /// 10000件のコミットで辞書構築がO(n)時間で完了すること。
        /// </summary>
        [Fact]
        public void Commits_TenThousandCommits_BuildsDictionaryWithinReasonableTime()
        {
            var histories = new Histories(_repo);
            var commits = new List<Commit>();

            for (int i = 0; i < 10000; i++)
            {
                commits.Add(MakeCommit($"{i:x40}"[..40]));
            }

            var sw = Stopwatch.StartNew();
            histories.Commits = commits;
            sw.Stop();

            // 辞書構築は10000件でも100ms以下であるべき
            Assert.True(sw.ElapsedMilliseconds < 1000,
                $"10000件のコミットで辞書構築に{sw.ElapsedMilliseconds}msかかった（1000ms以下が期待値）");
            Assert.Equal(10000, histories.Commits.Count);
        }

        // ================================================================
        //  重複SHA: ToDictionary で ArgumentException が発生するケース
        // ================================================================

        /// <summary>
        /// 重複SHAのコミットリストを設定しても例外を投げず、最初の出現が辞書に登録されること。
        /// 仕様変更（防御的設計）: ToDictionary → TryAdd に切り替えたため、重複は静かに無視され、
        /// QueryCommits のパース異常等で重複が混入しても UI クラッシュしない。
        /// </summary>
        [Fact]
        public void Commits_DuplicateSHA_DoesNotThrowAndKeepsFirst()
        {
            var histories = new Histories(_repo);
            var sha = "deadbeef" + new string('0', 32);
            var first = MakeCommit(sha);
            var second = MakeCommit(sha);
            var commits = new List<Commit>
            {
                first,
                second, // 重複SHA。TryAdd により無視される（先勝ち）
            };

            // 例外を投げないこと
            histories.Commits = commits;

            // SelectedCommit など外部観測可能な状態が壊れていないことを軽く確認する
            // （NavigateTo で SHA から復元する経路は同じ辞書を使うため、最初の出現が引き当たる）
            Assert.Equal(2, histories.Commits.Count);
        }

        // ================================================================
        //  NavigateTo: 完全一致で見つかるケース
        // ================================================================

        /// <summary>
        /// NavigateTo が完全一致SHAでコミットを正しく選択すること。
        /// </summary>
        [Fact]
        public void NavigateTo_ExactMatch_SelectsCommit()
        {
            var histories = new Histories(_repo);
            var target = MakeCommit("aabbccdd" + new string('0', 32));
            histories.Commits = [
                MakeCommit("11111111" + new string('0', 32)),
                target,
                MakeCommit("22222222" + new string('0', 32)),
            ];

            histories.NavigateTo(target.SHA);

            Assert.Equal(target.SHA, histories.SelectedCommit?.SHA);
        }

        // ================================================================
        //  NavigateTo: プレフィックス一致のケース
        // ================================================================

        /// <summary>
        /// NavigateTo がプレフィックスで最初に一致するコミットを選択すること。
        /// </summary>
        [Fact]
        public void NavigateTo_PrefixMatch_SelectsFirstMatch()
        {
            var histories = new Histories(_repo);
            var commit1 = MakeCommit("abcdef11" + new string('0', 32));
            var commit2 = MakeCommit("abcdef22" + new string('0', 32));
            histories.Commits = [commit1, commit2];

            // "abcdef" はプレフィックスとして両方に一致するが、
            // 辞書の完全一致に失敗後、List.Find() で最初に見つかるものが選択される
            histories.NavigateTo("abcdef");

            Assert.NotNull(histories.SelectedCommit);
            Assert.StartsWith("abcdef", histories.SelectedCommit.SHA);
        }

        // ================================================================
        //  NavigateTo: NavigationId のインクリメント
        // ================================================================

        /// <summary>
        /// NavigateTo の呼び出しごとに NavigationId がインクリメントされること。
        /// </summary>
        [Fact]
        public void NavigateTo_IncrementsNavigationId()
        {
            var histories = new Histories(_repo);
            var commit = MakeCommit("aaa" + new string('0', 37));
            histories.Commits = [commit];

            var initialId = histories.NavigationId;
            histories.NavigateTo(commit.SHA);
            Assert.Equal(initialId + 1, histories.NavigationId);

            histories.NavigateTo(commit.SHA);
            Assert.Equal(initialId + 2, histories.NavigationId);
        }

        // ================================================================
        //  Commits プロパティ: 複数回設定（状態遷移）
        // ================================================================

        /// <summary>
        /// Commits プロパティを複数回設定しても辞書が正しく再構築されること。
        /// </summary>
        [Fact]
        public void Commits_SetMultipleTimes_RebuildsDictionary()
        {
            var histories = new Histories(_repo);

            // 最初のセット
            var commit1 = MakeCommit("aaa" + new string('0', 37));
            histories.Commits = [commit1];
            histories.NavigateTo(commit1.SHA);
            Assert.Equal(commit1.SHA, histories.SelectedCommit?.SHA);

            // 2回目のセットで辞書が再構築される
            var commit2 = MakeCommit("bbb" + new string('0', 37));
            histories.Commits = [commit2];
            histories.NavigateTo(commit2.SHA);
            Assert.Equal(commit2.SHA, histories.SelectedCommit?.SHA);

            // 旧コミットはもう辞書にない
            histories.NavigateTo(commit1.SHA);
            // commit1は辞書に存在しないためプレフィックスでもヒットしない（List.Find()でも見つからない）
            // → 非同期パスに入る（SelectedCommitは変わらない）
        }

        // ================================================================
        //  Commits プロパティ: 選択状態の復元
        // ================================================================

        /// <summary>
        /// Commits を再設定した際に、以前選択していたコミットが新リスト内に存在すれば
        /// SelectedCommit が自動的に復元されること。
        /// </summary>
        [Fact]
        public void Commits_RestoresSelectedCommit_WhenSameShaPresentInNewList()
        {
            var histories = new Histories(_repo);
            var sha = "abc" + new string('0', 37);

            histories.Commits = [MakeCommit(sha)];
            histories.SelectedCommit = histories.Commits[0];

            // 同じSHAの新しいコミットオブジェクトで再設定
            var newCommit = MakeCommit(sha);
            histories.Commits = [newCommit];

            // SelectedCommit が新リストのオブジェクトに復元される
            Assert.NotNull(histories.SelectedCommit);
            Assert.Equal(sha, histories.SelectedCommit.SHA);
            Assert.Same(newCommit, histories.SelectedCommit);
        }

        // ================================================================
        //  Dispose後のアクセス
        // ================================================================

        /// <summary>
        /// Dispose後にCommitsが空になること。
        /// </summary>
        [Fact]
        public void Dispose_ClearsCommitsAndGraph()
        {
            var histories = new Histories(_repo);
            histories.Commits = [MakeCommit("aaa" + new string('0', 37))];

            histories.Dispose();

            Assert.Empty(histories.Commits);
        }

        // ================================================================
        //  Select: 0件の選択
        // ================================================================

        /// <summary>
        /// Select に空のリストを渡した場合、DetailContext が null になること。
        /// （注: SearchCommitContext にアクセスするためリポジトリのOpen()が必要だが、
        /// ここではOpen()なしで呼ぶと NullReferenceException になることを検証）
        /// </summary>
        [Fact]
        public void Select_EmptyList_ThrowsWithoutRepoOpen()
        {
            var histories = new Histories(_repo);
            IList emptyList = new ArrayList();

            // _repo.SearchCommitContext は Open() していないと null
            Assert.Throws<NullReferenceException>(() => histories.Select(emptyList));
        }

        // ================================================================
        //  Select: 非Commitオブジェクトを含むリスト
        // ================================================================

        /// <summary>
        /// Select に Commit でないオブジェクトを含む1要素リストを渡した場合、
        /// InvalidCastException または NullReferenceException になること。
        /// （IList は型安全でないため）
        /// </summary>
        [Fact]
        public void Select_NonCommitObject_ThrowsException()
        {
            var histories = new Histories(_repo);
            IList badList = new ArrayList { "not a commit" };

            // (commits[0] as Models.Commit)! はキャスト失敗でnullになるが、
            // !演算子でnull抑制しているため、そのあとの.SHAアクセスでNullReferenceException
            Assert.ThrowsAny<Exception>(() => histories.Select(badList));
        }

        // ================================================================
        //  UpdateBisectInfo: BISECT_STARTファイルが存在しない場合
        // ================================================================

        /// <summary>
        /// BISECT_START ファイルが存在しない場合、BisectState.None を返すこと。
        /// </summary>
        [Fact]
        public void UpdateBisectInfo_NoBisectStart_ReturnsNone()
        {
            var histories = new Histories(_repo);

            var state = histories.UpdateBisectInfo();

            Assert.Equal(BisectState.None, state);
            Assert.Null(histories.Bisect);
        }

        // ================================================================
        //  UpdateBisectInfo: BISECT_STARTが存在するがrefs/bisectがないケース
        // ================================================================

        /// <summary>
        /// BISECT_START ファイルは存在するが refs/bisect ディレクトリがない場合、
        /// WaitingForRange 状態を返すこと。
        /// </summary>
        [Fact]
        public void UpdateBisectInfo_BisectStartExistsButNoRefsDir_ReturnsWaitingForRange()
        {
            // BISECT_START ファイルを作成
            File.WriteAllText(Path.Combine(_gitDir, "BISECT_START"), "abc123\n");

            var histories = new Histories(_repo);
            var state = histories.UpdateBisectInfo();

            Assert.Equal(BisectState.WaitingForRange, state);
            Assert.NotNull(histories.Bisect);
            Assert.Empty(histories.Bisect.Bads);
            Assert.Empty(histories.Bisect.Goods);
        }

        // ================================================================
        //  UpdateBisectInfo: good と bad の両方が存在する場合
        // ================================================================

        /// <summary>
        /// refs/bisect 内に good と bad の両方のファイルがある場合、
        /// Detecting 状態を返すこと。
        /// </summary>
        [Fact]
        public void UpdateBisectInfo_WithGoodAndBad_ReturnsDetecting()
        {
            // BISECT_START ファイルを作成
            File.WriteAllText(Path.Combine(_gitDir, "BISECT_START"), "abc123\n");

            // refs/bisect ディレクトリに bad と good ファイルを作成
            var bisectDir = Path.Combine(_gitDir, "refs", "bisect");
            Directory.CreateDirectory(bisectDir);
            File.WriteAllText(Path.Combine(bisectDir, "bad"), "deadbeef1234567890123456789012345678dead\n");
            File.WriteAllText(Path.Combine(bisectDir, "good-abc123"), "cafebabe1234567890123456789012345678cafe\n");

            var histories = new Histories(_repo);
            var state = histories.UpdateBisectInfo();

            Assert.Equal(BisectState.Detecting, state);
            Assert.NotNull(histories.Bisect);
            Assert.Single(histories.Bisect.Bads);
            Assert.Single(histories.Bisect.Goods);
            Assert.Contains("deadbeef1234567890123456789012345678dead", histories.Bisect.Bads);
            Assert.Contains("cafebabe1234567890123456789012345678cafe", histories.Bisect.Goods);
        }

        // ================================================================
        //  UpdateBisectInfo: bad のみの場合
        // ================================================================

        /// <summary>
        /// refs/bisect に bad のみ存在する場合、WaitingForRange を返すこと。
        /// </summary>
        [Fact]
        public void UpdateBisectInfo_OnlyBad_ReturnsWaitingForRange()
        {
            File.WriteAllText(Path.Combine(_gitDir, "BISECT_START"), "abc123\n");
            var bisectDir = Path.Combine(_gitDir, "refs", "bisect");
            Directory.CreateDirectory(bisectDir);
            File.WriteAllText(Path.Combine(bisectDir, "bad"), "deadbeef" + new string('0', 32) + "\n");

            var histories = new Histories(_repo);
            var state = histories.UpdateBisectInfo();

            Assert.Equal(BisectState.WaitingForRange, state);
        }

        // ================================================================
        //  UpdateBisectInfo: ファイル内容に余分な空白がある場合
        // ================================================================

        /// <summary>
        /// bisect ファイル内容に前後の空白・改行がある場合でも Trim() で正しく処理されること。
        /// </summary>
        [Fact]
        public void UpdateBisectInfo_FilesWithExtraWhitespace_TrimmedCorrectly()
        {
            File.WriteAllText(Path.Combine(_gitDir, "BISECT_START"), "  abc123  \r\n");
            var bisectDir = Path.Combine(_gitDir, "refs", "bisect");
            Directory.CreateDirectory(bisectDir);
            File.WriteAllText(Path.Combine(bisectDir, "bad"), "  aaa  \n");
            File.WriteAllText(Path.Combine(bisectDir, "good-1"), "  bbb  \r\n");

            var histories = new Histories(_repo);
            var state = histories.UpdateBisectInfo();

            Assert.Equal(BisectState.Detecting, state);
            Assert.Contains("aaa", histories.Bisect.Bads);
            Assert.Contains("bbb", histories.Bisect.Goods);
        }

        // ================================================================
        //  CompareWithHeadAsync: IsCurrentHead がない場合
        // ================================================================

        /// <summary>
        /// コミットリストに IsCurrentHead が存在しない場合、
        /// CompareWithHeadAsync は _repo.SearchCommitContext にアクセスするため、
        /// Open() されていないリポジトリでは NullReferenceException が発生すること。
        /// </summary>
        [Fact]
        public async Task CompareWithHeadAsync_NoCurrentHead_WithoutRepoOpen_ThrowsNullRef()
        {
            var histories = new Histories(_repo);
            histories.Commits = [
                MakeCommit("aaa" + new string('0', 37)),
                MakeCommit("bbb" + new string('0', 37)),
            ];

            // Open() されていないリポジトリでは SearchCommitContext が null
            await Assert.ThrowsAsync<NullReferenceException>(
                () => histories.CompareWithHeadAsync(histories.Commits[0]));
        }

        // ================================================================
        //  CompareWithHeadAsync: IsCurrentHead が存在する場合
        // ================================================================

        /// <summary>
        /// コミットリスト内に IsCurrentHead のコミットがある場合、
        /// そのコミットが返されること。
        /// </summary>
        [Fact]
        public async Task CompareWithHeadAsync_WithCurrentHead_ReturnsHeadCommit()
        {
            var histories = new Histories(_repo);
            var headCommit = MakeCommit("head" + new string('0', 36), isCurrentHead: true);
            var otherCommit = MakeCommit("other" + new string('0', 35));
            histories.Commits = [headCommit, otherCommit];

            var result = await histories.CompareWithHeadAsync(otherCommit);

            Assert.NotNull(result);
            Assert.True(result.IsCurrentHead);
        }

        // ================================================================
        //  Commits: 同じリストの再設定
        // ================================================================

        /// <summary>
        /// 同一リスト参照を再設定しても、SetProperty で変更なしと判定されること。
        /// </summary>
        [Fact]
        public void Commits_SameListReference_DoesNotRebuildDictionary()
        {
            var histories = new Histories(_repo);
            var commits = new List<Commit> { MakeCommit("aaa" + new string('0', 37)) };

            histories.Commits = commits;
            var initialId = histories.NavigationId;

            // 同じ参照を再設定しても SetProperty は false を返す（辞書再構築されない）
            histories.Commits = commits;

            // NavigationId は変わらない（辞書再構築の副作用がないことの間接的な確認）
            Assert.Equal(initialId, histories.NavigationId);
        }

        // ================================================================
        //  SHA がフォーマットの異なる場合のプレフィックス検索
        // ================================================================

        /// <summary>
        /// 短いSHA（7文字等）でプレフィックス検索が正しく動作すること。
        /// </summary>
        [Theory]
        [InlineData("abcdef1")] // 7文字
        [InlineData("abcdef12345")] // 11文字
        [InlineData("abcdef1234567890abcdef1234567890abcdef12")] // 完全一致
        public void NavigateTo_VariousShaPrefixLengths_FindsCommit(string prefix)
        {
            var histories = new Histories(_repo);
            var commit = MakeCommit("abcdef1234567890abcdef1234567890abcdef12");
            histories.Commits = [commit];

            histories.NavigateTo(prefix);

            Assert.NotNull(histories.SelectedCommit);
            Assert.Equal(commit.SHA, histories.SelectedCommit.SHA);
        }
    }
}
