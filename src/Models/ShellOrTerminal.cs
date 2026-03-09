using System;
using System.Collections.Generic;

using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Komorebi.Models
{
    /// <summary>
    ///     シェルまたはターミナルアプリケーションの定義クラス。
    ///     プラットフォームごとにサポートされるターミナルのリストを提供する。
    /// </summary>
    public class ShellOrTerminal
    {
        /// <summary>ターミナルの種別識別子（アイコンリソースの解決に使用）</summary>
        public string Type { get; set; }
        /// <summary>ターミナルの表示名</summary>
        public string Name { get; set; }
        /// <summary>実行ファイルのパスまたは名前</summary>
        public string Exec { get; set; }
        /// <summary>起動時に渡すコマンドライン引数</summary>
        public string Args { get; set; }

        /// <summary>ターミナルのアイコン画像（リソースから動的に読み込み）</summary>
        public Bitmap Icon
        {
            get
            {
                var icon = AssetLoader.Open(new Uri($"avares://Komorebi/Resources/Images/ShellIcons/{Type}.png", UriKind.RelativeOrAbsolute));
                return new Bitmap(icon);
            }
        }

        /// <summary>現在のプラットフォームでサポートされるターミナルのリスト</summary>
        public static readonly List<ShellOrTerminal> Supported;

        /// <summary>
        ///     静的コンストラクタ。プラットフォームに応じたターミナルリストを初期化する。
        /// </summary>
        static ShellOrTerminal()
        {
            // Windows向けターミナル
            if (OperatingSystem.IsWindows())
            {
                Supported = new List<ShellOrTerminal>()
                {
                    new ShellOrTerminal("git-bash", "Git Bash", "bash.exe"),
                    new ShellOrTerminal("pwsh", "PowerShell", "pwsh.exe|powershell.exe"),
                    new ShellOrTerminal("cmd", "Command Prompt", "cmd.exe"),
                    new ShellOrTerminal("wt", "Windows Terminal", "wt.exe", "-d .")
                };
            }
            // macOS向けターミナル
            else if (OperatingSystem.IsMacOS())
            {
                Supported = new List<ShellOrTerminal>()
                {
                    new ShellOrTerminal("mac-terminal", "Terminal", "Terminal"),
                    new ShellOrTerminal("iterm2", "iTerm", "iTerm"),
                    new ShellOrTerminal("warp", "Warp", "Warp"),
                    new ShellOrTerminal("ghostty", "Ghostty", "Ghostty"),
                    new ShellOrTerminal("kitty", "kitty", "kitty")
                };
            }
            // Linux向けターミナル
            else
            {
                Supported = new List<ShellOrTerminal>()
                {
                    new ShellOrTerminal("gnome-terminal", "Gnome Terminal", "gnome-terminal"),
                    new ShellOrTerminal("konsole", "Konsole", "konsole"),
                    new ShellOrTerminal("xfce4-terminal", "Xfce4 Terminal", "xfce4-terminal"),
                    new ShellOrTerminal("lxterminal", "LXTerminal", "lxterminal"),
                    new ShellOrTerminal("deepin-terminal", "Deepin Terminal", "deepin-terminal"),
                    new ShellOrTerminal("mate-terminal", "MATE Terminal", "mate-terminal"),
                    new ShellOrTerminal("foot", "Foot", "foot"),
                    new ShellOrTerminal("wezterm", "WezTerm", "wezterm", "start --cwd ."),
                    new ShellOrTerminal("ptyxis", "Ptyxis", "ptyxis", "--new-window --working-directory=."),
                    new ShellOrTerminal("ghostty", "Ghostty", "ghostty"),
                    new ShellOrTerminal("kitty", "kitty", "kitty"),
                    new ShellOrTerminal("custom", "Custom", ""),
                };
            }
        }

        /// <summary>
        ///     コンストラクタ
        /// </summary>
        /// <param name="type">種別識別子</param>
        /// <param name="name">表示名</param>
        /// <param name="exec">実行ファイル名</param>
        /// <param name="args">コマンドライン引数（省略可）</param>
        public ShellOrTerminal(string type, string name, string exec, string args = null)
        {
            Type = type;
            Name = name;
            Exec = exec;
            Args = args;
        }
    }
}
