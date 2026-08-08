using Komorebi.Commands;
using Komorebi.Models;

namespace Komorebi.Tests.Commands
{
    /// <summary>
    /// amend 中のアンステージで参照される diff-index 解析と、index 復元パッチ生成の回帰テスト。
    ///
    /// 入力行は実際の git で採取したもの。生成手順:
    ///   git init / commit (script.sh と gone.sh は 100755, keep.txt は 100644)
    ///   → script.sh を chmod -x + 内容変更 / keep.txt を削除 / gone.sh を renamed.txt へリネーム
    ///   → git commit → git diff-index --cached -M HEAD^
    ///
    /// 検証の要点は「復元に使う mode / hash は src 側（＝親コミット側）でなければならない」こと。
    /// dst 側（＝index 側）を使うと、親の内容に新しい mode を載せた壊れた index が出来上がる。
    /// </summary>
    public class QueryStagedChangesWithAmendTests
    {
        private const string ParentSHA = "1111111111111111111111111111111111111111";
        private const string SrcHash = "78981922613b2afb6025042ff6bd878ac1994e85";
        private const string DstHash = "61780798228d17af2d34fce4cfbdf35556832472";
        private const string RenamedDstHash = "93829c7b4af9dfbeea3b31395b042a614d7a190d";

        // 親では 100755、index では 100644（chmod -x が amend にステージ済み）
        private const string ModifiedLine =
            ":100755 100644 " + SrcHash + " " + DstHash + " M\tscript.sh";

        // 親では 100644、index では削除済み（dst-mode は 000000）
        private const string DeletedLine =
            ":100644 000000 " + SrcHash + " 0000000000000000000000000000000000000000 D\tkeep.txt";

        // 親では 100755 の gone.sh が、index では 100644 の renamed.txt にリネーム済み
        private const string RenamedLine =
            ":100755 100644 " + SrcHash + " " + RenamedDstHash + " R050\tgone.sh\trenamed.txt";

        // ---------------------------------------------------------------
        // ParseLine: src 側の mode / hash を捕捉する
        // ---------------------------------------------------------------

        [Fact]
        public void ParseLine_Modified_CapturesSourceModeNotDestination()
        {
            var change = QueryStagedChangesWithAmend.ParseLine(ModifiedLine, ParentSHA);

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Modified, change.Index);
            Assert.Equal("script.sh", change.Path);
            // dst-mode の 100644 を拾ってはならない
            Assert.Equal("100755", change.DataForAmend.FileMode);
            Assert.Equal(SrcHash, change.DataForAmend.ObjectHash);
            Assert.Equal(ParentSHA, change.DataForAmend.ParentSHA);
        }

        [Fact]
        public void ParseLine_Deleted_CapturesSourceModeNotZeroMode()
        {
            var change = QueryStagedChangesWithAmend.ParseLine(DeletedLine, ParentSHA);

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Deleted, change.Index);
            Assert.Equal("keep.txt", change.Path);
            // dst-mode は 000000。これを拾うと復元ではなく削除指示になる
            Assert.Equal("100644", change.DataForAmend.FileMode);
            Assert.Equal(SrcHash, change.DataForAmend.ObjectHash);
        }

        [Fact]
        public void ParseLine_Renamed_CapturesSourceModeAndSplitsPaths()
        {
            var change = QueryStagedChangesWithAmend.ParseLine(RenamedLine, ParentSHA);

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Renamed, change.Index);
            Assert.Equal("gone.sh", change.OriginalPath);
            Assert.Equal("renamed.txt", change.Path);
            Assert.Equal("100755", change.DataForAmend.FileMode);
            Assert.Equal(SrcHash, change.DataForAmend.ObjectHash);
        }

        [Fact]
        public void ParseLine_Added_HasZeroSourceMode()
        {
            var line = ":000000 100644 0000000000000000000000000000000000000000 " + DstHash + " A\tnew.txt";
            var change = QueryStagedChangesWithAmend.ParseLine(line, ParentSHA);

            Assert.NotNull(change);
            Assert.Equal(ChangeState.Added, change.Index);
            Assert.Equal("new.txt", change.Path);
            Assert.Equal("000000", change.DataForAmend.FileMode);
        }

        [Fact]
        public void ParseLine_TypeChanged_IsRecognized()
        {
            var line = ":100644 120000 " + SrcHash + " " + DstHash + " T\tlink";
            var change = QueryStagedChangesWithAmend.ParseLine(line, ParentSHA);

            Assert.NotNull(change);
            Assert.Equal(ChangeState.TypeChanged, change.Index);
            Assert.Equal("100644", change.DataForAmend.FileMode);
        }

        [Theory]
        [InlineData("")]
        [InlineData("garbage")]
        [InlineData(":100644 100644 short 0000 M\tfile")]
        public void ParseLine_InvalidLine_ReturnsNull(string line)
        {
            Assert.Null(QueryStagedChangesWithAmend.ParseLine(line, ParentSHA));
        }

        // ---------------------------------------------------------------
        // UpdateIndexInfo: 解析結果から mode を落とさずに復元パッチを組む
        // ---------------------------------------------------------------

        /// <summary>解析が成功する前提の行をパースする（失敗したらその場でテストを落とす）。</summary>
        private static Change Parse(string line)
        {
            var change = QueryStagedChangesWithAmend.ParseLine(line, ParentSHA);
            Assert.NotNull(change);
            return change;
        }

        [Fact]
        public void UpdateIndexInfo_Modified_RestoresExecutableBit()
        {
            var cmd = new UpdateIndexInfo("/repo", [Parse(ModifiedLine)]);

            Assert.Equal($"100755 {SrcHash}\tscript.sh\n", cmd.PatchContent);
        }

        [Fact]
        public void UpdateIndexInfo_Deleted_RestoresWithParentMode()
        {
            var cmd = new UpdateIndexInfo("/repo", [Parse(DeletedLine)]);

            Assert.Equal($"100644 {SrcHash}\tkeep.txt\n", cmd.PatchContent);
        }

        [Fact]
        public void UpdateIndexInfo_DeletedExecutable_KeepsExecutableBit()
        {
            var line = ":100755 000000 " + SrcHash + " 0000000000000000000000000000000000000000 D\trun.sh";
            var cmd = new UpdateIndexInfo("/repo", [Parse(line)]);

            // 100644 決め打ちだと実行ビットが落ちる
            Assert.Equal($"100755 {SrcHash}\trun.sh\n", cmd.PatchContent);
        }

        [Fact]
        public void UpdateIndexInfo_Renamed_RemovesNewPathAndRestoresOriginalWithParentMode()
        {
            var cmd = new UpdateIndexInfo("/repo", [Parse(RenamedLine)]);

            Assert.Equal(
                "0 0000000000000000000000000000000000000000\trenamed.txt\n" +
                $"100755 {SrcHash}\tgone.sh\n",
                cmd.PatchContent);
        }

        [Fact]
        public void UpdateIndexInfo_Added_RemovesEntryWithoutAmendData()
        {
            var change = new Change() { Path = "new.txt" };
            change.Set(ChangeState.Added);

            var cmd = new UpdateIndexInfo("/repo", [change]);

            Assert.False(cmd.HasUnrestorableEntry);
            Assert.Equal("0 0000000000000000000000000000000000000000\tnew.txt\n", cmd.PatchContent);
        }

        [Fact]
        public void UpdateIndexInfo_MissingAmendData_AbortsInsteadOfCorruptingIndex()
        {
            // amend 切替直後などで、まだ非 amend のステージド一覧（DataForAmend なし）が渡された場合
            var change = new Change() { Path = "script.sh" };
            change.Set(ChangeState.Modified);

            var cmd = new UpdateIndexInfo("/repo", [change]);

            Assert.True(cmd.HasUnrestorableEntry);
            Assert.Equal(string.Empty, cmd.PatchContent);
        }
    }
}
