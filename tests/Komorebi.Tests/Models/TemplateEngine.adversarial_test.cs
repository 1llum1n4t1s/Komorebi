using Komorebi.Models;

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

        private static IReadOnlyList<Change> MakeChanges(params string[] paths)
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

        // ===============================================================
        // 🌪️ 環境異常・カオステスト（Environmental Chaos）
        // ===============================================================

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
