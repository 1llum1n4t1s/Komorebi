using System.Reflection;
using Avalonia.Media.Imaging;
using Avalonia.Skia;
using Komorebi.Models;
using Komorebi.ViewModels;
using SkiaSharp;
using GitObject = Komorebi.Models.Object;

namespace Komorebi.Tests.ViewModels;

[CollectionDefinition("Revision file loading renderer", DisableParallelization = true)]
public class RevisionFileLoadingRendererCollection;

[Collection("Revision file loading renderer")]
public class RevisionFileLoadingTests
{
    static RevisionFileLoadingTests()
    {
        // アプリが既に参照するCPU描画バックエンドだけを初期化する。GUIは起動しない。
        SkiaPlatform.Initialize();
    }

    [Theory]
    [InlineData(ObjectType.Blob)]
    [InlineData(ObjectType.Commit)]
    public async Task ReversedCompletion_PreservesLatestSelection(ObjectType type)
    {
        var loads = new List<TaskCompletionSource<object>>();
        using var detail = CreateDetail((_, _, _) =>
        {
            var source = new TaskCompletionSource<object>();
            loads.Add(source);
            return source.Task;
        });
        var first = detail.ViewRevisionFileAsync(new() { Path = "a", Type = type });
        var second = detail.ViewRevisionFileAsync(new() { Path = "b", Type = type });
        var latest = new RevisionTextFile { FileName = "b", Content = "latest" };
        loads[1].SetResult(latest);
        await second;
        loads[0].SetResult(new RevisionTextFile { FileName = "a", Content = "old" });
        await first;

        Assert.Equal("b", detail.ViewRevisionFilePath);
        Assert.Same(latest, detail.ViewRevisionFileContent);
        Assert.Equal(type == ObjectType.Blob, detail.CanOpenRevisionFileWithDefaultEditor);
    }

    [Theory]
    [InlineData("clear")]
    [InlineData("commit")]
    [InlineData("dispose")]
    public async Task InvalidatedSelection_CannotBeRevived(string operation)
    {
        var pending = new TaskCompletionSource<object>();
        var loads = 0;
        using var detail = CreateDetail((_, _, _) =>
        {
            ++loads;
            return pending.Task;
        });
        var loading = detail.ViewRevisionFileAsync(new() { Path = "a", Type = ObjectType.Blob });
        if (operation == "clear")
            await detail.ViewRevisionFileAsync(null!);
        else if (operation == "commit")
            detail.Commit = null!;
        else
            detail.Dispose();

        var notifications = new List<string?>();
        detail.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        using var pixels = new SKBitmap(1, 1);
        pixels.Erase(SKColors.White);
        using var image = SKImage.FromBitmap(pixels);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        using var imageStream = encoded.AsStream();
        var bitmap = new CountingBitmap(imageStream);
        pending.SetResult(new RevisionImageFile("a.png", bitmap, encoded.Size));
        await loading;
        Assert.Null(detail.ViewRevisionFileContent);
        Assert.Empty(notifications);
        Assert.Equal(1, bitmap.DisposeCount);
        if (operation == "dispose")
            await detail.ViewRevisionFileAsync(new() { Path = "b", Type = ObjectType.Blob });
        Assert.Equal(1, loads);
    }

    [Fact]
    public async Task CommitSwitch_PreservesNewRevisionContent()
    {
        var pending = new TaskCompletionSource<object>();
        using var detail = CreateDetail((_, revision, file) => revision == "revision-a"
            ? pending.Task
            : Task.FromResult<object>(new RevisionBinaryFile { File = file.Path, Revision = revision }));
        var old = detail.ViewRevisionFileAsync(new() { Path = "old.bin", Type = ObjectType.Blob });
        // 通常のCommit setterからRefreshを通し、別経路のGit読込だけを隔離する。
        detail.Commit = null!;
        SetCommitWithoutLoadingMetadata(detail, "revision-b");
        await detail.ViewRevisionFileAsync(new() { Path = "new.bin", Type = ObjectType.Blob });
        pending.SetResult(new RevisionBinaryFile { File = "old.bin", Revision = "revision-a" });
        await old;
        var displayed = Assert.IsType<RevisionBinaryFile>(detail.ViewRevisionFileContent);
        Assert.Equal("new.bin", detail.ViewRevisionFilePath);
        Assert.Equal("revision-b", displayed.Revision);
    }

    [Fact]
    public async Task Load_UsesSelectionSnapshot()
    {
        var gate = new TaskCompletionSource();
        string? loadedRevision = null;
        GitObject? loadedFile = null;
        using var detail = CreateDetail(async (_, revision, file) =>
        {
            await gate.Task;
            loadedRevision = revision;
            loadedFile = file;
            return new RevisionBinaryFile { File = file.Path, Revision = revision };
        });
        var selected = new GitObject { Path = "a", SHA = "blob-a", Type = ObjectType.Blob };
        var loading = detail.ViewRevisionFileAsync(selected);
        selected.Path = "changed";
        selected.SHA = "changed";
        selected.Type = ObjectType.Commit;
        detail.Commit.SHA = "changed";
        gate.SetResult();
        await loading;

        Assert.Equal("revision-a", loadedRevision);
        Assert.NotSame(selected, loadedFile);
        Assert.Equal("a", loadedFile!.Path);
        Assert.Equal("blob-a", loadedFile.SHA);
        Assert.Equal(ObjectType.Blob, loadedFile.Type);
        var content = Assert.IsType<RevisionBinaryFile>(detail.ViewRevisionFileContent);
        Assert.Equal("revision-a", content.Revision);
    }

    [Fact]
    public async Task FailedLatestLoad_ClearsPreviousContentAndStaleFailureCannotReplaceIt()
    {
        var pending = new TaskCompletionSource<object>();
        using var detail = CreateDetail((_, _, file) => file.Path switch
        {
            "pending" => pending.Task,
            "failed" => Task.FromException<object>(new InvalidOperationException("expected")),
            _ => Task.FromResult<object>(new RevisionTextFile { FileName = file.Path, Content = "shown" }),
        });
        var old = detail.ViewRevisionFileAsync(new() { Path = "pending", Type = ObjectType.Blob });
        await detail.ViewRevisionFileAsync(new() { Path = "shown", Type = ObjectType.Blob });
        Assert.NotNull(detail.ViewRevisionFileContent);
        await Assert.ThrowsAsync<InvalidOperationException>(() => detail.ViewRevisionFileAsync(new() { Path = "failed", Type = ObjectType.Blob }));
        Assert.Null(detail.ViewRevisionFileContent);
        pending.SetException(new InvalidOperationException("stale"));
        await old;
        Assert.Equal("failed", detail.ViewRevisionFilePath);
        Assert.Null(detail.ViewRevisionFileContent);
    }

    [Fact]
    public async Task NullLatestResult_CannotRestorePreviousContent()
    {
        using var detail = CreateDetail((_, _, file) => Task.FromResult<object>(file.Path == "missing"
            ? null!
            : new RevisionTextFile { FileName = file.Path, Content = "shown" }));
        await detail.ViewRevisionFileAsync(new() { Path = "shown", Type = ObjectType.Blob });
        await detail.ViewRevisionFileAsync(new() { Path = "missing", Type = ObjectType.Blob });
        Assert.Equal("missing", detail.ViewRevisionFilePath);
        Assert.Null(detail.ViewRevisionFileContent);
    }

    private static CommitDetail CreateDetail(Func<string, string, GitObject, Task<object>> loader)
    {
        var repo = new Repository(false, Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "unused-git"));
        var detail = new CommitDetail(repo, new CommitDetailSharedData(), loader);
        // コミット情報の別経路のGit読込を起動せず、Filesタブの操作だけを検証する。
        SetCommitWithoutLoadingMetadata(detail, "revision-a");
        return detail;
    }

    private static void SetCommitWithoutLoadingMetadata(CommitDetail detail, string revision)
    {
        typeof(CommitDetail).GetField("_commit", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(detail, new Commit { SHA = revision });
    }

    private sealed class CountingBitmap(Stream stream) : Bitmap(stream)
    {
        public int DisposeCount { get; private set; }

        public override void Dispose()
        {
            ++DisposeCount;
            base.Dispose();
        }
    }
}
