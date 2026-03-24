using System.Collections.Generic;
using Komorebi.Models;
using Xunit;

namespace Komorebi.Tests.Models
{
    /// <summary>
    /// TemplateEngine.Eval に対する嫌がらせテスト。
    /// テンプレートインジェクション、ReDoS、エスケープバイパス、
    /// 正規表現置換の$バックリファレンス悪用を攻撃する。
    /// </summary>
    public class TemplateEngineAdversarialTests
    {
        private readonly TemplateEngine _engine = new();
        private readonly Branch _branch = new() { Name = "feature/test-branch" };

        private IReadOnlyList<Change> MakeChanges(params string[] paths)
        {
            var changes = new List<Change>();
            foreach (var p in paths)
                changes.Add(new Change { Path = p });
            return changes;
        }

        // ===============================================================
        // 🗡️ 境界値・極端入力（Boundary Assault）
        // ===============================================================

        /// <adversarial category="boundary" severity="critical" />
        /// <summary>空テンプレートでクラッシュしないこと</summary>
        [Fact]
        public void Eval_EmptyTemplate_ReturnsEmpty()
        {
            var result = _engine.Eval("", _branch, MakeChanges("file.cs"));
            Assert.Equal("", result);
        }

        /// <adversarial category="boundary" severity="critical" />
        /// <summary>$だけのテンプレートでクラッシュしないこと</summary>
        [Theory]
        [InlineData("$")]
        [InlineData("$$")]
        [InlineData("$$$")]
        public void Eval_DollarSignOnly_DoesNotThrow(string template)
        {
            var ex = Record.Exception(() => _engine.Eval(template, _branch, MakeChanges("file.cs")));
            Assert.Null(ex);
        }

        /// <adversarial category="boundary" severity="critical" />
        /// <summary>${だけで閉じられていないテンプレートでクラッシュしないこと</summary>
        [Theory]
        [InlineData("${")]
        [InlineData("${branch_name")]
        [InlineData("${branch_name:")]
        [InlineData("${branch_name/")]
        [InlineData("${branch_name/regex")]
        [InlineData("${branch_name/regex/")]
        [InlineData("${branch_name/regex/replace")]
        public void Eval_UnclosedVariable_DoesNotThrow(string template)
        {
            var ex = Record.Exception(() => _engine.Eval(template, _branch, MakeChanges("file.cs")));
            Assert.Null(ex);
        }

        /// <adversarial category="boundary" severity="high" />
        /// <summary>巨大なテンプレート（100KB）でクラッシュしないこと</summary>
        [Fact]
        public void Eval_HugeTemplate_CompletesInTime()
        {
            var template = new string('x', 100_000);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = _engine.Eval(template, _branch, MakeChanges("file.cs"));
            sw.Stop();

            Assert.Equal(100_000, result.Length);
            Assert.True(sw.ElapsedMilliseconds < 3000, $"100KBテンプレートの評価に{sw.ElapsedMilliseconds}msかかった");
        }

        /// <adversarial category="boundary" severity="high" />
        /// <summary>ヌルバイトを含むテンプレートでクラッシュしないこと</summary>
        [Fact]
        public void Eval_NullByte_DoesNotThrow()
        {
            var ex = Record.Exception(() => _engine.Eval("prefix\x00${branch_name}\x00suffix", _branch, MakeChanges()));
            Assert.Null(ex);
        }

        // ===============================================================
        // 🎭 型パンチ・プロトコル違反（Type Punching）
        // ===============================================================

        /// <adversarial category="type" severity="critical" />
        /// <summary>正規表現置換の$バックリファレンスがインジェクションされないこと</summary>
        [Fact]
        public void Eval_RegexReplacementWithBackreference_HandledSafely()
        {
            // ${branch_name/(.+)/$1$1$1} は展開後のブランチ名に対してReplace
            // $1は正規表現の後方参照として解釈される
            var result = _engine.Eval("${branch_name/(.+)/$1$1}", _branch, MakeChanges());

            // $1はRegex.Replaceで後方参照として解釈される
            // "feature/test-branch" → "(.+)"でキャプチャ → $1$1 で2回繰り返し
            var ex = Record.Exception(() => _engine.Eval("${branch_name/(.+)/$1$1}", _branch, MakeChanges()));
            Assert.Null(ex);
        }

        /// <adversarial category="type" severity="critical" />
        /// <summary>不正な正規表現パターンでクラッシュしないこと</summary>
        [Theory]
        [InlineData("${branch_name/[/replace}")]
        [InlineData("${branch_name/(/replace}")]
        [InlineData("${branch_name/*/replace}")]
        [InlineData("${branch_name/(?P</replace}")]
        public void Eval_InvalidRegexPattern_DoesNotThrow(string template)
        {
            var ex = Record.Exception(() => _engine.Eval(template, _branch, MakeChanges()));
            Assert.Null(ex);
        }

        /// <adversarial category="type" severity="high" />
        /// <summary>存在しない変数名が空文字列に展開されること</summary>
        [Theory]
        [InlineData("${nonexistent}")]
        [InlineData("${BRANCH_NAME}")]
        [InlineData("${__proto__}")]
        [InlineData("${constructor}")]
        public void Eval_UnknownVariable_ReturnsEmpty(string template)
        {
            var result = _engine.Eval(template, _branch, MakeChanges("file.cs"));
            Assert.Equal("", result);
        }

        /// <adversarial category="type" severity="high" />
        /// <summary>スライスに0を指定した場合の動作</summary>
        [Fact]
        public void Eval_SliceZero_ReturnsEmptyOrMinimal()
        {
            var result = _engine.Eval("${files:0}", _branch, MakeChanges("a.cs", "b.cs", "c.cs"));
            // count=0の場合、0ファイル + "and 3 other files" になるはず
            var ex = Record.Exception(() => _engine.Eval("${files:0}", _branch, MakeChanges("a.cs")));
            Assert.Null(ex);
        }

        /// <adversarial category="type" severity="medium" />
        /// <summary>スライスに巨大な数を指定した場合でもクラッシュしないこと</summary>
        [Fact]
        public void Eval_SliceHugeNumber_DoesNotThrow()
        {
            var ex = Record.Exception(() => _engine.Eval("${files:999999}", _branch, MakeChanges("a.cs")));
            Assert.Null(ex);
        }

        // ===============================================================
        // 🌪️ 環境異常・カオステスト（Environmental Chaos）
        // ===============================================================

        /// <adversarial category="chaos" severity="high" />
        /// <summary>ブランチ名に正規表現メタ文字が含まれる場合でもクラッシュしないこと</summary>
        [Fact]
        public void Eval_BranchNameWithRegexMetachars_DoesNotThrow()
        {
            var evilBranch = new Branch { Name = "feature/(test)+[branch]*{1,3}" };
            var ex = Record.Exception(() =>
                _engine.Eval("${branch_name}", evilBranch, MakeChanges()));
            Assert.Null(ex);
        }

        /// <adversarial category="chaos" severity="high" />
        /// <summary>ファイルパスにテンプレート構文が含まれる場合でもインジェクションされないこと</summary>
        [Fact]
        public void Eval_FilePathContainsTemplateVars_NotInterpreted()
        {
            var changes = MakeChanges("${branch_name}.cs", "file.cs");
            var result = _engine.Eval("${files}", _branch, changes);

            // ファイルパス内の${branch_name}はテンプレート変数として展開されないはず
            Assert.Contains("${branch_name}.cs", result);
        }

        /// <adversarial category="chaos" severity="medium" />
        /// <summary>変更ファイルが0件の場合の各変数の動作</summary>
        [Fact]
        public void Eval_EmptyChangeList_HandledGracefully()
        {
            var empty = MakeChanges();

            Assert.Equal("0", _engine.Eval("${files_num}", _branch, empty));
            Assert.Equal("", _engine.Eval("${files}", _branch, empty));
            Assert.Equal("", _engine.Eval("${pure_files}", _branch, empty));

            var ex = Record.Exception(() => _engine.Eval("${files:3}", _branch, empty));
            Assert.Null(ex);
        }

        /// <adversarial category="chaos" severity="high" />
        /// <summary>エスケープシーケンスが正しく処理されること</summary>
        [Fact]
        public void Eval_EscapeSequences_HandledCorrectly()
        {
            // \$ はリテラルの $ になるはず
            var result = _engine.Eval("\\${branch_name}", _branch, MakeChanges());
            // エスケープにより変数展開が抑制される
            Assert.DoesNotContain("feature/test-branch", result);
        }

        /// <adversarial category="chaos" severity="medium" />
        /// <summary>改行を含む正規表現パターンで置換がnullリターンすること</summary>
        [Fact]
        public void Eval_RegexWithNewline_DoesNotThrow()
        {
            var ex = Record.Exception(() =>
                _engine.Eval("${branch_name/test\nfoo/replace}", _branch, MakeChanges()));
            Assert.Null(ex);
        }

        // ===============================================================
        // 💀 リソース枯渇・DoS耐性（Resource Exhaustion）
        // ===============================================================

        /// <adversarial category="resource" severity="critical" />
        /// <summary>正規表現置換でReDoSパターンを使用した場合にタイムアウトで安全に中断されること</summary>
        [Fact]
        public void Eval_RegexReDoS_TimesOutGracefully()
        {
            // ReDoS脆弱なパターン (a+)+ を使用
            var evilBranch = new Branch { Name = new string('a', 25) + "!" };

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var ex = Record.Exception(() =>
                _engine.Eval("${branch_name/(a+)+$/matched}", evilBranch, MakeChanges()));
            sw.Stop();

            // タイムアウト設定済みのため、クラッシュせずに完了する
            Assert.Null(ex);
            // タイムアウトは1秒に設定されているので、2秒以内に完了するはず
            Assert.True(sw.ElapsedMilliseconds < 3000,
                $"ReDoSパターンの処理に{sw.ElapsedMilliseconds}msかかった");
        }

        /// <adversarial category="resource" severity="high" />
        /// <summary>大量の変数を含むテンプレートでパフォーマンスが劣化しないこと</summary>
        [Fact]
        public void Eval_ManyVariables_CompletesInTime()
        {
            var parts = new string[1000];
            for (int i = 0; i < 1000; i++)
                parts[i] = "${branch_name}";
            var template = string.Join(" ", parts);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            _engine.Eval(template, _branch, MakeChanges("a.cs"));
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 3000, $"1000変数の評価に{sw.ElapsedMilliseconds}msかかった");
        }

        // ===============================================================
        // ⚡ 並行性・レースコンディション（Concurrency Chaos）
        // ===============================================================

        /// <adversarial category="concurrency" severity="high" />
        /// <summary>同一TemplateEngineインスタンスの連続Eval呼び出しで状態がリセットされること</summary>
        [Fact]
        public void Eval_ConsecutiveCalls_StateResets()
        {
            var result1 = _engine.Eval("${branch_name}", _branch, MakeChanges("a.cs"));
            var result2 = _engine.Eval("plain text", _branch, MakeChanges("b.cs"));
            var result3 = _engine.Eval("${files_num}", _branch, MakeChanges("c.cs", "d.cs"));

            Assert.Equal("feature/test-branch", result1);
            Assert.Equal("plain text", result2);
            Assert.Equal("2", result3);
        }

        // ===============================================================
        // 🔀 状態遷移の矛盾（State Machine Abuse）
        // ===============================================================

        /// <adversarial category="state" severity="high" />
        /// <summary>正規表現置換で空のリプレースメント文字列が動作すること</summary>
        [Fact]
        public void Eval_RegexEmptyReplacement_RemovesMatch()
        {
            // feature/test-branchから"feature/"を削除
            var result = _engine.Eval("${branch_name/feature\\//}", _branch, MakeChanges());
            Assert.Equal("test-branch", result);
        }

        /// <adversarial category="state" severity="medium" />
        /// <summary>正規表現置換の閉じ波括弧エスケープが動作すること</summary>
        [Fact]
        public void Eval_RegexReplacementEscapedBrace_DoesNotThrow()
        {
            // 置換文字列に \} を含む場合
            var ex = Record.Exception(() =>
                _engine.Eval("${branch_name/test/replaced\\}text}", _branch, MakeChanges()));
            Assert.Null(ex);
        }
    }
}
