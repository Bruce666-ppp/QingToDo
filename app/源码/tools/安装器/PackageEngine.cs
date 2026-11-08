using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace QingTodo.Setup
{
    internal sealed class InstallResult
    {
        public string Folder;
        public bool Migrated;
        public bool KeptExistingData;
    }

    internal static class PackageEngine
    {
        public const string AppFile = "轻待办.exe";
        public const string IconFile = "轻待办-便签.ico";

        public static Stream OpenPayload()
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("QingTodo.Payload.zip");
            if (stream == null) throw new IOException("安装包缺少程序文件，请重新获取完整安装包。");
            return stream;
        }

        public static bool IsRunning(string dataFolder)
        {
            string canonical = Path.GetFullPath(dataFolder).ToUpperInvariant();
            string hash;
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical))).Replace("-", "").Substring(0, 24);
            Mutex handle;
            if (!Mutex.TryOpenExisting("Local\\QingTodo-" + hash, out handle)) return false;
            handle.Dispose(); return true;
        }

        private static string SafePath(string folder, string relative)
        {
            string root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(":")) throw new IOException("安装包内含无效路径。");
            string resolved = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException("安装包内含越界路径。");
            return resolved;
        }

        public static List<string> Extract(Stream zipStream, string stage)
        {
            var names = new List<string>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, true))
            {
                long total = 0;
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string destination = SafePath(stage, entry.FullName);
                    if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(destination); continue; }
                    if (!seen.Add(entry.FullName.Replace('/', '\\'))) throw new IOException("安装包中存在重复文件。");
                    if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000) throw new IOException("安装包不能包含符号链接。");
                    total += entry.Length; if (total > 64 * 1024 * 1024) throw new IOException("安装包内容大小异常。");
                    string relative = entry.FullName.Replace('/', '\\');
                    if (relative.StartsWith("data\\", StringComparison.OrdinalIgnoreCase)) throw new IOException("安装包不应包含个人待办数据。");
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    using (var input = entry.Open()) using (var output = File.Create(destination)) input.CopyTo(output);
                    names.Add(relative);
                }
            }
            if (!names.Contains(AppFile, StringComparer.OrdinalIgnoreCase) || !names.Contains(IconFile, StringComparer.OrdinalIgnoreCase))
                throw new IOException("安装包不完整，缺少主程序或图标。");
            if (new FileInfo(Path.Combine(stage, AppFile)).Length == 0) throw new IOException("安装包主程序为空。");
            return names;
        }

        private static byte[] ReadData(string folder)
        {
            string primary = Path.Combine(folder, "todos.json");
            Exception lastError = null;
            foreach (string file in new[] { primary, primary + ".bak" })
            {
                if (!File.Exists(file)) continue;
                try
                {
                    byte[] bytes = File.ReadAllBytes(file);
                    using (var stream = new MemoryStream(bytes))
                    {
                        var document = (Document)new DataContractJsonSerializer(typeof(Document)).ReadObject(stream);
                        Repository.Validate(document);
                    }
                    return bytes;
                }
                catch (NotSupportedException) { throw; }
                catch (Exception error) { lastError = error; }
            }
            if (lastError != null) throw new IOException("旧版待办文件无法读取，文件已保留。请先修复或备份旧版数据，再重试安装。", lastError);
            return null;
        }

        public static InstallResult Install(Stream payload, string destination, string sourceData, Action<string> progress)
        {
            destination = Path.GetFullPath(destination);
            string targetData = Path.Combine(destination, "data");
            if (IsRunning(targetData) || (!string.IsNullOrEmpty(sourceData) && IsRunning(sourceData)))
                throw new InvalidOperationException("轻待办正在运行。请在托盘里右键选择“退出轻待办”，再点击“重试安装”。");
            // Resolve and validate migration before touching the installation.
            bool existing = File.Exists(Path.Combine(targetData, "todos.json")) || File.Exists(Path.Combine(targetData, "todos.json.bak"));
            byte[] migration = existing ? null : string.IsNullOrEmpty(sourceData) ? null : ReadData(sourceData);
            if (existing) ReadData(targetData);
            Directory.CreateDirectory(destination);
            string stage = Path.Combine(destination, ".setup-" + Guid.NewGuid().ToString("N"));
            string files = Path.Combine(stage, "files"), backups = Path.Combine(stage, "backup");
            var changed = new List<Tuple<string, string>>();
            bool rollbackFailed = false;
            try
            {
                Directory.CreateDirectory(files);
                if (progress != null) progress("正在准备程序文件…");
                List<string> names = Extract(payload, files);
                // Put the app first and keep rollback copies until all files are ready.
                names = names.OrderBy(name => name == AppFile ? 0 : name == IconFile ? 1 : 2).ThenBy(name => name).ToList();
                foreach (string name in names)
                {
                    string output = SafePath(destination, name), input = SafePath(files, name);
                    string backup = null;
                    if (File.Exists(output))
                    {
                        backup = SafePath(backups, name); Directory.CreateDirectory(Path.GetDirectoryName(backup));
                        File.Copy(output, backup, false);
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(output));
                    changed.Add(Tuple.Create(output, backup)); File.Copy(input, output, true);
                }
                if (migration != null)
                {
                    if (progress != null) progress("正在迁移已有待办…");
                    Directory.CreateDirectory(targetData);
                    string migratedFile = Path.Combine(targetData, "todos.json");
                    // CreateNew makes a concurrent or previously created data file safe from replacement.
                    string stagedData = Path.Combine(stage, "migrated-data.json");
                    using (var output = new FileStream(stagedData, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    { output.Write(migration, 0, migration.Length); output.Flush(true); }
                    File.Move(stagedData, migratedFile);
                    try { File.Copy(migratedFile, migratedFile + ".bak", false); } catch (IOException) { /* The valid primary and original source remain available. */ }
                }
                return new InstallResult { Folder = destination, Migrated = migration != null, KeptExistingData = existing };
            }
            catch
            {
                for (int i = changed.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        if (changed[i].Item2 != null) File.Copy(changed[i].Item2, changed[i].Item1, true);
                        else if (File.Exists(changed[i].Item1)) File.Delete(changed[i].Item1);
                    }
                    catch { rollbackFailed = true; }
                }
                throw;
            }
            finally
            {
                // Delete only our uniquely named staging folder under the resolved install directory.
                string installRoot = destination.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string absoluteStage = Path.GetFullPath(stage);
                if (!rollbackFailed && absoluteStage.StartsWith(installRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(absoluteStage))
                { try { Directory.Delete(absoluteStage, true); } catch { } }
            }
        }
    }
}
