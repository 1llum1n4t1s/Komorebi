using System;
using System.Collections.Generic;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     コマンドログのViewModel。Models.ICommandLogインターフェースを実装する。
    ///     gitコマンドの実行ログを蓄積し、UIに表示するためのスレッドセーフなログ管理を行う。
    /// </summary>
    public class CommandLog : ObservableObject, Models.ICommandLog
    {
        /// <summary>
        ///     ログの名前（操作名）。
        /// </summary>
        public string Name
        {
            get;
            private set;
        }

        /// <summary>
        ///     ログの開始時刻。
        /// </summary>
        public DateTime StartTime
        {
            get;
        } = DateTime.Now;

        /// <summary>
        ///     ログの終了時刻。Complete()呼び出し時に更新される。
        /// </summary>
        public DateTime EndTime
        {
            get;
            private set;
        } = DateTime.Now;

        /// <summary>
        ///     ログが完了したかどうかのフラグ。
        /// </summary>
        public bool IsComplete
        {
            get;
            private set;
        } = false;

        /// <summary>
        ///     ログの内容文字列。完了前はStringBuilderから動的に生成し、完了後はキャッシュされた文字列を返す。
        /// </summary>
        public string Content
        {
            get
            {
                return IsComplete ? _content : _builder.ToString();
            }
        }

        /// <summary>
        ///     コンストラクタ。ログ名を受け取って初期化する。
        /// </summary>
        /// <param name="name">ログの名前（操作名）</param>
        public CommandLog(string name)
        {
            Name = name;
        }

        /// <summary>
        ///     ログ受信者を登録する。リアルタイムでログ行を受け取るレシーバーを追加する。
        /// </summary>
        /// <param name="receiver">ログ受信者</param>
        public void Subscribe(Models.ICommandLogReceiver receiver)
        {
            _receivers.Add(receiver);
        }

        /// <summary>
        ///     ログ受信者の登録を解除する。
        /// </summary>
        /// <param name="receiver">ログ受信者</param>
        public void Unsubscribe(Models.ICommandLogReceiver receiver)
        {
            _receivers.Remove(receiver);
        }

        /// <summary>
        ///     ログに1行追加する。UIスレッド以外から呼ばれた場合はUIスレッドにディスパッチする。
        ///     登録されている全レシーバーに新しい行を通知する。
        /// </summary>
        /// <param name="line">追加するログ行（nullの場合は空行）</param>
        public void AppendLine(string line = null)
        {
            // UIスレッド以外からの呼び出しはUIスレッドにディスパッチする
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Invoke(() => AppendLine(line));
            }
            else
            {
                // StringBuilderに行を追加する
                var newline = line ?? string.Empty;
                _builder.AppendLine(newline);

                // 全レシーバーに新しいログ行を通知する
                foreach (var receiver in _receivers)
                    receiver.OnReceiveCommandLog(newline);
            }
        }

        /// <summary>
        ///     ログを完了状態にする。StringBuilderの内容をキャッシュし、リソースを解放する。
        ///     UIスレッド以外から呼ばれた場合はUIスレッドにディスパッチする。
        /// </summary>
        public void Complete()
        {
            // UIスレッド以外からの呼び出しはUIスレッドにディスパッチする
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Invoke(Complete);
                return;
            }

            // 完了フラグと終了時刻を設定する
            IsComplete = true;
            EndTime = DateTime.Now;

            // StringBuilderの内容をキャッシュし、リソースを解放する
            _content = _builder.ToString();
            _builder.Clear();
            _receivers.Clear();
            _builder = null;

            // UIにIsComplete変更を通知する
            OnPropertyChanged(nameof(IsComplete));
        }

        /// <summary>完了後のキャッシュされたログ内容</summary>
        private string _content = string.Empty;
        /// <summary>ログ蓄積用のStringBuilder</summary>
        private StringBuilder _builder = new StringBuilder();
        /// <summary>リアルタイムログ受信者のリスト</summary>
        private List<Models.ICommandLogReceiver> _receivers = new List<Models.ICommandLogReceiver>();
    }
}
