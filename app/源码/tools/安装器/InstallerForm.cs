using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace QingTodo.Setup
{
    internal static class SetupProgram
    {
        [STAThread]
        private static int Main(string[] args)
        {
            // Verification/extraction modes never write outside the explicitly supplied test folder.
            if (args.Length == 2 && args[0] == "--verify-package")
            {
                try
                {
                    string folder = Path.GetFullPath(args[1]); Directory.CreateDirectory(folder);
                    using (var payload = PackageEngine.OpenPayload()) PackageEngine.Extract(payload, folder);
                    File.WriteAllText(Path.Combine(folder, "verification.txt"), "Embedded application extracted and verified."); return 0;
                }
                catch { return 1; }
            }
            bool created;
            using (var instance = new Mutex(true, "Local\\QingTodo-Installer", out created))
            {
                if (!created) { MessageBox.Show("安装程序已经打开，请返回安装窗口继续。", "轻待办"); return 0; }
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new InstallerForm()); return 0;
            }
        }
    }

    internal sealed class InstallerForm : Form
    {
        private readonly Label status, note;
        private readonly Button action, close;
        private readonly ProgressBar progress;
        private readonly string destination;
        private bool working, installed;
        public InstallerForm()
        {
            destination = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "QingTodo");
            Text = "轻待办 · 一键安装"; ClientSize = new Size(444, 290); MaximizeBox = false; MinimizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog; StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(247, 248, 245); ForeColor = Color.FromArgb(41, 54, 44);
            Font = new Font("Microsoft YaHei UI", 9f);
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            Controls.Add(new Label { Text = "轻待办", Font = new Font(Font.FontFamily, 22, FontStyle.Bold), AutoSize = true, Location = new Point(28, 24) });
            Controls.Add(new Label { Text = "三条待办小窗 · 轻提醒 · 自主排序", AutoSize = true, ForeColor = Color.FromArgb(123, 131, 121), Location = new Point(30, 75) });
            status = new Label { Text = "正在准备安装…", AutoSize = false, Size = new Size(382, 48), Location = new Point(30, 119) }; Controls.Add(status);
            progress = new ProgressBar { Location = new Point(30, 174), Size = new Size(382, 5), Style = ProgressBarStyle.Marquee }; Controls.Add(progress);
            note = new Label { Text = "程序文件已内置，无需联网下载。", AutoSize = false, Size = new Size(382, 32), ForeColor = Color.FromArgb(123, 131, 121), Location = new Point(30, 187) }; Controls.Add(note);
            action = new Button { Text = "安装并打开", Size = new Size(130, 35), Location = new Point(282, 231), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(82, 109, 80), ForeColor = Color.White };
            action.FlatAppearance.BorderSize = 0; action.Click += delegate { if (installed) Close(); else Install(); }; Controls.Add(action);
            close = new Button { Text = "关闭", Size = new Size(65, 35), Location = new Point(203, 231), FlatStyle = FlatStyle.Flat, BackColor = BackColor, ForeColor = ForeColor };
            close.FlatAppearance.BorderSize = 0; close.Click += delegate { Close(); }; Controls.Add(close);
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (working) e.Cancel = true; };
            Shown += delegate { BeginInvoke(new Action(Install)); };
        }

        private void Install()
        {
            if (working) return;
            string source;
            bool startupWasEnabled = File.Exists(SetupIntegration.StartupShortcut);
            try { source = SetupIntegration.FindOldData(AppDomain.CurrentDomain.BaseDirectory, destination); }
            catch (Exception error) { Failed(error); return; }
            working = true; action.Enabled = close.Enabled = false; progress.Visible = true;
            status.Text = "正在安装轻待办…";
            var worker = new BackgroundWorker { WorkerReportsProgress = true };
            worker.ProgressChanged += delegate(object sender, ProgressChangedEventArgs e) { status.Text = e.UserState as string; };
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                using (var payload = PackageEngine.OpenPayload())
                    e.Result = PackageEngine.Install(payload, destination, source, message => worker.ReportProgress(0, message));
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                working = false; progress.Visible = false; action.Enabled = close.Enabled = true;
                if (e.Error != null) { Failed(e.Error); worker.Dispose(); return; }
                var result = (InstallResult)e.Result;
                string shortcutWarning = null;
                try
                {
                    SetupIntegration.WriteShortcut(SetupIntegration.DesktopShortcut, destination, "");
                    if (startupWasEnabled) SetupIntegration.WriteShortcut(SetupIntegration.StartupShortcut, destination, "--startup");
                    SetupIntegration.SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
                }
                catch (Exception error) { shortcutWarning = error.Message; }
                installed = true; action.Text = "完成"; close.Visible = false;
                status.Text = result.Migrated ? "安装完成，旧版待办已保留并迁移。" : result.KeptExistingData ? "更新完成，已有待办与设置已保留。" : "安装完成，可以开始记录待办了。";
                note.Text = shortcutWarning == null ? "桌面已创建“轻待办”入口，正在为你打开。" : "程序已安装。桌面入口创建失败，可从安装目录打开。";
                try { Process.Start(new ProcessStartInfo(Path.Combine(destination, PackageEngine.AppFile)) { WorkingDirectory = destination, UseShellExecute = true }); }
                catch (Exception error) { note.Text = "程序已安装，请手动打开：" + destination; note.Height = 48; status.Text = "自动打开失败：" + error.Message; }
                worker.Dispose();
            };
            worker.RunWorkerAsync();
        }
        private void Failed(Exception error)
        {
            working = false; progress.Visible = false; action.Enabled = close.Enabled = true;
            status.Text = error.Message; action.Text = "重试安装";
            note.Text = "现有待办会保留。安装位置：%LOCALAPPDATA%\\Programs\\QingTodo";
        }
    }
}
