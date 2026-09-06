using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using ADMorpher.Services;

namespace ADMorpher
{
    public partial class App : Application
    {
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);
        private const int ATTACH_PARENT_PROCESS = -1;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // コマンドライン引数チェック
            for (int i = 0; i < e.Args.Length; i++)
            {
                if (e.Args[i].Equals("--test-regression", StringComparison.OrdinalIgnoreCase))
                {
                    AttachConsole(ATTACH_PARENT_PROCESS);
                    int exitCode = RegressionTestService.RunAllTests();
                    Environment.Exit(exitCode);
                    return;
                }
            }

            // 通常のGUI起動
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}
