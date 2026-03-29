namespace Komorebi.Tests.ViewModels
{
    public class ConventionalCommitMessageBuilderTests
    {
        #region Helper

        /// <summary>
        /// Creates a builder with default types (no file override) and captures the
        /// generated message via the onApply callback.
        /// </summary>
        private static (Komorebi.ViewModels.ConventionalCommitMessageBuilder Builder, Func<string?> GetResult) CreateBuilder()
        {
            string? captured = null;
            var builder = new Komorebi.ViewModels.ConventionalCommitMessageBuilder(
                conventionalTypesOverride: null,
                onApply: msg => captured = msg);
            return (builder, () => captured);
        }

        #endregion

        // ------------------------------------------------------------------
        // Default types loading
        // ------------------------------------------------------------------

        [Fact]
        public void Constructor_LoadsDefaultTypes_WhenOverrideIsNull()
        {
            var (builder, _) = CreateBuilder();

            Assert.NotNull(builder.Types);
            Assert.True(builder.Types.Count > 0);
            Assert.Equal("feat", builder.Types[0].Type);
        }

        [Fact]
        public void Constructor_SelectedTypeIsFirstDefault()
        {
            var (builder, _) = CreateBuilder();

            Assert.NotNull(builder.SelectedType);
            Assert.Equal("feat", builder.SelectedType.Type);
        }

        // ------------------------------------------------------------------
        // PrefillShortDesc auto-population
        // ------------------------------------------------------------------

        [Fact]
        public void SelectedType_PrefillShortDesc_SetsDescription()
        {
            var (builder, _) = CreateBuilder();

            // Create a custom type with a PrefillShortDesc value
            var customType = new Komorebi.Models.ConventionalCommitType("Custom", "custom", "desc")
            {
                PrefillShortDesc = "auto filled"
            };
            builder.Types.Add(customType);
            builder.SelectedType = customType;

            Assert.Equal("auto filled", builder.Description);
        }

        [Fact]
        public void SelectedType_EmptyPrefillShortDesc_DoesNotOverwriteDescription()
        {
            var (builder, _) = CreateBuilder();
            builder.Description = "my description";

            // Select a type with empty PrefillShortDesc (default types have empty PrefillShortDesc)
            builder.SelectedType = builder.Types[1]; // "fix"

            Assert.Equal("my description", builder.Description);
        }

        // ------------------------------------------------------------------
        // Validation - Required fields
        // ------------------------------------------------------------------

        [Fact]
        public void Apply_ReturnsFalse_WhenDescriptionIsEmpty()
        {
            var (builder, getResult) = CreateBuilder();
            // SelectedType is already set to "feat" by default, but Description is empty.
            builder.Description = string.Empty;

            var result = builder.Apply();

            Assert.False(result);
            Assert.Null(getResult());
        }

        [Fact]
        public void Apply_ReturnsFalse_WhenSelectedTypeIsNull()
        {
            string? captured = null;
            var builder = new Komorebi.ViewModels.ConventionalCommitMessageBuilder(
                conventionalTypesOverride: null,
                onApply: msg => captured = msg);

            builder.SelectedType = null;
            builder.Description = "some description";

            var result = builder.Apply();

            Assert.False(result);
            Assert.Null(captured);
        }

        // ------------------------------------------------------------------
        // Basic message generation: type + description only
        // ------------------------------------------------------------------

        [Fact]
        public void Apply_GeneratesBasicMessage_TypeAndDescription()
        {
            var (builder, getResult) = CreateBuilder();
            builder.Description = "add login feature";

            var result = builder.Apply();

            Assert.True(result);
            var msg = getResult();
            Assert.NotNull(msg);
            Assert.StartsWith("feat: add login feature", msg);
        }

        // ------------------------------------------------------------------
        // Scope
        // ------------------------------------------------------------------

        [Fact]
        public void Apply_IncludesScope_WhenProvided()
        {
            var (builder, getResult) = CreateBuilder();
            builder.Description = "add login feature";
            builder.Scope = "auth";

            builder.Apply();

            var msg = getResult();
            Assert.StartsWith("feat(auth): add login feature", msg);
        }

        [Fact]
        public void Apply_NoScope_WhenScopeIsEmpty()
        {
            var (builder, getResult) = CreateBuilder();
            builder.Description = "add login feature";
            builder.Scope = string.Empty;

            builder.Apply();

            var msg = getResult();
            Assert.StartsWith("feat: add login feature", msg);
            Assert.DoesNotContain("()", msg);
        }

        // ------------------------------------------------------------------
        // Breaking changes
        // ------------------------------------------------------------------

        [Fact]
        public void Apply_AddsExclamationAndFooter_WhenBreakingChanges()
        {
            var (builder, getResult) = CreateBuilder();
            builder.Description = "remove old API";
            builder.BreakingChanges = "The v1 API is removed";

            builder.Apply();

            var msg = getResult();
            // Header should include "!" before ":"
            Assert.StartsWith("feat!: remove old API", msg);
            Assert.Contains("BREAKING CHANGE: The v1 API is removed", msg);
        }

        [Fact]
        public void Apply_NoExclamation_WhenBreakingChangesEmpty()
        {
            var (builder, getResult) = CreateBuilder();
            builder.Description = "minor fix";
            builder.BreakingChanges = string.Empty;

            builder.Apply();

            var msg = getResult();
            Assert.NotNull(msg);
            Assert.StartsWith("feat: minor fix", msg);
            Assert.DoesNotContain("!", msg.Split('\n')[0]);
            Assert.DoesNotContain("BREAKING CHANGE", msg);
        }

        // ------------------------------------------------------------------
        // Detail / body
        // ------------------------------------------------------------------

        [Fact]
        public void Apply_IncludesBody_WhenDetailProvided()
        {
            var (builder, getResult) = CreateBuilder();
            builder.Description = "add feature";
            builder.Detail = "This is a longer explanation of the change.";

            builder.Apply();

            var msg = getResult();
            Assert.Contains("This is a longer explanation of the change.", msg);
        }

        [Fact]
        public void Apply_NoBody_WhenDetailIsEmpty()
        {
            var (builder, getResult) = CreateBuilder();
            builder.Description = "add feature";
            builder.Detail = string.Empty;

            builder.Apply();

            var msg = getResult();
            Assert.NotNull(msg);
            // The message should not contain empty body artifacts beyond the required newlines
            var lines = msg.Split('\n');
            // First line is "feat: add feature", followed by blank lines
            Assert.StartsWith("feat: add feature", lines[0]);
        }

        // ------------------------------------------------------------------
        // Closed issue / footer
        // ------------------------------------------------------------------

        [Fact]
        public void Apply_IncludesClosedIssue_WhenProvided()
        {
            var (builder, getResult) = CreateBuilder();
            builder.Description = "fix crash";

            // Select "fix" type
            builder.SelectedType = builder.Types.Find(t => t.Type == "fix");
            builder.ClosedIssue = "#42";

            builder.Apply();

            var msg = getResult();
            Assert.Contains("Closed #42", msg);
        }

        // ------------------------------------------------------------------
        // Full combination: all fields populated
        // ------------------------------------------------------------------

        [Fact]
        public void Apply_GeneratesFullMessage_AllFieldsPopulated()
        {
            var (builder, getResult) = CreateBuilder();
            builder.SelectedType = builder.Types.Find(t => t.Type == "fix");
            builder.Scope = "parser";
            builder.Description = "handle null input";
            builder.Detail = "Previously the parser would crash on null.\nNow it returns an empty result.";
            builder.BreakingChanges = "Null input now returns empty instead of throwing";
            builder.ClosedIssue = "#123";

            var result = builder.Apply();
            Assert.True(result);

            var msg = getResult();
            Assert.StartsWith("fix(parser)!: handle null input", msg);
            Assert.Contains("Previously the parser would crash on null.", msg);
            Assert.Contains("BREAKING CHANGE: Null input now returns empty instead of throwing", msg);
            Assert.Contains("Closed #123", msg);
        }

        // ------------------------------------------------------------------
        // Different commit types
        // ------------------------------------------------------------------

        [Theory]
        [InlineData("feat", "feat: description")]
        [InlineData("fix", "fix: description")]
        [InlineData("docs", "docs: description")]
        [InlineData("refactor", "refactor: description")]
        [InlineData("test", "test: description")]
        [InlineData("chore", "chore: description")]
        [InlineData("ci", "ci: description")]
        [InlineData("perf", "perf: description")]
        [InlineData("build", "build: description")]
        [InlineData("style", "style: description")]
        [InlineData("revert", "revert: description")]
        [InlineData("wip", "wip: description")]
        public void Apply_UsesCorrectTypePrefix(string typeCode, string expectedStart)
        {
            var (builder, getResult) = CreateBuilder();
            builder.SelectedType = builder.Types.Find(t => t.Type == typeCode);
            builder.Description = "description";

            builder.Apply();

            var msg = getResult();
            Assert.StartsWith(expectedStart, msg);
        }

        // ------------------------------------------------------------------
        // Scope with breaking change together
        // ------------------------------------------------------------------

        [Fact]
        public void Apply_ScopeAndBreakingChange_FormatsCorrectly()
        {
            var (builder, getResult) = CreateBuilder();
            builder.Scope = "api";
            builder.Description = "change endpoint";
            builder.BreakingChanges = "endpoint path changed";

            builder.Apply();

            var msg = getResult();
            Assert.StartsWith("feat(api)!: change endpoint", msg);
        }

        // ------------------------------------------------------------------
        // Message structure ordering
        // ------------------------------------------------------------------

        [Fact]
        public void Apply_MessagePartsInCorrectOrder()
        {
            var (builder, getResult) = CreateBuilder();
            builder.SelectedType = builder.Types.Find(t => t.Type == "feat");
            builder.Description = "add feature";
            builder.Detail = "BODY_CONTENT";
            builder.BreakingChanges = "BREAK_CONTENT";
            builder.ClosedIssue = "#99";

            builder.Apply();

            var msg = getResult();
            Assert.NotNull(msg);
            var headerIdx = msg.IndexOf("feat: add feature");
            var bodyIdx = msg.IndexOf("BODY_CONTENT");
            var breakIdx = msg.IndexOf("BREAKING CHANGE: BREAK_CONTENT");
            var closedIdx = msg.IndexOf("Closed #99");

            Assert.True(headerIdx < bodyIdx, "Header should come before body");
            Assert.True(bodyIdx < breakIdx, "Body should come before breaking change");
            Assert.True(breakIdx < closedIdx, "Breaking change should come before closed issue");
        }
    }
}
