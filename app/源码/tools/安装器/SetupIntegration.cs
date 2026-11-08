using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace QingTodo.Setup
{
    internal static class SetupIntegration
    {
        public static string DesktopShortcut { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "轻待办.lnk"); } }
        public static string StartupShortcut { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "轻待办.lnk"); } }

        public static string ShortcutTarget(string path)
        {
            if (!File.Exists(path)) return null;
            object shell = null, shortcut = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true));
                shortcut = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
                return shortcut.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null) as string;
            }
            finally { if (shortcut != null) Marshal.FinalReleaseComObject(shortcut); if (shell != null) Marshal.FinalReleaseComObject(shell); }
        }

        public static void WriteShortcut(string path, string folder, string arguments)
        {
            object shell = null, shortcut = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true));
                shortcut = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
                foreach (var pair in new[] {
                    new[] { "TargetPath", Path.Combine(folder, PackageEngine.AppFile) },
                    new[] { "WorkingDirectory", folder }, new[] { "Arguments", arguments },
                    new[] { "IconLocation", Path.Combine(folder, PackageEngine.IconFile) + ",0" },
                    new[] { "Description", "轻待办 · 简单记录，轻声提醒" } })
                    shortcut.GetType().InvokeMember(pair[0], BindingFlags.SetProperty, null, shortcut, new object[] { pair[1] });
                shortcut.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally { if (shortcut != null) Marshal.FinalReleaseComObject(shortcut); if (shell != null) Marshal.FinalReleaseComObject(shell); }
        }

        public static string FindOldData(string installerFolder, string targetFolder)
        {
            var candidates = new List<string>();
            string oldTarget = ShortcutTarget(DesktopShortcut);
            if (!string.IsNullOrEmpty(oldTarget) && string.Equals(Path.GetFileName(oldTarget), PackageEngine.AppFile, StringComparison.OrdinalIgnoreCase))
                candidates.Add(Path.Combine(Path.GetDirectoryName(oldTarget), "data"));
            candidates.Add(Path.Combine(installerFolder, "data"));
            candidates.Add(Path.Combine(installerFolder, "轻待办", "data"));
            string targetData = Path.GetFullPath(Path.Combine(targetFolder, "data"));
            foreach (string candidate in candidates)
                if (!string.Equals(Path.GetFullPath(candidate), targetData, StringComparison.OrdinalIgnoreCase) &&
                    (File.Exists(Path.Combine(candidate, "todos.json")) || File.Exists(Path.Combine(candidate, "todos.json.bak")))) return candidate;
            return null;
        }
        [DllImport("shell32.dll")] public static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
    }
}
