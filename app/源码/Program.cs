using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;

namespace QingTodo
{
    internal static class Program
    {
        private static Mutex instance;
        private static EventWaitHandle wake;
        private static RegisteredWaitHandle registration;
        public static string TracePath;
        public static void Trace(string entry)
        {
            if (TracePath == null) return;
            try { File.AppendAllText(TracePath, DateTime.UtcNow.ToString("O") + " " + entry + Environment.NewLine); } catch { }
        }
        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--data-dir" && i + 1 < args.Length) folder = args[++i];
                    else if (args[i] == "--trace" && i + 1 < args.Length) TracePath = args[++i];
                }
                folder = Path.GetFullPath(folder);
                string hash;
                using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(folder.ToUpperInvariant()))).Replace("-", "").Substring(0, 24);
                bool created;
                instance = new Mutex(true, "Local\\QingTodo-" + hash, out created);
                wake = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\QingTodo-Wake-" + hash);
                if (!created) { wake.Set(); Trace("existing_instance_requested"); return; }
                var repository = new Repository(folder);
                Document document = repository.Load();
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                Ui.Install(app);
                var main = new MainWindow(repository, document);
                app.MainWindow = main;
                registration = ThreadPool.RegisterWaitForSingleObject(wake, delegate(object state, bool timedOut)
                { app.Dispatcher.BeginInvoke(new Action(delegate { main.RestoreManually(); })); }, null, Timeout.Infinite, false);
                app.DispatcherUnhandledException += delegate(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
                {
                    Trace("unhandled_exception:" + e.Exception.GetType().Name);
                    MessageBox.Show("出现了意外错误。已保存的数据仍在：\n" + repository.Folder + "\n\n" + e.Exception.Message, "轻待办", MessageBoxButton.OK, MessageBoxImage.Warning);
                    e.Handled = true;
                };
                Trace("app_started"); app.Run(main);
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message, "轻待办暂时无法打开", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                if (registration != null) registration.Unregister(null);
                if (wake != null) wake.Dispose();
                if (instance != null) instance.Dispose();
            }
        }
    }
}
