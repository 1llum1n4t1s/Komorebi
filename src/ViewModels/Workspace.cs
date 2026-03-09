using System.Collections.Generic;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     ワークスペースの設定と状態を管理するViewModel。
    ///     複数のリポジトリをグループ化し、起動時の復元や配色を設定する。
    /// </summary>
    public class Workspace : ObservableObject
    {
        /// <summary>
        ///     ワークスペースの表示名。
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>
        ///     ワークスペースのテーマカラー（ARGB値）。
        ///     変更時にBrushプロパティの更新通知も発行する。
        /// </summary>
        public uint Color
        {
            get => _color;
            set
            {
                if (SetProperty(ref _color, value))
                    // カラー変更時にブラシも再生成されるよう通知
                    OnPropertyChanged(nameof(Brush));
            }
        }

        /// <summary>
        ///     このワークスペースに属するリポジトリパスのリスト。
        /// </summary>
        public List<string> Repositories
        {
            get;
            set;
        } = new List<string>();

        /// <summary>
        ///     アクティブなリポジトリタブのインデックス。
        /// </summary>
        public int ActiveIdx
        {
            get;
            set;
        } = 0;

        /// <summary>
        ///     このワークスペースが現在アクティブかどうか。
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        /// <summary>
        ///     アプリ起動時にこのワークスペースを自動復元するかどうか。
        /// </summary>
        public bool RestoreOnStartup
        {
            get => _restoreOnStartup;
            set => SetProperty(ref _restoreOnStartup, value);
        }

        /// <summary>
        ///     このワークスペースのデフォルトクローン先ディレクトリ。
        /// </summary>
        public string DefaultCloneDir
        {
            get => _defaultCloneDir;
            set => SetProperty(ref _defaultCloneDir, value);
        }

        /// <summary>
        ///     カラー値から生成されるブラシ（UI表示用）。
        /// </summary>
        public IBrush Brush
        {
            get => new SolidColorBrush(_color);
        }

        private string _name = string.Empty;
        private uint _color = 4278221015;
        private bool _isActive = false;
        private bool _restoreOnStartup = true;
        private string _defaultCloneDir = string.Empty;
    }
}
