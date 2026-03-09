using System.Threading.Tasks;

using Avalonia.Media;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     リポジトリの統計情報（コントリビューション履歴）を表示するViewModel。
    ///     全期間、月間、週間のレポートを切り替えて表示できる。
    /// </summary>
    public class Statistics : ObservableObject
    {
        /// <summary>
        ///     統計データの読み込み中かどうか。
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        ///     選択されたレポート期間のインデックス（0=全期間, 1=月間, 2=週間）。
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
        ///     現在選択されているレポートデータ。設定時に著者選択をリセットする。
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
        ///     統計グラフに使用するサンプルカラー（ARGB値）。変更時にブラシとレポートの色を更新する。
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
        ///     SampleColorから生成されるブラシ。UIのカラーインジケーターに使用する。
        /// </summary>
        public IBrush SampleBrush
        {
            get => new SolidColorBrush(SampleColor);
        }

        /// <summary>
        ///     コンストラクタ。バックグラウンドで統計データを非同期読み込みする。
        /// </summary>
        public Statistics(string repo)
        {
            Task.Run(async () =>
            {
                // バックグラウンドスレッドで統計データを取得
                var result = await new Commands.Statistics(repo, Preferences.Instance.MaxHistoryCommits).ReadAsync().ConfigureAwait(false);
                // UIスレッドに戻してデータを反映
                Dispatcher.UIThread.Post(() =>
                {
                    _data = result;
                    RefreshReport();
                    IsLoading = false;
                });
            });
        }

        /// <summary>
        ///     選択された期間に応じてレポートデータを切り替える。
        /// </summary>
        private void RefreshReport()
        {
            if (_data == null)
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

        private bool _isLoading = true;
        private Models.Statistics _data = null;
        private Models.StatisticsReport _selectedReport = null;
        private int _selectedIndex = 0;
    }
}
