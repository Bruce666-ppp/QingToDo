using System;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace QingTodo
{
    internal sealed class SchedulePanel : UserControl
    {
        private readonly Todo original;
        private readonly CheckBox timed;
        private readonly DatePicker date;
        private readonly TextBox clock, lead;
        private readonly ComboBox mode;
        private readonly StackPanel fields;
        private readonly TextBlock error;
        public event Action<Todo> Finished;
        private void Finish(Todo result) { var handler = Finished; if (handler != null) handler(result); }

        public SchedulePanel(Todo item)
        {
            original = item.Copy(); MinHeight = 418;
            var root = new Grid { Margin = new Thickness(0, 18, 0, 18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;
            var header = new Grid { Margin = new Thickness(0, 0, 0, 18) };
            header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(Ui.Text("时间与提醒", 19, Ui.Ink));
            var close = Ui.Icon("\uE8BB", "取消时间设置", delegate { Finish(null); }); Grid.SetColumn(close, 1); header.Children.Add(close);
            root.Children.Add(header);
            var content = new StackPanel(); Grid.SetRow(content, 1); root.Children.Add(content);
            timed = new CheckBox { Content = "设置待办时间", IsChecked = item.DueUtc.HasValue, Margin = new Thickness(0, 0, 0, 17) };
            content.Children.Add(timed);
            DateTime suggested = item.DueUtc.HasValue ? Reminders.LocalDate(item) : DateTime.Now.AddHours(1);
            fields = new StackPanel(); content.Children.Add(fields);
            fields.Children.Add(Ui.Text("日期与时间", 12, Ui.Muted));
            var timeGrid = new Grid { Margin = new Thickness(0, 6, 0, 18) };
            timeGrid.ColumnDefinitions.Add(new ColumnDefinition()); timeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            date = new DatePicker { SelectedDate = suggested.Date, SelectedDateFormat = DatePickerFormat.Short, Margin = new Thickness(0, 0, 10, 0) };
            AutomationProperties.SetName(date, "待办日期");
            clock = new TextBox { Text = suggested.ToString("HH:mm"), MaxLength = 5, ToolTip = "24小时制，例如 09:30" };
            AutomationProperties.SetName(clock, "待办时间，24小时制");
            timeGrid.Children.Add(date); Grid.SetColumn(clock, 1); timeGrid.Children.Add(clock); fields.Children.Add(timeGrid);
            var leadGrid = new Grid { Margin = new Thickness(0, 0, 0, 17) };
            leadGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            leadGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(74) });
            leadGrid.ColumnDefinitions.Add(new ColumnDefinition());
            leadGrid.Children.Add(Ui.Text("提前  ", 13, Ui.Ink));
            lead = new TextBox { Text = item.LeadMinutes.ToString(), MaxLength = 5 };
            AutomationProperties.SetName(lead, "提前提醒分钟数"); Grid.SetColumn(lead, 1); leadGrid.Children.Add(lead);
            var minuteLabel = Ui.Text("  分钟提醒", 13, Ui.Ink); Grid.SetColumn(minuteLabel, 2); leadGrid.Children.Add(minuteLabel); fields.Children.Add(leadGrid);
            fields.Children.Add(Ui.Text("提醒方式", 12, Ui.Muted));
            mode = new ComboBox { Margin = new Thickness(0, 6, 0, 0) };
            mode.Items.Add("弹出小窗，轻闪几下"); mode.Items.Add("只轻闪，保持最小化"); mode.Items.Add("不提醒，只记录时间");
            mode.SelectedIndex = item.Reminder == "gentle" ? 1 : item.Reminder == "none" ? 2 : 0;
            AutomationProperties.SetName(mode, "提醒方式"); fields.Children.Add(mode);
            var note = Ui.Text("不发出声音，也不会抢走输入焦点。\n提前填 0，即在设定时间提醒。", 11, Ui.Muted);
            note.Margin = new Thickness(0, 12, 0, 0); content.Children.Add(note);
            error = Ui.Text("", 11, "#98664C"); error.Margin = new Thickness(0, 8, 0, 0); content.Children.Add(error);
            var actions = new Grid(); Grid.SetRow(actions, 2); root.Children.Add(actions);
            actions.ColumnDefinitions.Add(new ColumnDefinition()); actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            var clear = Ui.Button("不设时间", delegate { Todo result = original.Copy(); result.DueUtc = null; result.Reminded = false; Finish(result); });
            clear.HorizontalAlignment = HorizontalAlignment.Left; actions.Children.Add(clear);
            var save = Ui.Button("保存", delegate { Save(); }); save.Style = (Style)FindResource("Primary"); save.Height = 36;
            Grid.SetColumn(save, 1); actions.Children.Add(save);
            Action updateEnabled = delegate { fields.IsEnabled = timed.IsChecked == true; lead.IsEnabled = mode.SelectedIndex != 2; };
            timed.Checked += delegate { updateEnabled(); }; timed.Unchecked += delegate { updateEnabled(); };
            mode.SelectionChanged += delegate { updateEnabled(); }; updateEnabled();
            PreviewKeyDown += delegate(object sender, KeyEventArgs args)
            {
                if (args.Key == Key.Escape) { args.Handled = true; Finish(null); }
            };
        }

        private void Save()
        {
            Todo result = original.Copy();
            if (timed.IsChecked != true) { result.DueUtc = null; result.Reminded = false; Finish(result); return; }
            TimeSpan time; int minutes;
            if (!date.SelectedDate.HasValue || !Reminders.TryTime(clock.Text, out time)) { error.Text = "请填写有效日期，以及 HH:mm 格式的时间。"; return; }
            if (mode.SelectedIndex != 2 && (!int.TryParse(lead.Text, out minutes) || minutes < 0 || minutes > 10080))
            { error.Text = "提前分钟数请填 0–10080 之间的整数。"; return; }
            if (mode.SelectedIndex == 2) minutes = original.LeadMinutes; else minutes = int.Parse(lead.Text);
            DateTime local = DateTime.SpecifyKind(date.SelectedDate.Value.Date.Add(time), DateTimeKind.Local);
            if (TimeZoneInfo.Local.IsInvalidTime(local)) { error.Text = "该时间处于夏令时跳转时段，请换一个时间。"; return; }
            long utc = local.ToUniversalTime().Ticks;
            if (utc <= DateTime.UtcNow.Ticks && (!original.DueUtc.HasValue || utc != original.DueUtc.Value))
            { error.Text = "请选择之后的时间；不设时间也可以直接记录。"; return; }
            string reminder = mode.SelectedIndex == 1 ? "gentle" : mode.SelectedIndex == 2 ? "none" : "popup";
            if (result.DueUtc != utc || result.LeadMinutes != minutes || result.Reminder != reminder) result.Reminded = false;
            result.DueUtc = utc; result.LeadMinutes = minutes; result.Reminder = reminder;
            Finish(result);
        }
    }
}
