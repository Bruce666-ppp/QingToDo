using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace QingTodo
{
    // Use the Unicode Shell interface so Chinese paths work on every Windows locale.
    internal static class WindowsShortcut
    {
        private static readonly Guid ShellLinkClass = new Guid("00021401-0000-0000-C000-000000000046");

        public static void Write(string path, string target, string folder, string arguments, string icon)
        {
            object instance = Activator.CreateInstance(Type.GetTypeFromCLSID(ShellLinkClass, true));
            try
            {
                var link = (IShellLinkW)instance;
                link.SetPath(Path.GetFullPath(target));
                link.SetWorkingDirectory(Path.GetFullPath(folder));
                link.SetArguments(arguments ?? "");
                link.SetIconLocation(Path.GetFullPath(icon), 0);
                link.SetDescription("轻待办 · 简单记录，轻声提醒");
                string fullPath = Path.GetFullPath(path);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                ((IPersistFile)instance).Save(fullPath, true);
            }
            finally { Marshal.FinalReleaseComObject(instance); }
        }

        public static string Target(string path)
        {
            if (!File.Exists(path)) return null;
            object instance = Activator.CreateInstance(Type.GetTypeFromCLSID(ShellLinkClass, true));
            try
            {
                ((IPersistFile)instance).Load(Path.GetFullPath(path), 0);
                var target = new StringBuilder(32768);
                ((IShellLinkW)instance).GetPath(target, target.Capacity, IntPtr.Zero, 4);
                return target.ToString();
            }
            finally { Marshal.FinalReleaseComObject(instance); }
        }

        [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int count, IntPtr findData, uint flags);
            void GetIDList(out IntPtr list);
            void SetIDList(IntPtr list);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int count);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string text);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int count);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string path);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int count);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
            void GetHotkey(out short key);
            void SetHotkey(short key);
            void GetShowCmd(out int command);
            void SetShowCmd(int command);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int count, out int index);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
            void Resolve(IntPtr window, uint flags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
        }
    }
}
