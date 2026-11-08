using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace QingTodo
{
    [DataContract]
    public sealed class Todo
    {
        [DataMember] public string Id = Guid.NewGuid().ToString("N");
        [DataMember] public string Text = "";
        [DataMember] public long CreatedUtc = DateTime.UtcNow.Ticks;
        [DataMember(EmitDefaultValue = false)] public long? SortOrder;
        [DataMember] public bool Done;
        [DataMember] public long? CompletedUtc;
        [DataMember] public long? DueUtc;
        [DataMember] public int LeadMinutes = 10;
        // popup: restore the window if hidden; gentle: highlight only; none: no reminder.
        [DataMember] public string Reminder = "popup";
        [DataMember] public bool Reminded;

        public Todo Copy() { return (Todo)MemberwiseClone(); }
    }

    [DataContract]
    public sealed class Preferences
    {
        [DataMember] public bool AlwaysOnTop;
        [DataMember] public double Left = -1;
        [DataMember] public double Top = -1;
        [DataMember] public double Width = 408;
        [DataMember] public double Height = 574;
        [DataMember] public bool CompletedExpanded;
        [DataMember] public bool CompactMode;
        [DataMember] public double CompactLeft = -1;
        [DataMember] public double CompactTop = -1;
        [DataMember] public string Draft = "";
        [DataMember] public long? DraftDueUtc;
        [DataMember] public int DraftLeadMinutes = 10;
        [DataMember] public string DraftReminder = "popup";
        [OnDeserializing]
        private void SetNewFieldDefaults(StreamingContext context) { CompactLeft = -1; CompactTop = -1; }
    }

    [DataContract]
    public sealed class Document
    {
        [DataMember] public int Version = 1;
        [DataMember] public List<Todo> Items = new List<Todo>();
        [DataMember] public Preferences Settings = new Preferences();
    }

    public sealed class Repository
    {
        public readonly string Folder;
        public string FilePath { get { return Path.Combine(Folder, "todos.json"); } }
        public string RecoveryMessage { get; private set; }
        public Repository(string folder) { Folder = Path.GetFullPath(folder); }

        public Document Load()
        {
            Directory.CreateDirectory(Folder);
            if (!File.Exists(FilePath))
            {
                if (!File.Exists(FilePath + ".bak")) return new Document();
                Document missingRecovered = Read(FilePath + ".bak");
                File.Copy(FilePath + ".bak", FilePath, false);
                RecoveryMessage = "已从备份恢复待办。";
                return missingRecovered;
            }
            try { return Read(FilePath); }
            catch (NotSupportedException) { throw; }
            catch (Exception primaryError)
            {
                if (!File.Exists(FilePath + ".bak"))
                    throw new IOException("待办文件无法读取，原文件已保留：\n" + FilePath, primaryError);
                Document restored;
                try { restored = Read(FilePath + ".bak"); }
                catch (Exception backupError)
                {
                    throw new IOException("待办及备份均无法读取，文件已保留：\n" + Folder, backupError);
                }
                string preserved = FilePath + ".damaged-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
                File.Move(FilePath, preserved);
                File.Copy(FilePath + ".bak", FilePath, false);
                RecoveryMessage = "已从备份恢复；损坏文件已保留。";
                return restored;
            }
        }

        private static Document Read(string file)
        {
            Document document;
            using (var stream = File.OpenRead(file))
                document = (Document)new DataContractJsonSerializer(typeof(Document)).ReadObject(stream);
            Validate(document);
            return document;
        }

        public static void Validate(Document document)
        {
            if (document == null || document.Items == null || document.Settings == null)
                throw new SerializationException("待办文件缺少必要字段。");
            if (document.Version != 1) throw new NotSupportedException("此待办文件版本不受支持，请用对应版本的轻待办打开。");
            var ids = new HashSet<string>();
            foreach (Todo item in document.Items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Id) || !ids.Add(item.Id) ||
                    string.IsNullOrWhiteSpace(item.Text) || item.Text.Length > 1000 ||
                    item.LeadMinutes < 0 || item.LeadMinutes > 10080 ||
                    (item.DueUtc.HasValue && (item.DueUtc.Value < DateTime.MinValue.Ticks || item.DueUtc.Value > DateTime.MaxValue.Ticks)) ||
                    (item.Reminder != "popup" && item.Reminder != "gentle" && item.Reminder != "none"))
                    throw new SerializationException("待办内容无效。");
            }
        }

        public void Save(Document document)
        {
            Validate(document);
            Directory.CreateDirectory(Folder);
            string temporary = FilePath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    new DataContractJsonSerializer(typeof(Document)).WriteObject(stream, document);
                    stream.Flush(true);
                }
                if (File.Exists(FilePath)) File.Replace(temporary, FilePath, FilePath + ".bak", true);
                else File.Move(temporary, FilePath);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }

    public static class TaskOrdering
    {
        private static List<Todo> All(Document document)
        {
            return document.Items.OrderBy(item => item.SortOrder ?? item.CreatedUtc).ThenBy(item => item.CreatedUtc).ToList();
        }
        public static List<Todo> Pending(Document document) { return All(document).Where(item => !item.Done).ToList(); }
        public static void Append(Document document, Todo item)
        {
            var ordered = All(document); ordered.Add(item);
            for (int i = 0; i < ordered.Count; i++) ordered[i].SortOrder = i;
            document.Items.Add(item);
        }
        public static bool Move(Document document, string sourceId, string targetId, bool after)
        {
            if (sourceId == targetId) return false;
            List<Todo> ordered = All(document);
            List<Todo> previous = ordered.Where(item => !item.Done).ToList();
            Todo source = previous.FirstOrDefault(item => item.Id == sourceId);
            if (source == null || !previous.Any(item => item.Id == targetId)) return false;
            var pending = previous.ToList(); pending.Remove(source);
            int insertion = pending.FindIndex(item => item.Id == targetId) + (after ? 1 : 0);
            pending.Insert(insertion, source);
            if (previous.SequenceEqual(pending)) return false;
            // Keep completed items in their existing slots so restoring them has a stable position.
            int next = 0;
            for (int i = 0; i < ordered.Count; i++)
                if (!ordered[i].Done) ordered[i] = pending[next++];
            for (int i = 0; i < ordered.Count; i++) ordered[i].SortOrder = i;
            return true;
        }
    }

    public static class Reminders
    {
        public const int CompactLimit = 3;
        public static List<Todo> CompactItems(Document document, IEnumerable<string> attentionIds)
        {
            var attention = new HashSet<string>(attentionIds ?? Enumerable.Empty<string>());
            return TaskOrdering.Pending(document)
                .OrderBy(item => attention.Contains(item.Id) ? 0 : 1)
                .Take(CompactLimit).ToList();
        }
        public static bool IsDue(Todo item, DateTime nowUtc)
        {
            return !item.Done && !item.Reminded && item.DueUtc.HasValue && item.Reminder != "none" &&
                nowUtc.Ticks >= item.DueUtc.Value - TimeSpan.FromMinutes(item.LeadMinutes).Ticks;
        }
        public static List<Todo> Collect(Document document, DateTime nowUtc)
        {
            List<Todo> due = document.Items.Where(item => IsDue(item, nowUtc)).ToList();
            foreach (Todo item in due) item.Reminded = true;
            return due;
        }
        public static DateTime LocalDate(Todo item)
        {
            return new DateTime(item.DueUtc.Value, DateTimeKind.Utc).ToLocalTime();
        }
        public static bool TryTime(string value, out TimeSpan time)
        {
            DateTime parsed;
            bool valid = DateTime.TryParseExact(value.Trim(), new[] { "H:mm", "HH:mm" }, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out parsed);
            time = valid ? parsed.TimeOfDay : TimeSpan.Zero;
            return valid;
        }
        public static string Summary(Todo item)
        {
            if (!item.DueUtc.HasValue) return "添加时间";
            DateTime local = LocalDate(item);
            string day = local.Date == DateTime.Today ? "今天" :
                local.Date == DateTime.Today.AddDays(1) ? "明天" : local.ToString("M月d日");
            if (local.Year != DateTime.Today.Year) day = local.ToString("yyyy年M月d日");
            string result = day + " " + local.ToString("HH:mm");
            if (item.Reminder != "none")
                result += item.Reminded ? " · 已提醒" : item.LeadMinutes == 0 ? " · 准时提醒" : " · 提前" + item.LeadMinutes + "分钟";
            return result;
        }
    }
}
