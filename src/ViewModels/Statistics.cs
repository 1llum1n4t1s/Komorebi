// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
using System.Collections.Generic;
using System.Threading.Tasks;

using Avalonia.Media;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
/// リポジトリの統計情報（コントリビューション履歴）を表示するViewModel。
/// 全期間、月間、週間のレポートを切り替えて表示できる。
/// </summary>
public class Statistics : ObservableObject
{
    public List<Models.Branch> Branches
    {
        get => _branches;
        private set => SetProperty(ref _branches, value);
    }

    public Models.Branch SelectedBranch
    {
        get => _selectedBranch;
        set
        {
            if (value != null && SetProperty(ref _selectedBranch, value))
                LoadStatistics();
        }
    }

    /// <summary>
    /// 統計データの読み込み中かどうか。
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>
    /// 選択されたレポート期間のインデックス（0=全期間, 1=月間, 2=週間）。
    /// </summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (SetProperty(ref _selectedIndex, value))
                RefreshReport();
        }
    }

    /// <summary>
    /// 現在選択されているレポートデータ。設定時に著者選択をリセットする。
    /// </summary>
    public Models.StatisticsReport SelectedReport
    {
        get => _selectedReport;
        private set
        {
            // 新しいレポート表示時に著者のハイライト選択をリセット
            value?.ChangeAuthor(null);
            SetProperty(ref _selectedReport, value);
        }
    }

    /// <summary>
    /// 統計グラフに使用するサンプルカラー（ARGB値）。変更時にブラシとレポートの色を更新する。
    /// </summary>
    public uint SampleColor
    {
        get => Preferences.Instance.StatisticsSampleColor;
        set
        {
            if (value != Preferences.Instance.StatisticsSampleColor)
            {
                Preferences.Instance.StatisticsSampleColor = value;
                // ブラシプロパティの変更を通知してUI更新
                OnPropertyChanged(nameof(SampleBrush));
                _selectedReport?.ChangeColor(value);
            }
        }
    }

    /// <summary>
    /// SampleColorから生成されるブラシ。UIのカラーインジケーターに使用する。
    /// </summary>
    public IBrush SampleBrush
    {
        get => new SolidColorBrush(SampleColor);
    }

    /// <summary>
    /// コンストラクタ。バックグラウンドで統計データを非同期読み込みする。
    /// </summary>
    public Statistics(string repo)
    {
        _repo = repo;
        var allBranches = _selectedBranch;
        Task.Run(async () =>
        {
            var branches = await new Commands.QueryBranches(repo).GetResultAsync().ConfigureAwait(false);
            branches.Insert(0, allBranches);
            Dispatcher.UIThread.Post(() => Branches = branches);
        });
        LoadStatistics();
    }

    private void LoadStatistics()
    {
        IsLoading = true;
        var generation = ++_generation;
        var branch = _selectedBranch;
        var max = Preferences.Instance.MaxHistoryCommits;
        Task.Run(async () =>
        {
            // バックグラウンドスレッドで統計データを取得
            var result = await new Commands.Statistics(_repo, max, branch).ReadAsync().ConfigureAwait(false);
            // UIスレッドに戻してデータを反映
            Dispatcher.UIThread.Post(() =>
            {
                // 上流との差分: 選択し直す前の集計結果で表示を巻き戻さない。
                if (generation != _generation)
                    return;

                _data = result;
                RefreshReport();
                IsLoading = false;
            });
        });
    }

    /// <summary>
    /// 選択された期間に応じてレポートデータを切り替える。
    /// </summary>
    private void RefreshReport()
    {
        if (_data is null)
            return;

        // インデックスに応じて全期間/月間/週間のレポートを選択
        var report = _selectedIndex switch
        {
            0 => _data.All,
            1 => _data.Month,
            _ => _data.Week,
        };

        report.ChangeColor(SampleColor);
        SelectedReport = report;
    }

    private readonly string _repo;
    private List<Models.Branch> _branches = [];
    private Models.Branch _selectedBranch = new() { Name = "--- (All)", IsLocal = true, FullName = string.Empty, Head = "---" };
    private int _generation;
    private bool _isLoading = true;
    private Models.Statistics _data = null;
    private Models.StatisticsReport _selectedReport = null;
    private int _selectedIndex = 0;
}
