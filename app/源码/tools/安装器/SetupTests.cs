using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace QingTodo.Setup
{
    internal static class Tests
    {
        private static int passed;
        private static string root;
        private static string Folder(string name) { return Path.Combine(root, name); }
        private static void Check(bool value, string message) { if (!value) throw new Exception("FAIL: " + message); passed++; Console.WriteLine("PASS: " + message); }
        private static InstallResult Install(string destination, string source)
        { using (var payload = PackageEngine.OpenPayload()) return PackageEngine.Install(payload, destination, source, null); }
        private static Document Data(string folder, string text)
        {
            var document = new Document(); document.Items.Add(new Todo { Text = text, SortOrder = 0, DueUtc = DateTime.UtcNow.AddHours(2).Ticks, LeadMinutes = 15, Reminded = true });
            document.Settings.CompactMode = true; document.Settings.AlwaysOnTop = true;
            new Repository(folder).Save(document); return document;
        }
        private static MemoryStream Zip(params string[] names)
        {
            var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
                foreach (string name in names)
                    using (var writer = new StreamWriter(archive.CreateEntry(name).Open())) writer.Write("test contents");
            stream.Position = 0; return stream;
        }
        [STAThread]
        private static void Main(string[] args)
        {
            root = Path.Combine(Path.GetFullPath(args[0]), "setup-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            string fresh = Folder("fresh"); InstallResult result = Install(fresh, null);
            Check(File.Exists(Path.Combine(fresh, PackageEngine.AppFile)) && File.Exists(Path.Combine(fresh, PackageEngine.IconFile)), "Fresh installation includes app and icon");
            Check(File.Exists(Path.Combine(fresh, "源码", "SortFeedback.cs")) && File.Exists(Path.Combine(fresh, "使用说明.md")), "Source and instructions install with the app");
            Check(!result.Migrated && !result.KeptExistingData && !File.Exists(Path.Combine(fresh, "data", "todos.json")), "Fresh install contains no personal or sample tasks");
            string source = Folder("old-portable-data"); Data(source, "保留原有待办及提醒");
            byte[] sourceBytes = File.ReadAllBytes(Path.Combine(source, "todos.json"));
            string migrated = Folder("migrated"); result = Install(migrated, source);
            Check(result.Migrated && File.ReadAllBytes(Path.Combine(migrated, "data", "todos.json")).SequenceEqual(sourceBytes), "Portable data migrates byte for byte");
            Document migratedDoc = new Repository(Path.Combine(migrated, "data")).Load();
            Check(migratedDoc.Settings.CompactMode && migratedDoc.Settings.AlwaysOnTop && migratedDoc.Items[0].Reminded && migratedDoc.Items[0].LeadMinutes == 15, "Migration keeps ordering, reminders and preferences");
            Check(File.ReadAllBytes(Path.Combine(source, "todos.json")).SequenceEqual(sourceBytes), "Migration leaves the old source unchanged");
            Data(Path.Combine(migrated, "data"), "已安装版本的新待办优先");
            byte[] existingBytes = File.ReadAllBytes(Path.Combine(migrated, "data", "todos.json"));
            result = Install(migrated, source);
            Check(result.KeptExistingData && !result.Migrated && File.ReadAllBytes(Path.Combine(migrated, "data", "todos.json")).SequenceEqual(existingBytes), "Reinstall preserves newer installed data over portable data");
            string corrupt = Folder("corrupt-source"); Directory.CreateDirectory(corrupt); File.WriteAllText(Path.Combine(corrupt, "todos.json"), "broken");
            bool rejected = false; try { Install(Folder("corrupt-install"), corrupt); } catch (IOException) { rejected = true; }
            Check(rejected && !Directory.Exists(Folder("corrupt-install")), "Unreadable old data blocks install without changing files");
            File.Copy(Path.Combine(source, "todos.json"), Path.Combine(corrupt, "todos.json.bak"));
            result = Install(Folder("backup-recovery"), corrupt);
            Check(result.Migrated && File.ReadAllText(Path.Combine(corrupt, "todos.json")) == "broken", "Migration can read a valid backup without modifying the old corrupt file");
            string busyData = Folder("running-data"), busyTarget = Folder("blocked-by-running");
            string hash; using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(busyData).ToUpperInvariant()))).Replace("-", "").Substring(0, 24);
            using (var mutex = new Mutex(true, "Local\\QingTodo-" + hash))
            {
                rejected = false; try { Install(busyTarget, busyData); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected && !Directory.Exists(busyTarget), "A running app prevents writes and prompts for a safe retry"); mutex.ReleaseMutex();
            }
            string rollback = Folder("rollback"); Directory.CreateDirectory(rollback);
            File.WriteAllText(Path.Combine(rollback, PackageEngine.AppFile), "old app must survive"); Directory.CreateDirectory(Path.Combine(rollback, PackageEngine.IconFile));
            rejected = false; try { Install(rollback, null); } catch (IOException) { rejected = true; } catch (UnauthorizedAccessException) { rejected = true; }
            Check(rejected && File.ReadAllText(Path.Combine(rollback, PackageEngine.AppFile)) == "old app must survive", "Failure during update rolls back overwritten app files");
            using (var malicious = Zip("../outside.txt", PackageEngine.AppFile, PackageEngine.IconFile))
            {
                rejected = false; try { PackageEngine.Extract(malicious, Folder("zip-slip")); } catch (IOException) { rejected = true; }
                Check(rejected && !File.Exists(Path.Combine(root, "outside.txt")), "Archive paths cannot escape the install folder");
            }
            using (var privateData = Zip(PackageEngine.AppFile, PackageEngine.IconFile, "data/todos.json"))
            {
                rejected = false; try { PackageEngine.Extract(privateData, Folder("private-data")); } catch (IOException) { rejected = true; }
                Check(rejected, "Distribution payload rejects personal task data");
            }
            using (var incomplete = Zip(PackageEngine.AppFile))
            {
                rejected = false; try { PackageEngine.Extract(incomplete, Folder("incomplete")); } catch (IOException) { rejected = true; }
                Check(rejected, "Missing application files are detected before installing");
            }
            string shortcut = Folder("test-shortcut.lnk"); SetupIntegration.WriteShortcut(shortcut, fresh, "--startup");
            Check(string.Equals(SetupIntegration.ShortcutTarget(shortcut), Path.Combine(fresh, PackageEngine.AppFile), StringComparison.OrdinalIgnoreCase), "Shortcut points at the installed app rather than the installer");
            Check(!Directory.GetDirectories(fresh, ".setup-*").Any() && !Directory.GetDirectories(migrated, ".setup-*").Any(), "Successful installs clean only their own staging directories");
            Console.WriteLine("ALL SETUP TESTS PASSED: " + passed);
        }
    }
}
