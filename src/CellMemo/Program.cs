using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using CellMemo.Native;
using CellMemo.Services;
using CellMemo.UI;

namespace CellMemo
{
    internal static class Program
    {
        private static Mutex? _mutex;
        private static CancellationTokenSource? _ipcServerCts;

        [STAThread]
        private static void Main(string[] args)
        {
            // 1. システムワイド名前付きMutexによる二重起動防止
            bool createdNew = false;
            try
            {
                _mutex = new Mutex(true, SingleInstanceIpc.MutexName, out createdNew);
            }
            catch (UnauthorizedAccessException)
            {
                try
                {
                    _mutex = new Mutex(true, @"Local\CellMemo_SingleInstance_Mutex_8F9A2B1C", out createdNew);
                }
                catch
                {
                    createdNew = true;
                }
            }
            catch
            {
                createdNew = true;
            }

            if (!createdNew)
            {
                bool sent = SingleInstanceIpc.SendToExistingInstance(args);
                if (sent)
                {
                    return;
                }
            }

            // 2. WinForms 初期化
            ApplicationConfiguration.Initialize();
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var settingsService = new SettingsService();

            // 3. グローバル未処理例外ハンドラ
            Application.ThreadException += (s, e) =>
            {
                settingsService.LogError("Application.ThreadException", e.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    settingsService.LogError("AppDomain.UnhandledException", ex);
                }
            };

            // 4. 設定の同期読み込み (デッドロック回避)
            settingsService.LoadSettingsSync();

            var mainForm = new MainForm(settingsService);

            // 5. 名前付きパイプサーバーの常時待機開始
            _ipcServerCts = new CancellationTokenSource();
            SingleInstanceIpc.StartServer(payload =>
            {
                mainForm.HandleIpcActivation(payload);
            }, _ipcServerCts.Token);

            // 6. セッション同期復元 (デッドロック回避)
            mainForm.RestoreSessionSync();

            // 7. コマンドライン引数にファイルパスが指定されている場合は表示後に非同期ロード
            if (args.Length > 0)
            {
                mainForm.Shown += (s, e) =>
                {
                    foreach (string filePath in args)
                    {
                        if (File.Exists(filePath))
                        {
                            _ = mainForm.OpenFileAsync(filePath);
                        }
                    }
                };
            }

            // 8. メインループ実行
            try
            {
                Application.Run(mainForm);
            }
            catch (Exception ex)
            {
                settingsService.LogError("Application.Run crashed", ex);
                MessageBox.Show(ex.ToString(), "CellMemo Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // 9. 終了時クリーンアップ
            _ipcServerCts?.Cancel();
            SingleInstanceIpc.StopServer();
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            Environment.Exit(0);
        }
    }
}
