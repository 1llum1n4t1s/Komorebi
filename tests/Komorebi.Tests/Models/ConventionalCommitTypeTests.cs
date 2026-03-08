using Komorebi.Models;
using Xunit;

namespace Komorebi.Tests.Models
{
    public class ConventionalCommitTypeTests
    {
        #region Constructor

        [Fact]
        public void Constructor_SetsAllProperties()
        {
            var type = new ConventionalCommitType("Features", "feat", "Adding a new feature");
            Assert.Equal("Features", type.Name);
            Assert.Equal("feat", type.Type);
            Assert.Equal("Adding a new feature", type.Description);
        }

        [Fact]
        public void PrefillShortDesc_DefaultsToEmpty()
        {
            var type = new ConventionalCommitType("Test", "test", "desc");
            Assert.Equal(string.Empty, type.PrefillShortDesc);
        }

        #endregion

        #region Load - Default Types

        [Fact]
        public void Load_NullPath_ReturnsDefaultTypes()
        {
            var types = ConventionalCommitType.Load(null);
            Assert.NotNull(types);
            Assert.NotEmpty(types);
        }

        [Fact]
        public void Load_EmptyPath_ReturnsDefaultTypes()
        {
            var types = ConventionalCommitType.Load("");
            Assert.NotNull(types);
            Assert.NotEmpty(types);
        }

        [Fact]
        public void Load_NonExistentPath_ReturnsDefaultTypes()
        {
            var types = ConventionalCommitType.Load("/nonexistent/path/that/does/not/exist.json");
            Assert.NotNull(types);
            Assert.NotEmpty(types);
        }

        [Fact]
        public void Load_DefaultTypes_HasExpectedCount()
        {
            var types = ConventionalCommitType.Load(null);
            Assert.Equal(12, types.Count);
        }

        [Fact]
        public void Load_DefaultTypes_AllHaveNonEmptyName()
        {
            var types = ConventionalCommitType.Load(null);
            foreach (var type in types)
            {
                Assert.False(string.IsNullOrWhiteSpace(type.Name),
                    $"ConventionalCommitType with Type='{type.Type}' has empty Name");
            }
        }

        [Fact]
        public void Load_DefaultTypes_AllHaveNonEmptyType()
        {
            var types = ConventionalCommitType.Load(null);
            foreach (var type in types)
            {
                Assert.False(string.IsNullOrWhiteSpace(type.Type),
                    $"ConventionalCommitType with Name='{type.Name}' has empty Type");
            }
        }

        [Fact]
        public void Load_DefaultTypes_AllHaveNonEmptyDescription()
        {
            var types = ConventionalCommitType.Load(null);
            foreach (var type in types)
            {
                Assert.False(string.IsNullOrWhiteSpace(type.Description),
                    $"ConventionalCommitType '{type.Name}' has empty Description");
            }
        }

        [Fact]
        public void Load_DefaultTypes_AllTypesAreUnique()
        {
            var types = ConventionalCommitType.Load(null);
            var typeValues = new HashSet<string>();
            foreach (var type in types)
            {
                Assert.True(typeValues.Add(type.Type),
                    $"Duplicate conventional commit type: '{type.Type}'");
            }
        }

        #endregion

        #region Expected Default Types

        [Theory]
        [InlineData("feat", "Features")]
        [InlineData("fix", "Bug Fixes")]
        [InlineData("wip", "Work In Progress")]
        [InlineData("revert", "Reverts")]
        [InlineData("refactor", "Code Refactoring")]
        [InlineData("perf", "Performance Improvements")]
        [InlineData("build", "Builds")]
        [InlineData("ci", "Continuous Integrations")]
        [InlineData("docs", "Documentations")]
        [InlineData("style", "Styles")]
        [InlineData("test", "Tests")]
        [InlineData("chore", "Chores")]
        public void Load_DefaultTypes_ContainsExpectedType(string expectedType, string expectedName)
        {
            var types = ConventionalCommitType.Load(null);
            var found = types.Find(t => t.Type == expectedType);
            Assert.NotNull(found);
            Assert.Equal(expectedName, found.Name);
        }

        #endregion
    }
}
