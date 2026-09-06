using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>バイナリを必要な範囲だけ読み込む表示モデル。</summary>
public class BinaryFileViewer : ObservableObject
{
    public string File { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public Models.BinaryFile? Content
    {
        get => _content;
        private set => SetProperty(ref _content, value);
    }

    public BinaryFileViewer(string repo, string file, string? revision)
    {
        _repo = repo;
        File = file;
        _revision = revision;
    }

    public async Task LoadAsync()
    {
        if (_closed || _started)
            return;

        _started = true;
        string? temporary = null;
        try
        {
            var path = Path.Combine(_repo, File);
            if (_revision is not null)
            {
                temporary = Path.GetTempFileName();
                if (!await Commands.SaveRevisionFile.RunAsync(_repo, _revision, File, temporary))
                    return;
                path = temporary;
            }

            // 上流との差分: 読み込み中に閉じても一時ファイルとストリームを残さない。
            if (_closed)
                return;
            Content = new Models.BinaryFile(path, temporary is not null);
            temporary = null;
        }
        catch (Exception ex)
        {
            if (!_closed)
                App.RaiseException(_repo, ex.Message);
        }
        finally
        {
            if (temporary is not null)
                System.IO.File.Delete(temporary);
            IsLoading = false;
        }
    }

    public void Cleanup()
    {
        _closed = true;
        Content?.Dispose();
        Content = null;
    }

    private readonly string _repo;
    private readonly string? _revision;
    private bool _isLoading = true;
    private bool _started;
    private bool _closed;
    private Models.BinaryFile? _content;
}
