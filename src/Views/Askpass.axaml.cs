using System;
using Avalonia.Interactivity;

namespace Komorebi.Views
{
    /// <summary>
    ///     Git認証情報（パスワード等）入力ダイアログのコードビハインド。
    /// </summary>
    public partial class Askpass : ChromelessWindow
    {
        /// <summary>
        ///     コンストラクタ。コンポーネントを初期化する。
        /// </summary>
        public Askpass()
        {
            InitializeComponent();
        }

        /// <summary>
        ///     CloseWindowの処理を行う。
        /// </summary>
        private void CloseWindow(object _1, RoutedEventArgs _2)
        {
            Console.Out.WriteLine("No passphrase entered.");
            App.Quit(-1);
        }

        /// <summary>
        ///     EnterPasswordの処理を行う。
        /// </summary>
        private void EnterPassword(object _1, RoutedEventArgs _2)
        {
            var passphrase = TxtPassphrase.Text ?? string.Empty;
            Console.Out.WriteLine(passphrase);
            App.Quit(0);
        }
    }
}
