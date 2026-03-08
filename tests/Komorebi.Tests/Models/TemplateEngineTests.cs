using Komorebi.Models;

namespace Komorebi.Tests.Models
{
    public class TemplateEngineTests
    {
        private readonly TemplateEngine _engine = new();

        #region Helpers

        private static Branch MakeBranch(string name)
        {
            return new Branch { Name = name, FullName = name, IsLocal = true };
        }

        private static List<Change> MakeChanges(params string[] paths)
        {
            var changes = new List<Change>();
            foreach (var path in paths)
            {
                changes.Add(new Change { Path = path });
            }
            return changes;
        }

        private string Eval(string template, string branchName = "main", params string[] filePaths)
        {
            var branch = MakeBranch(branchName);
            var changes = MakeChanges(filePaths);
            return _engine.Eval(template, branch, changes);
        }

        #endregion

        #region PlainText (no variables)

        [Fact]
        public void Eval_PlainText_ReturnsUnchanged()
        {
            var result = Eval("Hello World");
            Assert.Equal("Hello World", result);
        }

        [Fact]
        public void Eval_EmptyString_ReturnsEmpty()
        {
            var result = Eval("");
            Assert.Equal("", result);
        }

        [Fact]
        public void Eval_TextWithSpecialChars_ReturnsUnchanged()
        {
            var result = Eval("!@#%^&*()");
            Assert.Equal("!@#%^&*()", result);
        }

        [Fact]
        public void Eval_TextWithNewlines_PreservesNewlines()
        {
            var result = Eval("line1\nline2\nline3");
            Assert.Equal("line1\nline2\nline3", result);
        }

        #endregion

        #region Variable: branch_name

        [Fact]
        public void Eval_BranchNameVariable_ReturnsBranchName()
        {
            var result = Eval("${branch_name}", "feature/login");
            Assert.Equal("feature/login", result);
        }

        [Fact]
        public void Eval_BranchNameVariable_WithSurroundingText()
        {
            var result = Eval("Branch: ${branch_name} is active", "develop");
            Assert.Equal("Branch: develop is active", result);
        }

        [Fact]
        public void Eval_MultipleBranchNameVariables()
        {
            var result = Eval("${branch_name} and ${branch_name}", "main");
            Assert.Equal("main and main", result);
        }

        #endregion

        #region Variable: files_num

        [Fact]
        public void Eval_FilesNumVariable_ReturnsCount()
        {
            var result = Eval("${files_num}", "main", "file1.txt", "file2.txt", "file3.txt");
            Assert.Equal("3", result);
        }

        [Fact]
        public void Eval_FilesNumVariable_ZeroFiles()
        {
            var result = Eval("${files_num}", "main");
            Assert.Equal("0", result);
        }

        [Fact]
        public void Eval_FilesNumVariable_SingleFile()
        {
            var result = Eval("Changed ${files_num} file(s)", "main", "README.md");
            Assert.Equal("Changed 1 file(s)", result);
        }

        #endregion

        #region Variable: files

        [Fact]
        public void Eval_FilesVariable_ReturnsCommaSeparatedPaths()
        {
            var result = Eval("${files}", "main", "src/main.cs", "src/util.cs");
            Assert.Equal("src/main.cs, src/util.cs", result);
        }

        [Fact]
        public void Eval_FilesVariable_SingleFile()
        {
            var result = Eval("${files}", "main", "README.md");
            Assert.Equal("README.md", result);
        }

        [Fact]
        public void Eval_FilesVariable_NoFiles()
        {
            var result = Eval("${files}", "main");
            Assert.Equal("", result);
        }

        #endregion

        #region Variable: pure_files

        [Fact]
        public void Eval_PureFilesVariable_ReturnsFileNamesOnly()
        {
            var result = Eval("${pure_files}", "main", "src/Models/User.cs", "src/Views/Main.axaml");
            Assert.Equal("User.cs, Main.axaml", result);
        }

        [Fact]
        public void Eval_PureFilesVariable_SingleFile()
        {
            var result = Eval("${pure_files}", "main", "deep/nested/path/file.txt");
            Assert.Equal("file.txt", result);
        }

        [Fact]
        public void Eval_PureFilesVariable_AlreadyPureFilename()
        {
            var result = Eval("${pure_files}", "main", "README.md");
            Assert.Equal("README.md", result);
        }

        #endregion

        #region Sliced Variables: files:N

        [Fact]
        public void Eval_SlicedFiles_TruncatesWithRemainder()
        {
            var result = Eval(
                "${files:2}",
                "main",
                "a.txt", "b.txt", "c.txt", "d.txt");
            Assert.Equal("a.txt, b.txt and 2 other files", result);
        }

        [Fact]
        public void Eval_SlicedFiles_ExactCount_NoRemainder()
        {
            var result = Eval(
                "${files:3}",
                "main",
                "a.txt", "b.txt", "c.txt");
            Assert.Equal("a.txt, b.txt, c.txt", result);
        }

        [Fact]
        public void Eval_SlicedFiles_CountGreaterThanFiles_ShowsAll()
        {
            var result = Eval(
                "${files:10}",
                "main",
                "a.txt", "b.txt");
            Assert.Equal("a.txt, b.txt", result);
        }

        [Fact]
        public void Eval_SlicedFiles_OneFile_SliceOfOne()
        {
            var result = Eval(
                "${files:1}",
                "main",
                "a.txt", "b.txt", "c.txt");
            Assert.Equal("a.txt and 2 other files", result);
        }

        [Fact]
        public void Eval_SlicedPureFiles_TruncatesWithRemainder()
        {
            var result = Eval(
                "${pure_files:1}",
                "main",
                "src/a.txt", "src/b.txt", "src/c.txt");
            Assert.Equal("a.txt and 2 other files", result);
        }

        [Fact]
        public void Eval_SlicedPureFiles_ExactCount()
        {
            var result = Eval(
                "${pure_files:2}",
                "main",
                "src/a.txt", "lib/b.txt");
            Assert.Equal("a.txt, b.txt", result);
        }

        #endregion

        #region Regex Variables: name/regex/replacement

        [Fact]
        public void Eval_RegexVariable_BasicReplacement()
        {
            var result = Eval("${branch_name/feature\\//}", "feature/login");
            Assert.Equal("login", result);
        }

        [Fact]
        public void Eval_RegexVariable_WithReplacementText()
        {
            var result = Eval("${branch_name/feat/feature}", "feat/login");
            Assert.Equal("feature/login", result);
        }

        [Fact]
        public void Eval_RegexVariable_CaseInsensitive()
        {
            // RegexOptions includes IgnoreCase
            var result = Eval("${branch_name/MAIN/master}", "main");
            Assert.Equal("master", result);
        }

        [Fact]
        public void Eval_RegexVariable_NoMatch_ReturnsOriginal()
        {
            var result = Eval("${branch_name/xyz/abc}", "main");
            Assert.Equal("main", result);
        }

        [Fact]
        public void Eval_RegexVariable_EmptyVariable_ReturnsEmpty()
        {
            // Using an undefined variable with regex returns empty
            var result = Eval("${undefined_var/test/replace}", "main");
            Assert.Equal("", result);
        }

        [Fact]
        public void Eval_RegexVariable_CaptureGroupReplacement()
        {
            var result = Eval("${branch_name/^(\\w+)\\/(\\w+)$/$1-$2}", "feature/login");
            Assert.Equal("feature-login", result);
        }

        #endregion

        #region Undefined Variables

        [Fact]
        public void Eval_UndefinedVariable_ReturnsEmpty()
        {
            var result = Eval("${nonexistent}");
            Assert.Equal("", result);
        }

        [Fact]
        public void Eval_UndefinedVariable_SurroundingTextPreserved()
        {
            var result = Eval("before ${nonexistent} after");
            Assert.Equal("before  after", result);
        }

        [Fact]
        public void Eval_UndefinedSlicedVariable_ReturnsEmpty()
        {
            var result = Eval("${nonexistent:5}");
            Assert.Equal("", result);
        }

        #endregion

        #region Escape Sequences

        [Fact]
        public void Eval_EscapedDollar_TreatedAsLiteral()
        {
            var result = Eval("\\${branch_name}", "main");
            Assert.Equal("${branch_name}", result);
        }

        [Fact]
        public void Eval_EscapedBackslash_TreatedAsLiteral()
        {
            var result = Eval("path\\\\file", "main");
            Assert.Equal("path\\file", result);
        }

        [Fact]
        public void Eval_NonEscapeBackslash_PreservedAsIs()
        {
            // Backslash followed by something other than \ or $ is kept as-is
            var result = Eval("path\\nfile", "main");
            Assert.Equal("path\\nfile", result);
        }

        #endregion

        #region Malformed Variables (should be treated as plain text)

        [Fact]
        public void Eval_DollarSignAlone_TreatedAsText()
        {
            var result = Eval("cost is $5");
            Assert.Equal("cost is $5", result);
        }

        [Fact]
        public void Eval_DollarBraceNoClose_TreatedAsText()
        {
            var result = Eval("${branch_name");
            Assert.Equal("${branch_name", result);
        }

        [Fact]
        public void Eval_EmptyBraces_TreatedAsText()
        {
            var result = Eval("${}");
            Assert.Equal("${}", result);
        }

        [Fact]
        public void Eval_DollarWithoutBrace_TreatedAsText()
        {
            var result = Eval("$branch_name");
            Assert.Equal("$branch_name", result);
        }

        #endregion

        #region Complex Templates

        [Fact]
        public void Eval_ComplexTemplate_MixedVariablesAndText()
        {
            var result = Eval(
                "[${branch_name}] Update ${files_num} files: ${pure_files:2}",
                "feature/auth",
                "src/auth.cs", "src/login.cs", "src/token.cs", "tests/auth_test.cs");
            Assert.Equal("[feature/auth] Update 4 files: auth.cs, login.cs and 2 other files", result);
        }

        [Fact]
        public void Eval_ComplexTemplate_WithRegexAndSlice()
        {
            var result = Eval(
                "${branch_name/feature\\//}: ${files:1}",
                "feature/auth",
                "src/auth.cs", "src/login.cs");
            Assert.Equal("auth: src/auth.cs and 1 other files", result);
        }

        [Fact]
        public void Eval_ComplexTemplate_AllVariableTypes()
        {
            var result = Eval(
                "Branch ${branch_name}, ${files_num} files changed: ${files}",
                "main",
                "a.cs", "b.cs");
            Assert.Equal("Branch main, 2 files changed: a.cs, b.cs", result);
        }

        #endregion

        #region Reuse (engine is stateful with Reset)

        [Fact]
        public void Eval_CalledMultipleTimes_ResetsCorrectly()
        {
            var result1 = Eval("${branch_name}", "first");
            var result2 = Eval("${branch_name}", "second");

            Assert.Equal("first", result1);
            Assert.Equal("second", result2);
        }

        [Fact]
        public void Eval_CalledMultipleTimes_DifferentTemplates()
        {
            var result1 = Eval("${files_num}", "main", "a.cs");
            var result2 = Eval("${branch_name}", "develop");

            Assert.Equal("1", result1);
            Assert.Equal("develop", result2);
        }

        #endregion

        #region Edge Cases for Regex Variables

        [Fact]
        public void Eval_RegexVariable_InvalidRegex_TreatedAsText()
        {
            // Invalid regex pattern should result in the variable not being parsed
            var result = Eval("${branch_name/[invalid/replace}", "main");
            // If regex is invalid, TryParseRegexVariable returns null, variable is not created
            // So the ${ is treated as text
            Assert.Equal("${branch_name/[invalid/replace}", result);
        }

        [Fact]
        public void Eval_RegexVariable_EmptyPattern_TreatedAsText()
        {
            var result = Eval("${branch_name//replace}", "main");
            // Empty regex returns null from ParseRegex
            Assert.Equal("${branch_name//replace}", result);
        }

        [Fact]
        public void Eval_RegexVariable_EmptyReplacement_RemovesMatch()
        {
            var result = Eval("${branch_name/feature\\//}", "feature/test");
            Assert.Equal("test", result);
        }

        [Fact]
        public void Eval_RegexVariable_EscapedSlashInRegex()
        {
            // \/ inside regex variable should be treated as literal /
            var result = Eval("${branch_name/\\//-}", "feature/test");
            Assert.Equal("feature-test", result);
        }

        [Fact]
        public void Eval_RegexVariable_EscapedCloseBraceInReplacement()
        {
            // \} inside replacement should be treated as literal }
            var result = Eval("${branch_name/main/\\}}", "main");
            Assert.Equal("}", result);
        }

        #endregion

        #region Sliced Variable Edge Cases

        [Fact]
        public void Eval_SlicedFiles_ZeroCount()
        {
            var result = Eval("${files:0}", "main", "a.txt", "b.txt");
            Assert.Equal(" and 2 other files", result);
        }

        [Fact]
        public void Eval_SlicedFiles_NoFiles()
        {
            var result = Eval("${files:3}", "main");
            Assert.Equal("", result);
        }

        #endregion
    }
}
