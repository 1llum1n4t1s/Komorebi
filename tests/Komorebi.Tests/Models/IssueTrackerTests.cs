using Komorebi.Models;
using Xunit;

namespace Komorebi.Tests.Models
{
    public class IssueTrackerTests
    {
        #region Property Defaults

        [Fact]
        public void NewInstance_HasNullRegex_IsRegexValidFalse()
        {
            var tracker = new IssueTracker();
            Assert.False(tracker.IsRegexValid);
        }

        [Fact]
        public void NewInstance_PropertiesAreNull()
        {
            var tracker = new IssueTracker();
            Assert.Null(tracker.Name);
            Assert.Null(tracker.RegexString);
            Assert.Null(tracker.URLTemplate);
            Assert.False(tracker.IsShared);
        }

        #endregion

        #region RegexString Validation

        [Fact]
        public void RegexString_ValidPattern_SetsIsRegexValid()
        {
            var tracker = new IssueTracker();
            tracker.RegexString = @"#(\d+)";
            Assert.True(tracker.IsRegexValid);
        }

        [Fact]
        public void RegexString_InvalidPattern_IsRegexValidFalse()
        {
            var tracker = new IssueTracker();
            tracker.RegexString = @"[invalid";
            Assert.False(tracker.IsRegexValid);
        }

        [Fact]
        public void RegexString_EmptyString_IsRegexValidFalse()
        {
            var tracker = new IssueTracker();
            tracker.RegexString = "";
            Assert.False(tracker.IsRegexValid);
        }

        [Fact]
        public void RegexString_UpdatedFromValidToInvalid_IsRegexValidChanges()
        {
            var tracker = new IssueTracker();
            tracker.RegexString = @"#(\d+)";
            Assert.True(tracker.IsRegexValid);

            tracker.RegexString = @"[invalid";
            Assert.False(tracker.IsRegexValid);
        }

        [Fact]
        public void RegexString_UpdatedFromInvalidToValid_IsRegexValidChanges()
        {
            var tracker = new IssueTracker();
            tracker.RegexString = @"[invalid";
            Assert.False(tracker.IsRegexValid);

            tracker.RegexString = @"#(\d+)";
            Assert.True(tracker.IsRegexValid);
        }

        #endregion

        #region Matches - Basic

        [Fact]
        public void Matches_NullRegex_DoesNothing()
        {
            var tracker = new IssueTracker();
            tracker.URLTemplate = "https://example.com/$1";
            var collector = new InlineElementCollector();
            tracker.Matches(collector, "test #123 message");
            Assert.Equal(0, collector.Count);
        }

        [Fact]
        public void Matches_NullURLTemplate_DoesNothing()
        {
            var tracker = new IssueTracker();
            tracker.RegexString = @"#(\d+)";
            var collector = new InlineElementCollector();
            tracker.Matches(collector, "test #123 message");
            Assert.Equal(0, collector.Count);
        }

        [Fact]
        public void Matches_EmptyURLTemplate_DoesNothing()
        {
            var tracker = new IssueTracker();
            tracker.RegexString = @"#(\d+)";
            tracker.URLTemplate = "";
            var collector = new InlineElementCollector();
            tracker.Matches(collector, "test #123 message");
            Assert.Equal(0, collector.Count);
        }

        [Fact]
        public void Matches_ValidPatternAndTemplate_FindsMatch()
        {
            var tracker = new IssueTracker();
            tracker.RegexString = @"#(\d+)";
            tracker.URLTemplate = "https://github.com/repo/issues/$1";
            var collector = new InlineElementCollector();

            tracker.Matches(collector, "Fix #123 bug");

            Assert.Equal(1, collector.Count);
            Assert.Equal(InlineElementType.Link, collector[0].Type);
            Assert.Equal(4, collector[0].Start); // "Fix " = 4 chars
            Assert.Equal(4, collector[0].Length); // "#123" = 4 chars
            Assert.Equal("https://github.com/repo/issues/123", collector[0].Link);
        }

        [Fact]
        public void Matches_MultipleMatches_FindsAll()
        {
            var tracker = new IssueTracker();
            tracker.RegexString = @"#(\d+)";
            tracker.URLTemplate = "https://example.com/issues/$1";
            var collector = new InlineElementCollector();

            tracker.Matches(collector, "Fix #123 and #456");

            Assert.Equal(2, collector.Count);
            Assert.Equal("https://example.com/issues/123", collector[0].Link);
            Assert.Equal("https://example.com/issues/456", collector[1].Link);
        }

        [Fact]
        public void Matches_NoMatch_AddsNothing()
        {
            var tracker = new IssueTracker();
            tracker.RegexString = @"#(\d+)";
            tracker.URLTemplate = "https://example.com/$1";
            var collector = new InlineElementCollector();

            tracker.Matches(collector, "No issues here");

            Assert.Equal(0, collector.Count);
        }

        #endregion

        #region Matches - URL Template Substitution

        [Fact]
        public void Matches_MultipleGroups_ReplacesAllPlaceholders()
        {
            var tracker = new IssueTracker();
            tracker.RegexString = @"(\w+)/(\w+)#(\d+)";
            tracker.URLTemplate = "https://github.com/$1/$2/issues/$3";
            var collector = new InlineElementCollector();

            tracker.Matches(collector, "See owner/repo#42 for details");

            Assert.Equal(1, collector.Count);
            Assert.Equal("https://github.com/owner/repo/issues/42", collector[0].Link);
        }

        #endregion

        #region Matches - Intersection Avoidance

        [Fact]
        public void Matches_OverlappingExistingElement_SkipsMatch()
        {
            var tracker = new IssueTracker();
            tracker.RegexString = @"#(\d+)";
            tracker.URLTemplate = "https://example.com/$1";
            var collector = new InlineElementCollector();

            // Pre-add an element that overlaps with where #123 would be (position 4, length 4)
            collector.Add(new InlineElement(InlineElementType.CommitSHA, 4, 10, "sha-link"));

            tracker.Matches(collector, "Fix #123 bug");

            // Should still be just 1 element (the pre-added one)
            Assert.Equal(1, collector.Count);
            Assert.Equal("sha-link", collector[0].Link);
        }

        #endregion

        #region Property Change Notifications

        [Fact]
        public void Name_SetProperty_RaisesPropertyChanged()
        {
            var tracker = new IssueTracker();
            bool raised = false;
            tracker.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(IssueTracker.Name))
                    raised = true;
            };
            tracker.Name = "GitHub Issues";
            Assert.True(raised);
        }

        [Fact]
        public void RegexString_SetProperty_RaisesPropertyChanged()
        {
            var tracker = new IssueTracker();
            var changedProps = new List<string>();
            tracker.PropertyChanged += (s, e) => changedProps.Add(e.PropertyName ?? string.Empty);

            tracker.RegexString = @"#(\d+)";

            Assert.Contains(nameof(IssueTracker.RegexString), changedProps);
            Assert.Contains(nameof(IssueTracker.IsRegexValid), changedProps);
        }

        #endregion
    }
}
