using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace QingTodo.Setup
{
    internal static class SetupIntegration
    {
        public static string DesktopShortcut { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "轻待办.lnk"); } }
        public static string StartupShortcut { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "轻待办.lnk"); } }

        public static string ShortcutTarget(string path)
        {
            return WindowsShortcut.Target(path);
        }

        public static void WriteShortcut(string path, string folder, string arguments)
        {
            WindowsShortcut.Write(path, Path.Combine(folder, PackageEngine.AppFile), folder, arguments, Path.Combine(folder, PackageEngine.IconFile));
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
