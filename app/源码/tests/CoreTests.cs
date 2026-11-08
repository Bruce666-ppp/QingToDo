using System;
using System.IO;
using System.Linq;
using QingTodo;

internal static class CoreTests
{
    private static int passed;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        passed++; Console.WriteLine("PASS: " + name);
    }
    private static void Main(string[] args)
    {
        string folder = Path.Combine(args[0], "core-" + Guid.NewGuid().ToString("N"));
        var repo = new Repository(folder);
        Document doc = repo.Load();
        var now = new DateTime(2026, 11, 8, 4, 0, 0, DateTimeKind.Utc);
        var free = new Todo { Text = "写下一件事，不填时间 ✓" };
        doc.Items.Add(free); repo.Save(doc);
        Check(repo.Load().Items[0].Text == free.Text && repo.Load().Items[0].DueUtc == null, "Unicode and optional time survive restart");
        Check(!Reminders.IsDue(free, now), "Untimed task never triggers");
        var timed = new Todo { Text = "提前提醒", DueUtc = now.AddMinutes(10).Ticks, LeadMinutes = 10 };
        doc.Items.Add(timed);
        Check(!Reminders.IsDue(timed, now.AddTicks(-1)), "Reminder waits until the exact lead boundary");
        Check(Reminders.IsDue(timed, now), "Reminder fires at the lead boundary");
        Check(Reminders.Collect(doc, now).Count == 1 && timed.Reminded, "Reminder marked as delivered");
        Check(Reminders.Collect(doc, now.AddHours(1)).Count == 0, "No repeated reminder in a running session");
        repo.Save(doc); doc = repo.Load();
        Check(Reminders.Collect(doc, now.AddDays(1)).Count == 0, "Delivery status survives restart");
        var completed = new Todo { Text = "已完成", DueUtc = now.Ticks, LeadMinutes = 0, Done = true };
        Check(!Reminders.IsDue(completed, now), "Completed task never triggers");
        var disabled = new Todo { Text = "只记时间", DueUtc = now.Ticks, LeadMinutes = 0, Reminder = "none" };
        Check(!Reminders.IsDue(disabled, now), "Disabled reminders never trigger");
        var gentle = new Todo { Text = "只轻闪", DueUtc = now.Ticks, LeadMinutes = 0, Reminder = "gentle" };
        Check(Reminders.IsDue(gentle, now), "Highlight-only mode can trigger");
        var missed = new Todo { Text = "关机时到点", DueUtc = now.AddHours(-2).Ticks, LeadMinutes = 0 };
        Check(Reminders.IsDue(missed, now), "Missed reminder is caught on resume or restart");
        var zero = new Todo { Text = "准时提醒", DueUtc = now.Ticks, LeadMinutes = 0 };
        Check(!Reminders.IsDue(zero, now.AddSeconds(-1)) && Reminders.IsDue(zero, now), "Zero-minute lead means at the scheduled time");
        var batch = new Document(); batch.Items.Add(gentle); batch.Items.Add(missed); batch.Items.Add(zero);
        Check(Reminders.Collect(batch, now).Count == 3, "Simultaneous reminders are collected together");
        TimeSpan time;
        Check(Reminders.TryTime("9:05", out time) && time == new TimeSpan(9, 5, 0), "24-hour time accepts short hour");
        Check(!Reminders.TryTime("24:00", out time) && !Reminders.TryTime("12:67", out time) && !Reminders.TryTime("noon", out time), "Invalid time rejected");
        doc.Settings.Draft = "未提交的草稿"; doc.Settings.DraftDueUtc = now.AddDays(1).Ticks; repo.Save(doc);
        Check(repo.Load().Settings.Draft == "未提交的草稿" && repo.Load().Settings.DraftDueUtc == doc.Settings.DraftDueUtc, "Draft and optional schedule survive restart");
        Check(File.Exists(repo.FilePath + ".bak"), "Atomic save keeps a previous backup");
        string originalBackup = File.ReadAllText(repo.FilePath + ".bak");
        File.WriteAllText(repo.FilePath, "{broken");
        var recovery = new Repository(folder); Document recovered = recovery.Load();
        Check(recovered.Items.Count == 2 && recovery.RecoveryMessage != null, "Corrupted primary recovers from backup");
        Check(Directory.GetFiles(folder, "*.damaged-*").Length == 1, "Corrupt original is preserved");
        Check(File.ReadAllText(repo.FilePath) == originalBackup, "Recovery restores the last good file");
        string future = File.ReadAllText(repo.FilePath).Replace("\"Version\":1", "\"Version\":99");
        File.WriteAllText(repo.FilePath, future);
        bool futureRejected = false;
        try { repo.Load(); } catch (NotSupportedException) { futureRejected = true; }
        Check(futureRejected && File.ReadAllText(repo.FilePath) == future, "Unknown version rejected without overwriting data");
        var invalid = new Document(); invalid.Items.Add(new Todo { Text = "非法提前分钟", LeadMinutes = -1 });
        bool invalidRejected = false; try { repo.Save(invalid); } catch { invalidRejected = true; }
        Check(invalidRejected, "Invalid reminder offset rejected");
        string shortcutPath = Path.Combine(folder, "shortcut-test.lnk");
        typeof(Integration).GetMethod("CreateShortcut", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Invoke(null, new object[] { shortcutPath, "--startup" });
        Check(File.Exists(shortcutPath) && new FileInfo(shortcutPath).Length > 0, "Windows shortcut creation succeeds without extra dependencies");
        object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true));
        object shortcut = shell.GetType().InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
        string target = (string)shortcut.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, shortcut, null);
        string arguments = (string)shortcut.GetType().InvokeMember("Arguments", System.Reflection.BindingFlags.GetProperty, null, shortcut, null);
        Check(string.Equals(target, Integration.ExePath, StringComparison.OrdinalIgnoreCase) && arguments == "--startup", "Shortcut resolves the executable and startup arguments");
        System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
        System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        var previewDoc = new Document();
        var third = new Todo { Text = "第三件", CreatedUtc = 30 };
        var first = new Todo { Text = "第一件", CreatedUtc = 10 };
        var second = new Todo { Text = "第二件", CreatedUtc = 20 };
        previewDoc.Items.Add(third); previewDoc.Items.Add(new Todo { Text = "已完成不显示", CreatedUtc = 0, Done = true }); previewDoc.Items.Add(second); previewDoc.Items.Add(first);
        var preview = Reminders.CompactItems(previewDoc, null);
        Check(preview.Count == 3 && preview[0].Id == first.Id && preview[1].Id == second.Id && preview[2].Id == third.Id, "Compact view shows the first three unfinished tasks");
        Check(preview.All(item => !item.Done), "Completed tasks are excluded from compact view");
        var fourth = new Todo { Text = "第四件", CreatedUtc = 40 }; previewDoc.Items.Add(fourth);
        Check(Reminders.CompactItems(previewDoc, null).Count == 3 && !Reminders.CompactItems(previewDoc, null).Any(item => item.Id == fourth.Id), "A fourth task stays hidden until one of the first three is completed");
        preview = Reminders.CompactItems(previewDoc, new[] { fourth.Id });
        Check(preview.Count == 3 && preview[0].Id == fourth.Id && preview[1].Id == first.Id, "Offscreen reminder appears without exceeding three compact tasks");
        first.Done = true;
        preview = Reminders.CompactItems(previewDoc, null);
        Check(preview.Count == 3 && preview[0].Id == second.Id && preview[1].Id == third.Id && preview[2].Id == fourth.Id, "Completing a compact task fills the third visible slot with the next task");
        second.Done = true; fourth.Done = true;
        Check(Reminders.CompactItems(previewDoc, null).Count == 1, "A single remaining compact task is not duplicated");
        third.Done = true;
        Check(Reminders.CompactItems(previewDoc, null).Count == 0, "Empty compact list is supported");
        var compactRepo = new Repository(Path.Combine(folder, "compact-prefs"));
        previewDoc.Settings.CompactMode = true; previewDoc.Settings.CompactLeft = 100; previewDoc.Settings.CompactTop = 200;
        compactRepo.Save(previewDoc); var compactReloaded = compactRepo.Load();
        Check(compactReloaded.Settings.CompactMode && compactReloaded.Settings.CompactLeft == 100 && compactReloaded.Settings.CompactTop == 200, "Compact mode and dragged position survive restart");
        string olderData = File.ReadAllText(compactRepo.FilePath).Replace("\"CompactMode\":true,", "").Replace("\"CompactLeft\":100,", "").Replace("\"CompactTop\":200,", "");
        File.WriteAllText(compactRepo.FilePath, olderData);
        var migrated = compactRepo.Load();
        Check(!migrated.Settings.CompactMode && migrated.Settings.CompactLeft == -1 && migrated.Settings.CompactTop == -1, "Existing data opens with safe defaults for new compact preferences");
        var orderDoc = new Document();
        var orderA = new Todo { Text = "A", CreatedUtc = 10, DueUtc = now.AddHours(1).Ticks, LeadMinutes = 5, Reminded = true };
        var orderDone = new Todo { Text = "Done", CreatedUtc = 15, Done = true, CompletedUtc = now.Ticks };
        var orderB = new Todo { Text = "B", CreatedUtc = 20 };
        var orderC = new Todo { Text = "C", CreatedUtc = 30 };
        var orderD = new Todo { Text = "D", CreatedUtc = 40 };
        orderDoc.Items.AddRange(new[] { orderD, orderB, orderDone, orderA, orderC });
        Check(string.Join("", TaskOrdering.Pending(orderDoc).Select(item => item.Text)) == "ABCD", "Legacy tasks keep their original chronological order");
        Check(TaskOrdering.Move(orderDoc, orderD.Id, orderA.Id, false) && string.Join("", TaskOrdering.Pending(orderDoc).Select(item => item.Text)) == "DABC", "Dragging a lower task before the first task raises its priority");
        Check(TaskOrdering.Move(orderDoc, orderA.Id, orderC.Id, true) && string.Join("", TaskOrdering.Pending(orderDoc).Select(item => item.Text)) == "DBCA", "Dragging a task below another task moves it downward");
        Check(string.Join("", Reminders.CompactItems(orderDoc, null).Select(item => item.Text)) == "DBC", "The three compact tasks follow the manual priority order");
        Check(orderA.CreatedUtc == 10 && orderA.DueUtc == now.AddHours(1).Ticks && orderA.LeadMinutes == 5 && orderA.Reminded, "Reordering preserves dates and reminder delivery state");
        Check(!TaskOrdering.Move(orderDoc, orderA.Id, orderA.Id, false) && !TaskOrdering.Move(orderDoc, "missing", orderB.Id, false), "Invalid and self drops do not modify ordering");
        Check(!TaskOrdering.Move(orderDoc, orderDone.Id, orderB.Id, false) && !TaskOrdering.Move(orderDoc, orderA.Id, orderDone.Id, false), "Completed tasks are not reorder targets or sources");
        var orderRepo = new Repository(Path.Combine(folder, "manual-order")); orderRepo.Save(orderDoc);
        Check(string.Join("", TaskOrdering.Pending(orderRepo.Load()).Select(item => item.Text)) == "DBCA", "Manual order survives application restart");
        var orderE = new Todo { Text = "E", CreatedUtc = 1 }; TaskOrdering.Append(orderDoc, orderE);
        Check(string.Join("", TaskOrdering.Pending(orderDoc).Select(item => item.Text)) == "DBCAE", "New tasks append after manually ranked tasks");
        Check(TaskOrdering.Move(orderDoc, orderE.Id, orderD.Id, false) && string.Join("", TaskOrdering.Pending(orderDoc).Select(item => item.Text)) == "EDBCA", "Move to front works after adding a new task");
        Check(TaskOrdering.Move(orderDoc, orderE.Id, orderA.Id, true) && string.Join("", TaskOrdering.Pending(orderDoc).Select(item => item.Text)) == "DBCAE", "Move to back preserves all other task priorities");
        orderB.Done = true;
        Check(string.Join("", TaskOrdering.Pending(orderDoc).Select(item => item.Text)) == "DCAE", "Completing an item preserves the remaining manual order");
        orderB.Done = false;
        Check(string.Join("", TaskOrdering.Pending(orderDoc).Select(item => item.Text)) == "DBCAE", "Restoring a completed item keeps its priority position");
        Check(orderDone.Done && orderDone.CompletedUtc == now.Ticks && orderDoc.Items.Select(item => item.Id).Distinct().Count() == orderDoc.Items.Count, "Reordering preserves completed records and every task identity");
        Console.WriteLine("ALL PASSED: " + passed);
    }
}
