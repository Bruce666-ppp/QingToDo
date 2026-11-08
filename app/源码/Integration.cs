using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace QingTodo
{
    public static class Integration
    {
        public static string ExePath { get { return Process.GetCurrentProcess().MainModule.FileName; } }
        public static string IconPath
        {
            get
            {
                string icon = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "轻待办-便签.ico");
                return File.Exists(icon) ? icon : ExePath;
            }
        }
        public static string StartupPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "轻待办.lnk"); } }
        public static bool StartupEnabled { get { return File.Exists(StartupPath); } }
        public static void SetStartup(bool enabled)
        {
            if (enabled) CreateShortcut(StartupPath, "--startup");
            else if (File.Exists(StartupPath)) File.Delete(StartupPath);
        }
        public static string CreateDesktopShortcut()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "轻待办.lnk");
            CreateShortcut(path, "");
            SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
            return path;
        }
        // Windows Script Host is built into Windows. Release both COM references after saving.
        private static void CreateShortcut(string path, string arguments)
        {
            object shell = null, shortcut = null;
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell", true);
                shell = Activator.CreateInstance(shellType);
                shortcut = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod,
                    null, shell, new object[] { path });
                Type type = shortcut.GetType();
                foreach (var pair in new[] {
                    new[] { "TargetPath", ExePath }, new[] { "Arguments", arguments },
                    new[] { "WorkingDirectory", Path.GetDirectoryName(ExePath) },
                    new[] { "IconLocation", IconPath + ",0" }, new[] { "Description", "轻待办 · 简单记录，轻声提醒" } })
                    type.InvokeMember(pair[0], System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { pair[1] });
                type.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally
            {
                if (shortcut != null) Marshal.FinalReleaseComObject(shortcut);
                if (shell != null) Marshal.FinalReleaseComObject(shell);
            }
        }
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("shell32.dll")] private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] public static extern int GetWindowLong(IntPtr window, int index);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        public static void RoundCorners(IntPtr window)
        {
            try { int preference = 2; DwmSetWindowAttribute(window, 33, ref preference, sizeof(int)); } catch { }
        }
    }
}
