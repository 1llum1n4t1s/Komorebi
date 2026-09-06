using Komorebi.Models;
using Komorebi.ViewModels;

namespace Komorebi.Tests.ViewModels;

[CollectionDefinition("Diff loading preferences", DisableParallelization = true)]
public class DiffLoadingPreferencesCollection;

[Collection("Diff loading preferences")]
public class DiffLoadingTests
{
    [Fact]
    public void ReversedCompletion_PreservesLatestContentAndMetadata()
    {
        var option = new DiffOption(new Change { Path = "folder/" }, true);
        var context = new DiffContext(Path.GetTempPath(), option);
        var first = context.BeginLoadRequest();
        var second = context.BeginLoadRequest();
        var latest = new DiffResult { OldHash = "new-old", NewHash = "new-new", OldMode = "100644", NewMode = "100755" };
        var current = new BinaryDiff();
        context.ApplyLoadedContent(second, new DiffContext.Info(option, 8, true, latest), latest, current);
        var description = context.FileModeDescription;
        var old = new DiffResult { OldHash = "old-old", NewHash = "old-new" };
        context.ApplyLoadedContent(first, new DiffContext.Info(option, 4, false, old), old, new EmptyFile());
        Assert.Same(current, context.Content);
        Assert.Equal(description, context.FileModeDescription);
        // 古い結果でキャッシュだけが上書きされていないことも確認する。
        var third = context.BeginLoadRequest();
        context.ApplyLoadedContent(third, new DiffContext.Info(option, 8, true, latest), latest, new BinaryDiff());
        Assert.Same(current, context.Content);
    }

    [Fact]
    public void OldCompletion_BeforeLatestCompletionDoesNotFlashOldContent()
    {
        var option = new DiffOption(new Change { Path = "folder/" }, true);
        var context = new DiffContext(Path.GetTempPath(), option);
        var first = context.BeginLoadRequest();
        var second = context.BeginLoadRequest();
        var result = new DiffResult { OldHash = "old", NewHash = "new" };
        context.ApplyLoadedContent(first, new DiffContext.Info(option, 4, false, result), result, new BinaryDiff());
        Assert.Null(context.Content);
        var latest = new EmptyFile();
        context.ApplyLoadedContent(second, new DiffContext.Info(option, 5, false, result), result, latest);
        Assert.Same(latest, context.Content);
    }

    [Fact]
    public void SettingsChangedBack_InvalidatesPendingResult()
    {
        var preferences = Preferences.Instance;
        var original = preferences.IgnoreWhitespaceChangesInDiff;
        try
        {
            var option = new DiffOption(new Change { Path = "folder/" }, true);
            var context = new DiffContext(Path.GetTempPath(), option);
            preferences.IgnoreWhitespaceChangesInDiff = !original;
            context.CheckSettings();
            var pending = context.BeginLoadRequest();
            preferences.IgnoreWhitespaceChangesInDiff = original;
            context.CheckSettings();
            var result = new DiffResult { OldHash = "old", NewHash = "new" };
            context.ApplyLoadedContent(pending, new DiffContext.Info(option, 4, !original, result), result, new BinaryDiff());
            Assert.Null(context.Content);
        }
        finally
        {
            preferences.IgnoreWhitespaceChangesInDiff = original;
        }
    }

    [Fact]
    public void Cache_DistinguishesCRAtEOLSetting()
    {
        var option = new DiffOption(new Change { Path = "folder/" }, true);
        var result = new DiffResult { OldHash = "old", NewHash = "new" };
        Assert.False(new DiffContext.Info(option, 4, false, result, false)
            .IsSame(new DiffContext.Info(option, 4, false, result, true)));
    }

    [Fact]
    public void FileHistory_LateFileContentCannotReplaceDiffMode()
    {
        var revision = new FileVersion { SHA = "unused", Change = new Change { Path = "folder/" } };
        var context = new FileHistoriesSingleRevision(Path.GetTempPath(), revision, false);
        var pending = context.BeginContentRequest();
        context.IsDiffMode = true;
        var displayed = context.ViewContent;
        Assert.IsType<DiffContext>(displayed);
        context.ApplyRevisionContent(pending, new FileHistoriesRevisionFile("old"));
        Assert.Same(displayed, context.ViewContent);
    }

    [Fact]
    public void FileHistory_ReversedFileCompletionsKeepLatest()
    {
        var revision = new FileVersion { SHA = "unused", Change = new Change { Path = "folder/" } };
        var context = new FileHistoriesSingleRevision(Path.GetTempPath(), revision, false);
        var first = context.BeginContentRequest();
        var second = context.BeginContentRequest();
        var latest = new FileHistoriesRevisionFile("latest");
        context.ApplyRevisionContent(second, latest);
        context.ApplyRevisionContent(first, new FileHistoriesRevisionFile("old"));
        Assert.Same(latest, context.ViewContent);
    }
}
