using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace QingTodo
{
    internal sealed class MainWindow : Window
    {
        private readonly Repository repository;
        private readonly Document document;
        private readonly Grid body, root;
        private readonly Grid compactView;
        private readonly StackPanel compactList;
        private readonly Border surface;
        private readonly TextBlock count, day, status, draftLabel;
        private readonly TextBox input;
        private readonly StackPanel list;
        private readonly ScrollViewer scroll;
        private readonly Button undoButton;
        private readonly DispatcherTimer saveTimer, reminderTimer, infoTimer, topmostTimer, undoTimer;
        private readonly Dictionary<string, Border> cards = new Dictionary<string, Border>();
        private readonly Dictionary<string, TextBlock> timeLabels = new Dictionary<string, TextBlock>();
        private readonly Dictionary<string, Border> compactCards = new Dictionary<string, Border>();
        private readonly Dictionary<string, TextBlock> compactTimeLabels = new Dictionary<string, TextBlock>();
        private readonly HashSet<string> unread = new HashSet<string>();
        private Forms.NotifyIcon tray;
        private bool exiting, settingsOpen, saveFailed, scheduleOpen, compactMode, switchingView;
        private Todo deleted;
        private int deletedIndex;
        private string lastDay = "";
        private const string SortFormat = "QingTodo.TaskOrder";
        private Point sortStart;
        private string sortSource;
        private string draggedId, previewTargetId;
        private bool previewAfter, sortingDrag;
        private int previewDestination = -1;
        private List<KeyValuePair<string, Border>> dragCards;
        private readonly Dictionary<string, Rect> dragSlots = new Dictionary<string, Rect>();
        private SortFeedback dragFeedback;
        private AdornerLayer feedbackLayer;

        public MainWindow(Repository repo, Document state)
        {
            repository = repo; document = state; Title = "轻待办";
            Width = Clamp(state.Settings.Width, 360, 700, 408); Height = Clamp(state.Settings.Height, 430, 1000, 574);
            MinWidth = 360; MinHeight = 430; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResize;
            WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 0, CornerRadius = new CornerRadius(12), GlassFrameThickness = new Thickness(0), ResizeBorderThickness = new Thickness(6), UseAeroCaptionButtons = false });
            Topmost = state.Settings.AlwaysOnTop;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Rect area = SystemParameters.WorkArea;
            Left = state.Settings.Left >= area.Left && state.Settings.Left + Width <= area.Right ? state.Settings.Left : Math.Max(area.Left, area.Right - Width - 30);
            Top = state.Settings.Top >= area.Top && state.Settings.Top + Height <= area.Bottom ? state.Settings.Top : Math.Max(area.Top, area.Top + 55);
            root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var views = new Grid(); views.Children.Add(root);
            compactView = new Grid { Visibility = Visibility.Collapsed, Margin = new Thickness(11, 5, 11, 9), Background = Brushes.Transparent };
            compactView.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
            compactView.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var compactBar = new Grid { Background = Brushes.Transparent, Cursor = Cursors.SizeAll };
            var grip = Ui.Text("•••", 10, "#A3B09A"); grip.HorizontalAlignment = HorizontalAlignment.Center; grip.VerticalAlignment = VerticalAlignment.Top;
            compactBar.Children.Add(grip);
            var expand = Ui.Icon("\uE740", "展开完整窗口（也可双击空白处）", delegate { SetCompactMode(false); });
            expand.Width = 23; expand.Height = 20; expand.FontSize = 12; expand.HorizontalAlignment = HorizontalAlignment.Right; expand.Opacity = .45;
            compactBar.MouseEnter += delegate { expand.Opacity = 1; }; compactBar.MouseLeave += delegate { expand.Opacity = .45; };
            compactBar.Children.Add(expand); compactView.Children.Add(compactBar);
            compactList = new StackPanel(); Grid.SetRow(compactList, 1); compactView.Children.Add(compactList); views.Children.Add(compactView);
            surface = new Border { CornerRadius = new CornerRadius(12), Background = Ui.Brush(Ui.Paper), BorderBrush = Ui.Brush("#D8E0D2"), BorderThickness = new Thickness(1), Child = views };
            Content = new AdornerDecorator { Child = surface };
            surface.AllowDrop = true;
            surface.PreviewDragOver += HandleSortDragOver;
            surface.PreviewDrop += HandleSortDrop;
            PreviewMouseLeftButtonDown += DragFromBackground;
            var header = new Grid { Margin = new Thickness(22, 14, 13, 0), Height = 34, Background = Brushes.Transparent, Cursor = Cursors.SizeAll };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition());
            for (int i = 0; i < 4; i++) header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var mark = new Image { Width = 24, Height = 24 };
            try { mark.Source = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri(Integration.IconPath, UriKind.Absolute)); } catch { }
            header.Children.Add(mark);
            var name = Ui.Text("轻待办", 14, Ui.Ink); name.FontWeight = FontWeights.SemiBold; name.Margin = new Thickness(9, 0, 0, 0); Grid.SetColumn(name, 1); header.Children.Add(name);
            var compact = Ui.Button("小窗", delegate { SetCompactMode(true); }); compact.FontSize = 11; compact.Width = 42; compact.Height = 28; compact.Padding = new Thickness(0); compact.ToolTip = "只显示三条待办"; compact.Cursor = Cursors.Hand;
            compact.Background = Ui.Brush("#E9EEE5"); Grid.SetColumn(compact, 2); header.Children.Add(compact);
            var settings = Ui.Icon("\uE713", "设置", delegate { ToggleSettings(); }); Grid.SetColumn(settings, 3); header.Children.Add(settings);
            var minimize = Ui.Icon("\uE921", "最小化", delegate { WindowState = WindowState.Minimized; }); Grid.SetColumn(minimize, 4); header.Children.Add(minimize);
            var hide = Ui.Icon("\uE8BB", "收至托盘，继续提醒", delegate { HideToTray(); }); Grid.SetColumn(hide, 5); header.Children.Add(hide);
            root.Children.Add(header);
            body = new Grid { Margin = new Thickness(23, 0, 23, 0) }; Grid.SetRow(body, 1); root.Children.Add(body);
            var todoView = new Grid { Name = "TodoView" };
            todoView.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            todoView.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            todoView.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            body.Children.Add(todoView);
            var heading = new StackPanel { Margin = new Thickness(0, 24, 0, 20) };
            day = Ui.Text("", 12, Ui.Muted); heading.Children.Add(day);
            var headingLine = new Grid { Margin = new Thickness(0, 7, 0, 0) };
            headingLine.ColumnDefinitions.Add(new ColumnDefinition()); headingLine.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var title = Ui.Text("我的待办", 25, Ui.Ink); title.FontWeight = FontWeights.SemiBold; headingLine.Children.Add(title);
            count = Ui.Text("", 11, Ui.Muted); count.Margin = new Thickness(8, 6, 0, 0); Grid.SetColumn(count, 1); headingLine.Children.Add(count); heading.Children.Add(headingLine); todoView.Children.Add(heading);
            var composer = new StackPanel { Margin = new Thickness(0, 0, 0, 18) }; Grid.SetRow(composer, 1); todoView.Children.Add(composer);
            var entry = new Grid(); entry.ColumnDefinitions.Add(new ColumnDefinition()); entry.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var inputArea = new Grid();
            input = new TextBox { Text = state.Settings.Draft ?? "", BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(0, 8, 6, 8), MaxLength = 1000, FontSize = 13 };
            AutomationProperties.SetName(input, "写下一件待办事项");
            var watermark = Ui.Text("写下一件要做的事…", 13, "#969E91"); watermark.IsHitTestVisible = false;
            watermark.Visibility = string.IsNullOrWhiteSpace(input.Text) ? Visibility.Visible : Visibility.Collapsed;
            inputArea.Children.Add(input); inputArea.Children.Add(watermark); entry.Children.Add(inputArea);
            var add = Ui.Icon("\uE710", "添加待办（Enter）", delegate { AddTodo(); }); add.Style = (Style)FindResource("Primary"); add.Width = 30; add.Height = 30;
            Grid.SetColumn(add, 1); entry.Children.Add(add); composer.Children.Add(Ui.Card(entry, new Thickness(12, 5, 7, 5)));
            var schedule = Ui.Button("", delegate { EditDraftTime(); }); schedule.Padding = new Thickness(3, 7, 3, 3); schedule.HorizontalAlignment = HorizontalAlignment.Left;
            var scheduleLine = new StackPanel { Orientation = Orientation.Horizontal };
            var clockIcon = Ui.Text("\uE787", 12, Ui.Muted); clockIcon.FontFamily = new FontFamily("Segoe MDL2 Assets"); clockIcon.Margin = new Thickness(0, 0, 6, 0); scheduleLine.Children.Add(clockIcon);
            draftLabel = Ui.Text("", 11, Ui.Muted); scheduleLine.Children.Add(draftLabel); schedule.Content = scheduleLine;
            AutomationProperties.SetName(schedule, "设置待办时间与提醒，可选"); composer.Children.Add(schedule);
            list = new StackPanel(); scroll = new ScrollViewer { Content = list, Padding = new Thickness(0, 0, 3, 5) }; Grid.SetRow(scroll, 2); todoView.Children.Add(scroll);
            var footer = new Grid { Margin = new Thickness(23, 4, 23, 0), Height = 42 };
            footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var footerBorder = new Border { BorderBrush = Ui.Brush(Ui.Line), BorderThickness = new Thickness(0, 1, 0, 0), Child = footer }; Grid.SetRow(footerBorder, 2); root.Children.Add(footerBorder);
            status = Ui.Text("自动保存于本机", 10, Ui.Muted); status.Cursor = Cursors.Hand;
            status.MouseLeftButtonUp += delegate { if (saveFailed) SaveDocument(); }; footer.Children.Add(status);
            undoButton = Ui.Button("撤销删除", delegate { UndoDelete(); }); undoButton.FontSize = 10; undoButton.Padding = new Thickness(6, 4, 0, 4); undoButton.Visibility = Visibility.Collapsed; Grid.SetColumn(undoButton, 1); footer.Children.Add(undoButton);
            saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
            saveTimer.Tick += delegate { saveTimer.Stop(); SaveDocument(); };
            infoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
            infoTimer.Tick += delegate { infoTimer.Stop(); if (!saveFailed) status.Text = "自动保存于本机"; };
            topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4.3) };
            topmostTimer.Tick += delegate { topmostTimer.Stop(); RestoreTopmostPreference(); Program.Trace("temporary_topmost_ended"); };
            undoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
            undoTimer.Tick += delegate { undoTimer.Stop(); deleted = null; undoButton.Visibility = Visibility.Collapsed; };
            reminderTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            reminderTimer.Tick += delegate { CheckReminders(); UpdateDate(); UpdateTimeLabels(); };
            input.TextChanged += delegate { watermark.Visibility = string.IsNullOrWhiteSpace(input.Text) ? Visibility.Visible : Visibility.Collapsed; document.Settings.Draft = input.Text; ScheduleSave(); };
            input.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(input.Text)) { AddTodo(); e.Handled = true; } };
            LocationChanged += delegate { RememberBounds(); }; SizeChanged += delegate { RememberBounds(); };
            StateChanged += delegate { Program.Trace(WindowState == WindowState.Minimized ? "window_minimized" : "window_normal"); if (WindowState != WindowState.Minimized && unread.Count > 0 && !scheduleOpen && !settingsOpen) PulseUnread(); };
            Closing += OnClosing;
            SourceInitialized += delegate
            {
                Integration.RoundCorners(new WindowInteropHelper(this).Handle);
                try
                {
                    using (var icon = File.Exists(Integration.IconPath) && Integration.IconPath.EndsWith(".ico") ? new System.Drawing.Icon(Integration.IconPath) : System.Drawing.Icon.ExtractAssociatedIcon(Integration.ExePath))
                        Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                }
                catch { }
            };
            Loaded += delegate
            {
                CreateTray(); UpdateDate(); UpdateDraftLabel(); RenderItems(); reminderTimer.Start();
                if (document.Settings.CompactMode) SetCompactMode(true);
                if (!string.IsNullOrEmpty(repository.RecoveryMessage)) Info(repository.RecoveryMessage);
                if (!compactMode) input.Focus(); Program.Trace("window_loaded");
            };
            PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (scheduleOpen) return;
                if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control) { if (compactMode) SetCompactMode(false); ShowTodoView(); input.Focus(); e.Handled = true; }
                else if (e.Key == Key.OemComma && Keyboard.Modifiers == ModifierKeys.Control) { ToggleSettings(); e.Handled = true; }
                else if (e.Key == Key.Escape && settingsOpen) { ShowTodoView(); e.Handled = true; }
            };
        }

        private static double Clamp(double value, double minimum, double maximum, double fallback)
        { return double.IsNaN(value) || double.IsInfinity(value) ? fallback : Math.Max(minimum, Math.Min(value, maximum)); }
        private void DragFromBackground(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            DependencyObject node = e.OriginalSource as DependencyObject;
            while (node != null && node != this)
            {
                if (node is ButtonBase || node is TextBoxBase || node is Selector || node is DatePicker || node is Thumb) return;
                var textNode = node as FrameworkContentElement;
                node = textNode != null ? textNode.Parent : node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
            }
            if (compactMode && e.ClickCount == 2) { SetCompactMode(false); e.Handled = true; return; }
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                e.Handled = true; DragMove(); RememberBounds();
                Program.Trace("window_dragged:compact=" + compactMode);
            }
        }
        private void SetCompactMode(bool enabled)
        {
            if (compactMode == enabled) return;
            if (scheduleOpen) { Info("请先保存或取消时间设置"); return; }
            ShowTodoView();
            switchingView = true;
            try
            {
                Rect area = SystemParameters.WorkArea;
                if (enabled)
                {
                    document.Settings.Left = Left; document.Settings.Top = Top;
                    document.Settings.Width = Width; document.Settings.Height = Height;
                    compactMode = true; document.Settings.CompactMode = true;
                    MinWidth = 280; MinHeight = 132; ResizeMode = ResizeMode.NoResize;
                    Width = 308; Height = 201;
                    double compactLeft = document.Settings.CompactLeft;
                    double compactTop = document.Settings.CompactTop;
                    if (compactLeft >= area.Left && compactLeft + Width <= area.Right) Left = compactLeft;
                    if (compactTop >= area.Top && compactTop + Height <= area.Bottom) Top = compactTop;
                    Left = Math.Max(area.Left, Math.Min(Left, area.Right - Width));
                    Top = Math.Max(area.Top, Math.Min(Top, area.Bottom - Height));
                    document.Settings.CompactLeft = Left; document.Settings.CompactTop = Top;
                    root.Visibility = Visibility.Collapsed; compactView.Visibility = Visibility.Visible;
                    RenderCompactItems(null);
                }
                else
                {
                    document.Settings.CompactLeft = Left; document.Settings.CompactTop = Top;
                    compactMode = false; document.Settings.CompactMode = false;
                    MinWidth = 360; MinHeight = 430; ResizeMode = ResizeMode.CanResize;
                    Width = Clamp(document.Settings.Width, 360, 700, 408);
                    Height = Clamp(document.Settings.Height, 430, 1000, 574);
                    Left = Math.Max(area.Left, Math.Min(document.Settings.Left, area.Right - Width));
                    Top = Math.Max(area.Top, Math.Min(document.Settings.Top, area.Bottom - Height));
                    compactView.Visibility = Visibility.Collapsed; root.Visibility = Visibility.Visible;
                    RenderItems();
                }
            }
            finally { switchingView = false; }
            SaveDocument(); Program.Trace("compact_mode=" + compactMode);
        }
        private void RenderCompactItems(IEnumerable<string> attention)
        {
            compactList.Children.Clear(); compactCards.Clear(); compactTimeLabels.Clear();
            List<Todo> visible = Reminders.CompactItems(document, attention);
            if (visible.Count == 0)
            {
                var empty = Ui.Text("暂无未完成待办", 12, Ui.Muted);
                empty.HorizontalAlignment = HorizontalAlignment.Center; empty.Margin = new Thickness(0, 25, 0, 0);
                compactList.Children.Add(empty); return;
            }
            foreach (Todo item in visible)
            {
                var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(17) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(29) }); row.ColumnDefinitions.Add(new ColumnDefinition());
                row.Children.Add(CreateSortHandle(item));
                var complete = new CheckBox { VerticalAlignment = VerticalAlignment.Center };
                AutomationProperties.SetName(complete, "完成：" + item.Text);
                complete.Checked += delegate { item.Done = true; item.CompletedUtc = DateTime.UtcNow.Ticks; SaveDocument(); RenderItems(); Program.Trace("compact_todo_completed"); };
                Grid.SetColumn(complete, 1); row.Children.Add(complete);
                var words = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.SizeAll };
                var text = Ui.Text(item.Text, 12, Ui.Ink); text.TextWrapping = TextWrapping.NoWrap; text.TextTrimming = TextTrimming.CharacterEllipsis;
                text.ToolTip = item.Text; words.Children.Add(text);
                if (item.DueUtc.HasValue)
                {
                    var time = Ui.Text(Reminders.Summary(item), 10, Ui.Muted); time.Margin = new Thickness(0, 3, 0, 0);
                    time.TextWrapping = TextWrapping.NoWrap; time.TextTrimming = TextTrimming.CharacterEllipsis;
                    compactTimeLabels[item.Id] = time; words.Children.Add(time);
                }
                Grid.SetColumn(words, 2); row.Children.Add(words);
                var card = Ui.Card(row, new Thickness(9, 6, 9, 6)); card.Height = 49; card.Margin = new Thickness(0, 0, 0, 4);
                card.ContextMenu = CreateOrderMenu(item);
                compactCards[item.Id] = card; compactList.Children.Add(card);
            }
            Program.Trace("compact_visible_count=" + visible.Count);
        }
        private void UpdateDate()
        {
            string key = DateTime.Today.ToString("yyyyMMdd"); if (lastDay == key) return; lastDay = key;
            string[] names = { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };
            day.Text = DateTime.Today.ToString("M月d日") + " · " + names[(int)DateTime.Today.DayOfWeek];
        }
        private void RememberBounds()
        {
            if (!IsLoaded || switchingView || WindowState != WindowState.Normal) return;
            if (compactMode) { document.Settings.CompactLeft = Left; document.Settings.CompactTop = Top; }
            else { document.Settings.Left = Left; document.Settings.Top = Top; document.Settings.Width = Width; document.Settings.Height = Height; }
            ScheduleSave();
        }
        private void ScheduleSave() { if (saveTimer == null) return; saveTimer.Stop(); saveTimer.Start(); }
        private bool SaveDocument()
        {
            try
            {
                repository.Save(document); bool wasFailed = saveFailed; saveFailed = false;
                if (wasFailed) { status.Foreground = Ui.Brush(Ui.Muted); status.Text = "已重新保存"; }
                return true;
            }
            catch (Exception error)
            {
                saveFailed = true; status.Foreground = Ui.Brush("#98664C"); status.Text = "保存失败，点击这里重试"; status.ToolTip = error.Message;
                Program.Trace("save_failed:" + error.GetType().Name); return false;
            }
        }
        private void Info(string text)
        {
            if (saveFailed) return; status.Foreground = Ui.Brush(Ui.Muted); status.Text = text;
            infoTimer.Stop(); infoTimer.Start();
        }
        private Todo Draft()
        {
            return new Todo { DueUtc = document.Settings.DraftDueUtc, LeadMinutes = document.Settings.DraftLeadMinutes, Reminder = document.Settings.DraftReminder ?? "popup" };
        }
        private void UpdateDraftLabel()
        {
            Todo draft = Draft(); draftLabel.Text = draft.DueUtc.HasValue ? Reminders.Summary(draft) : "时间与提醒 · 可选";
        }
        private void EditDraftTime()
        {
            ShowSchedule(Draft(), delegate(Todo result)
            {
                document.Settings.DraftDueUtc = result.DueUtc; document.Settings.DraftLeadMinutes = result.LeadMinutes; document.Settings.DraftReminder = result.Reminder;
                UpdateDraftLabel(); SaveDocument(); input.Focus();
            });
        }
        private void AddTodo()
        {
            string text = input.Text.Trim(); if (string.IsNullOrWhiteSpace(text)) { input.Focus(); return; }
            Todo item = Draft(); item.Text = text; TaskOrdering.Append(document, item);
            input.Text = ""; document.Settings.DraftDueUtc = null; document.Settings.DraftLeadMinutes = 10; document.Settings.DraftReminder = "popup";
            UpdateDraftLabel(); SaveDocument(); RenderItems(); input.Focus(); Program.Trace("todo_added"); CheckReminders();
        }
        private void EditTime(Todo item)
        {
            ShowSchedule(item, delegate(Todo result)
            {
                item.DueUtc = result.DueUtc; item.LeadMinutes = result.LeadMinutes; item.Reminder = result.Reminder; item.Reminded = result.Reminded;
                SaveDocument(); RenderItems(); CheckReminders();
            });
        }
        private void ShowSchedule(Todo item, Action<Todo> save)
        {
            ShowTodoView(); scheduleOpen = true; body.Children[0].Visibility = Visibility.Collapsed;
            var panel = new SchedulePanel(item);
            var viewport = new ScrollViewer { Content = panel };
            panel.Finished += delegate(Todo result)
            {
                body.Children.Remove(viewport); body.Children[0].Visibility = Visibility.Visible; scheduleOpen = false;
                if (result != null) save(result); PulseUnread();
            };
            body.Children.Add(viewport);
        }
        private void RenderItems()
        {
            list.Children.Clear(); cards.Clear(); timeLabels.Clear();
            List<Todo> pending = TaskOrdering.Pending(document);
            count.Text = pending.Count == 0 ? "从一件小事开始" : pending.Count + " 项待完成";
            if (pending.Count == 0)
            {
                var empty = new StackPanel { Margin = new Thickness(8, 33, 8, 28), HorizontalAlignment = HorizontalAlignment.Center };
                var icon = Ui.Text("\uE73E", 27, "#B4C3AB"); icon.FontFamily = new FontFamily("Segoe MDL2 Assets"); icon.HorizontalAlignment = HorizontalAlignment.Center; empty.Children.Add(icon);
                var emptyTitle = Ui.Text(document.Items.Any(x => x.Done) ? "该做的，已经做好了" : "把要做的事，写在上面", 13, Ui.Muted); emptyTitle.HorizontalAlignment = HorizontalAlignment.Center; emptyTitle.Margin = new Thickness(0, 14, 0, 6); empty.Children.Add(emptyTitle);
                var hint = Ui.Text("时间和提醒，都可以稍后再设", 11, "#9CA494"); hint.HorizontalAlignment = HorizontalAlignment.Center; empty.Children.Add(hint); list.Children.Add(empty);
            }
            foreach (Todo item in pending) list.Children.Add(CreateRow(item));
            List<Todo> completed = document.Items.Where(x => x.Done).OrderByDescending(x => x.CompletedUtc).ToList();
            if (completed.Count > 0)
            {
                var expand = Ui.Button((document.Settings.CompletedExpanded ? "⌄  " : "›  ") + "已完成 · " + completed.Count, delegate
                { document.Settings.CompletedExpanded = !document.Settings.CompletedExpanded; SaveDocument(); RenderItems(); });
                expand.Foreground = Ui.Brush(Ui.Muted); expand.FontSize = 11; expand.HorizontalAlignment = HorizontalAlignment.Left; expand.Margin = new Thickness(0, 5, 0, 7); list.Children.Add(expand);
                if (document.Settings.CompletedExpanded) foreach (Todo item in completed) list.Children.Add(CreateRow(item));
            }
            if (compactMode) RenderCompactItems(null);
        }
        private Border CreateRow(Todo item)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(item.Done ? 0 : 17) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) }); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25) });
            if (!item.Done) grid.Children.Add(CreateSortHandle(item));
            var done = new CheckBox { IsChecked = item.Done, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 7, 0, 0) };
            AutomationProperties.SetName(done, (item.Done ? "标为未完成：" : "完成：") + item.Text);
            done.Checked += delegate { item.Done = true; item.CompletedUtc = DateTime.UtcNow.Ticks; SaveDocument(); RenderItems(); Program.Trace("todo_completed"); };
            done.Unchecked += delegate { item.Done = false; item.CompletedUtc = null; SaveDocument(); RenderItems(); CheckReminders(); };
            Grid.SetColumn(done, 1); grid.Children.Add(done);
            var content = new StackPanel(); Grid.SetColumn(content, 2); grid.Children.Add(content);
            if (item.Done)
            {
                var text = Ui.Text(item.Text, 13, "#99A091"); text.TextDecorations = TextDecorations.Strikethrough; text.Margin = new Thickness(0, 5, 0, 5); content.Children.Add(text);
            }
            else
            {
                var text = new TextBox { Text = item.Text, BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(0, 5, 2, 5), TextWrapping = TextWrapping.Wrap, MaxLength = 1000, MaxHeight = 115 };
                AutomationProperties.SetName(text, "编辑待办：" + item.Text);
                text.TextChanged += delegate { if (!string.IsNullOrWhiteSpace(text.Text)) { item.Text = text.Text.Trim(); ScheduleSave(); } };
                text.LostKeyboardFocus += delegate { if (string.IsNullOrWhiteSpace(text.Text)) { text.Text = item.Text; Info("待办内容不能为空"); } };
                text.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { input.Focus(); e.Handled = true; } };
                content.Children.Add(text);
            }
            if (!item.Done || item.DueUtc.HasValue)
            {
                var time = Ui.Text(Reminders.Summary(item), 10, item.DueUtc.HasValue ? Ui.Green : "#9CA494"); timeLabels[item.Id] = time;
                var timeButton = Ui.Button("", delegate { EditTime(item); }); timeButton.Content = time; timeButton.Padding = new Thickness(0, 1, 4, 3); timeButton.HorizontalContentAlignment = HorizontalAlignment.Left; timeButton.HorizontalAlignment = HorizontalAlignment.Left;
                AutomationProperties.SetName(timeButton, "编辑时间与提醒：" + item.Text); content.Children.Add(timeButton);
            }
            var more = Ui.Icon("\uE712", "更多操作：" + item.Text, null); more.Width = 25; more.Height = 28; more.VerticalAlignment = VerticalAlignment.Top;
            var menu = item.Done ? new ContextMenu() : CreateOrderMenu(item);
            if (!item.Done) menu.Items.Add(new Separator());
            var scheduleItem = new MenuItem { Header = "编辑时间与提醒" }; scheduleItem.Click += delegate { EditTime(item); }; menu.Items.Add(scheduleItem);
            var deleteItem = new MenuItem { Header = "删除" }; deleteItem.Click += delegate { DeleteTodo(item); }; menu.Items.Add(deleteItem);
            more.ContextMenu = menu; more.Click += delegate { menu.PlacementTarget = more; menu.IsOpen = true; }; Grid.SetColumn(more, 3); grid.Children.Add(more);
            var card = Ui.Card(grid, new Thickness(12, 9, 8, 10)); card.Margin = new Thickness(0, 0, 0, 9); card.Background = Ui.Brush(item.Done ? "#F1F3EE" : "#FFFFFF"); cards[item.Id] = card;
            return card;
        }
        private Button CreateSortHandle(Todo item)
        {
            var handle = Ui.Button("⠿", null); handle.FontFamily = new FontFamily("Segoe UI Symbol"); handle.FontSize = 15;
            handle.Width = 16; handle.Height = 29; handle.Padding = new Thickness(0); handle.Opacity = .6;
            handle.VerticalAlignment = VerticalAlignment.Center; handle.Cursor = Cursors.SizeNS;
            handle.ToolTip = "按住上下拖动调整顺序；也可按 Alt + ↑ / ↓";
            AutomationProperties.SetName(handle, "拖动排序：" + item.Text);
            handle.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            { sortSource = item.Id; sortStart = e.GetPosition(this); };
            handle.PreviewMouseLeftButtonUp += delegate { sortSource = null; };
            handle.PreviewMouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (e.LeftButton != MouseButtonState.Pressed || sortSource != item.Id) return;
                Point position = e.GetPosition(this);
                if (Math.Abs(position.X - sortStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                    Math.Abs(position.Y - sortStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                sortSource = null; e.Handled = true; handle.ReleaseMouseCapture();
                BeginSortDrag(item.Id);
                try { DragDrop.DoDragDrop(handle, new DataObject(SortFormat, item.Id), DragDropEffects.Move); }
                finally { EndSortDrag(true); if (unread.Count > 0) PulseUnread(); }
            };
            handle.PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Alt) == 0) return;
                Key key = e.Key == Key.System ? e.SystemKey : e.Key;
                if (key == Key.Up || key == Key.Down) { e.Handled = true; MoveTask(item, key == Key.Up ? "up" : "down"); }
            };
            return handle;
        }
        private ContextMenu CreateOrderMenu(Todo item)
        {
            var menu = new ContextMenu();
            var first = new MenuItem { Header = "移到最前" }; first.Click += delegate { MoveTask(item, "first"); };
            var up = new MenuItem { Header = "上移" }; up.Click += delegate { MoveTask(item, "up"); };
            var down = new MenuItem { Header = "下移" }; down.Click += delegate { MoveTask(item, "down"); };
            var last = new MenuItem { Header = "移到最后" }; last.Click += delegate { MoveTask(item, "last"); };
            menu.Items.Add(first); menu.Items.Add(up); menu.Items.Add(down); menu.Items.Add(last);
            menu.Opened += delegate
            {
                List<Todo> pending = TaskOrdering.Pending(document); int index = pending.IndexOf(item);
                first.IsEnabled = up.IsEnabled = index > 0;
                down.IsEnabled = last.IsEnabled = index >= 0 && index < pending.Count - 1;
            };
            return menu;
        }
        private void MoveTask(Todo item, string direction)
        {
            List<Todo> pending = TaskOrdering.Pending(document); int index = pending.IndexOf(item);
            if (index < 0 || pending.Count < 2) return;
            int target = direction == "first" ? 0 : direction == "last" ? pending.Count - 1 : direction == "up" ? index - 1 : index + 1;
            if (target < 0 || target >= pending.Count || target == index) return;
            if (TaskOrdering.Move(document, item.Id, pending[target].Id, target > index)) OrderingChanged(item.Id);
        }
        private void OrderingChanged(string itemId, Dictionary<string, Point> previous = null)
        {
            if (previous == null) previous = CaptureCardPositions();
            SaveDocument(); RenderItems();
            if (!compactMode) { Border card; if (cards.TryGetValue(itemId, out card)) card.BringIntoView(); }
            UpdateLayout();
            int animated = 0;
            foreach (var entry in ActiveCards())
            {
                Point before;
                if (previous.TryGetValue(entry.Key, out before))
                {
                    Vector delta = before - entry.Value.TranslatePoint(new Point(), surface);
                    if (delta.Length > .5) { SortMotion.Settle(entry.Value, delta); animated++; }
                }
                else
                {
                    SortMotion.Settle(entry.Value, new Vector(0, 10));
                    entry.Value.BeginAnimation(OpacityProperty, new DoubleAnimation(.3, 1, TimeSpan.FromMilliseconds(180)) { FillBehavior = FillBehavior.Stop });
                    animated++;
                }
            }
            Program.Trace("sort_settle_started:cards=" + animated);
            TraceSortAnimation();
            Info("顺序已保存，越靠前越优先"); Program.Trace("task_order_changed");
        }
        private Dictionary<string, Border> ActiveCards() { return compactMode ? compactCards : cards; }
        private FrameworkElement SortHost() { return compactMode ? (FrameworkElement)compactList : list; }
        private void TraceSortAnimation()
        {
            if (Program.TracePath == null) return;
            int frames = 0;
            EventHandler rendering = delegate { frames++; };
            CompositionTarget.Rendering += rendering;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SortMotion.SettleMilliseconds + 90) };
            timer.Tick += delegate
            {
                timer.Stop(); CompositionTarget.Rendering -= rendering;
                bool settled = ActiveCards().Values.All(card =>
                {
                    var translate = card.RenderTransform as TranslateTransform;
                    return translate == null || (Math.Abs(translate.X) < .1 && Math.Abs(translate.Y) < .1);
                });
                Program.Trace("sort_settle_completed:frames=" + frames + ":zero_offsets=" + settled);
            };
            timer.Start();
        }
        private Dictionary<string, Point> CaptureCardPositions()
        {
            var result = new Dictionary<string, Point>();
            foreach (var entry in ActiveCards()) if (entry.Value.IsVisible) result[entry.Key] = entry.Value.TranslatePoint(new Point(), surface);
            return result;
        }
        private void BeginSortDrag(string id)
        {
            var active = ActiveCards(); Border source;
            if (!active.TryGetValue(id, out source)) return;
            draggedId = id; sortingDrag = true; previewTargetId = null; previewDestination = -1;
            dragCards = active.Where(entry => document.Items.Any(item => item.Id == entry.Key && !item.Done))
                .OrderBy(entry => entry.Value.TranslatePoint(new Point(), SortHost()).Y).ToList();
            dragSlots.Clear();
            foreach (var entry in dragCards)
            {
                entry.Value.RenderTransform = new TranslateTransform();
                dragSlots[entry.Key] = new Rect(entry.Value.TranslatePoint(new Point(), SortHost()), entry.Value.RenderSize);
            }
            feedbackLayer = AdornerLayer.GetAdornerLayer(surface);
            if (feedbackLayer != null)
            {
                dragFeedback = new SortFeedback(surface, source, TranslatePoint(sortStart, surface)); feedbackLayer.Add(dragFeedback);
                dragFeedback.Update(Mouse.GetPosition(surface), null, null);
            }
            source.Opacity = .25;
            if (feedbackLayer != null) feedbackLayer.UpdateLayout();
            Program.Trace("sort_drag_started");
        }
        private void HandleSortDragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(SortFormat)) return;
            e.Handled = true;
            if (!sortingDrag || (e.Data.GetData(SortFormat) as string) != draggedId) { e.Effects = DragDropEffects.None; return; }
            e.Effects = DragDropEffects.Move; UpdateSortPreview(e);
        }
        private void UpdateSortPreview(DragEventArgs e)
        {
            if (dragCards == null || dragCards.Count == 0) return;
            if (!compactMode)
            {
                double y = e.GetPosition(scroll).Y;
                if (y >= 0 && y < 25) scroll.ScrollToVerticalOffset(Math.Max(0, scroll.VerticalOffset - 10));
                else if (y > scroll.ActualHeight - 25 && y <= scroll.ActualHeight) scroll.ScrollToVerticalOffset(scroll.VerticalOffset + 10);
            }
            Point pointer = e.GetPosition(SortHost());
            int sourceIndex = dragCards.FindIndex(entry => entry.Key == draggedId);
            int insertion = dragCards.Count;
            for (int i = 0; i < dragCards.Count; i++)
            {
                Rect slot = dragSlots[dragCards[i].Key];
                if (pointer.Y < slot.Top + slot.Height / 2) { insertion = i; break; }
            }
            int destination = insertion > sourceIndex ? insertion - 1 : insertion;
            destination = Math.Max(0, Math.Min(destination, dragCards.Count - 1));
            Point? lineStart = null, lineEnd = null;
            if (destination != sourceIndex)
            {
                previewTargetId = dragCards[destination].Key; previewAfter = destination > sourceIndex;
                Rect target = dragSlots[previewTargetId];
                Border source = dragCards[sourceIndex].Value;
                double sourceDistance = source.ActualHeight + source.Margin.Top + source.Margin.Bottom;
                double lineY = previewAfter ? target.Bottom - sourceDistance + 2 : target.Top - 2;
                lineStart = SortHost().TranslatePoint(new Point(target.Left + 3, lineY), surface);
                lineEnd = SortHost().TranslatePoint(new Point(target.Right - 3, lineY), surface);
            }
            else previewTargetId = null;
            if (previewDestination != destination)
            {
                previewDestination = destination;
                Border source = dragCards[sourceIndex].Value;
                double distance = source.ActualHeight + source.Margin.Top + source.Margin.Bottom;
                for (int i = 0; i < dragCards.Count; i++)
                {
                    double offset = i > sourceIndex && i <= destination ? -distance : i >= destination && i < sourceIndex ? distance : 0;
                    if (i != sourceIndex) SortMotion.Translate(dragCards[i].Value, 0, offset, SortMotion.PreviewMilliseconds);
                }
                Program.Trace("sort_preview_index=" + destination);
            }
            if (dragFeedback != null) dragFeedback.Update(e.GetPosition(surface), lineStart, lineEnd);
        }
        private void HandleSortDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(SortFormat)) return;
            e.Handled = true; if (!sortingDrag) return;
            UpdateSortPreview(e);
            string sourceId = draggedId, targetId = previewTargetId; bool after = previewAfter;
            var previous = CaptureCardPositions();
            if (dragFeedback != null) previous[sourceId] = dragFeedback.CardLocation;
            if (targetId == null || !TaskOrdering.Move(document, sourceId, targetId, after)) return;
            EndSortDrag(false); OrderingChanged(sourceId, previous);
        }
        private void EndSortDrag(bool animateBack)
        {
            if (!sortingDrag) return;
            Program.Trace("sort_drag_feedback_frames=" + (dragFeedback == null ? 0 : dragFeedback.RenderedFrames));
            if (dragFeedback != null && feedbackLayer != null) feedbackLayer.Remove(dragFeedback);
            if (dragCards != null)
                foreach (var entry in dragCards)
                {
                    entry.Value.Opacity = 1;
                    if (animateBack) SortMotion.Translate(entry.Value, 0, 0, SortMotion.PreviewMilliseconds);
                    else entry.Value.RenderTransform = new TranslateTransform();
                }
            dragFeedback = null; feedbackLayer = null; dragCards = null; dragSlots.Clear();
            draggedId = null; sortingDrag = false; previewTargetId = null; previewDestination = -1;
        }
        private void UpdateTimeLabels()
        {
            foreach (Todo item in document.Items)
            {
                TextBlock label; if (timeLabels.TryGetValue(item.Id, out label)) label.Text = Reminders.Summary(item);
                if (compactTimeLabels.TryGetValue(item.Id, out label)) label.Text = Reminders.Summary(item);
            }
        }
        private void DeleteTodo(Todo item)
        {
            deletedIndex = document.Items.IndexOf(item); deleted = item; document.Items.Remove(item); SaveDocument(); RenderItems();
            undoButton.Visibility = Visibility.Visible; Info("已删除，可以撤销"); undoTimer.Stop(); undoTimer.Start();
        }
        private void UndoDelete()
        {
            if (deleted == null) return; document.Items.Insert(Math.Min(deletedIndex, document.Items.Count), deleted); deleted = null;
            undoTimer.Stop(); undoButton.Visibility = Visibility.Collapsed; SaveDocument(); RenderItems(); Info("已恢复待办");
        }
        private void CheckReminders()
        {
            List<Todo> due = Reminders.Collect(document, DateTime.UtcNow); if (due.Count == 0) return;
            IntPtr foregroundBefore = Integration.GetForegroundWindow();
            SaveDocument(); foreach (Todo item in due) unread.Add(item.Id);
            if (sortingDrag) { Program.Trace("reminder_deferred_during_sort"); return; }
            bool hidden = !IsVisible || WindowState == WindowState.Minimized;
            Program.Trace("reminder_due:" + due.Count + ":hidden=" + hidden);
            if (hidden && due.Any(x => x.Reminder == "popup")) RestoreForReminder();
            if (IsVisible && WindowState != WindowState.Minimized)
            {
                if (scheduleOpen) { Pulse(surface); Info("有待办到提醒时间，返回列表可查看"); }
                else { ShowTodoView(); PulseUnread(); }
            }
            UpdateTimeLabels();
            Program.Trace("reminder_processing_kept_focus=" + (foregroundBefore == Integration.GetForegroundWindow()));
        }
        private void PulseUnread()
        {
            if (unread.Count == 0 || sortingDrag) return;
            if (compactMode)
            {
                RenderCompactItems(unread);
                foreach (string id in unread.ToArray()) { Border card; if (compactCards.TryGetValue(id, out card)) Pulse(card); }
                unread.Clear(); Program.Trace("compact_reminder_highlighted"); return;
            }
            string first = unread.First(); Border firstCard;
            if (cards.TryGetValue(first, out firstCard)) firstCard.BringIntoView();
            foreach (string id in unread.ToArray()) { Border card; if (cards.TryGetValue(id, out card)) Pulse(card); }
            unread.Clear(); Info("有待办到提醒时间了"); Program.Trace("reminder_highlighted");
        }
        private static void Pulse(Border card)
        {
            var previous = card.Background as SolidColorBrush;
            Color baseColor = previous == null ? Colors.White : (Color)previous.GetAnimationBaseValue(SolidColorBrush.ColorProperty);
            var background = new SolidColorBrush(baseColor); card.Background = background;
            var animation = new ColorAnimation { From = baseColor, To = (Color)ColorConverter.ConvertFromString("#DFEBD9"), Duration = TimeSpan.FromMilliseconds(650), AutoReverse = true, RepeatBehavior = new RepeatBehavior(3), FillBehavior = FillBehavior.Stop };
            background.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }
        private void RestoreForReminder()
        {
            IntPtr foregroundBefore = Integration.GetForegroundWindow();
            ShowActivated = false;
            if (!IsVisible) Show();
            IntPtr handle = new WindowInteropHelper(this).Handle;
            // SW_SHOWNOACTIVATE and SWP_NOACTIVATE leave the user's keyboard focus alone.
            Integration.ShowWindow(handle, 4);
            Integration.SetWindowPos(handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x0040);
            topmostTimer.Stop(); topmostTimer.Start(); Program.Trace("reminder_restored_without_activation");
            Program.Trace("reminder_kept_focus=" + (foregroundBefore == Integration.GetForegroundWindow()));
        }
        public void RestoreManually()
        {
            ShowActivated = true; if (!IsVisible) Show(); if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate(); topmostTimer.Stop(); RestoreTopmostPreference();
            if (!scheduleOpen) { if (unread.Count > 0) ShowTodoView(); PulseUnread(); } Program.Trace("window_restored_manually");
        }
        private void RestoreTopmostPreference()
        {
            Topmost = document.Settings.AlwaysOnTop;
            Integration.SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(document.Settings.AlwaysOnTop ? -1 : -2), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
            Program.Trace("topmost_matches_preference=" + (((Integration.GetWindowLong(new WindowInteropHelper(this).Handle, -20) & 8) != 0) == document.Settings.AlwaysOnTop));
        }
        private void HideToTray() { SaveDocument(); Hide(); Program.Trace("window_hidden_to_tray"); }
        private void CreateTray()
        {
            tray = new Forms.NotifyIcon { Text = "轻待办 · 点击打开", Visible = false };
            try { tray.Icon = Integration.IconPath.EndsWith(".ico") ? new System.Drawing.Icon(Integration.IconPath, 32, 32) : System.Drawing.Icon.ExtractAssociatedIcon(Integration.ExePath); } catch { tray.Icon = System.Drawing.SystemIcons.Application; }
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("打开轻待办", null, delegate { Dispatcher.BeginInvoke(new Action(RestoreManually)); });
            menu.Items.Add("切换小窗 / 完整窗口", null, delegate { Dispatcher.BeginInvoke(new Action(delegate { RestoreManually(); SetCompactMode(!compactMode); })); });
            menu.Items.Add("设置", null, delegate { Dispatcher.BeginInvoke(new Action(delegate { RestoreManually(); ShowSettings(); })); });
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("退出轻待办", null, delegate { Dispatcher.BeginInvoke(new Action(ExitApp)); });
            tray.ContextMenuStrip = menu;
            tray.MouseClick += delegate(object sender, Forms.MouseEventArgs e) { if (e.Button == Forms.MouseButtons.Left) Dispatcher.BeginInvoke(new Action(RestoreManually)); };
            tray.Visible = true;
        }
        private void ToggleSettings() { if (scheduleOpen) return; if (settingsOpen) ShowTodoView(); else ShowSettings(); }
        private void ShowTodoView()
        {
            if (!settingsOpen) return;
            while (body.Children.Count > 1) body.Children.RemoveAt(1);
            body.Children[0].Visibility = Visibility.Visible; settingsOpen = false;
        }
        private void ShowSettings()
        {
            if (scheduleOpen) return;
            if (compactMode) SetCompactMode(false);
            ShowTodoView(); settingsOpen = true; body.Children[0].Visibility = Visibility.Collapsed;
            var panel = new StackPanel { Margin = new Thickness(0, 16, 0, 8) };
            var back = Ui.Button("‹  返回待办", delegate { ShowTodoView(); }); back.HorizontalAlignment = HorizontalAlignment.Left; back.Padding = new Thickness(0, 6, 8, 6); back.FontSize = 11; panel.Children.Add(back);
            var title = Ui.Text("偏好设置", 23, Ui.Ink); title.FontWeight = FontWeights.SemiBold; title.Margin = new Thickness(0, 11, 0, 20); panel.Children.Add(title);
            bool updatingStartup = false;
            var startup = new CheckBox { Content = "开机自动打开", IsChecked = Integration.StartupEnabled };
            Action startupChanged = delegate
            {
                if (updatingStartup) return;
                try { Integration.SetStartup(startup.IsChecked == true); Info(startup.IsChecked == true ? "已开启开机自动打开" : "已关闭开机自动打开"); }
                catch (Exception error) { updatingStartup = true; startup.IsChecked = Integration.StartupEnabled; updatingStartup = false; Info("未能更改开机启动：" + error.Message); }
            };
            startup.Checked += delegate { startupChanged(); }; startup.Unchecked += delegate { startupChanged(); }; panel.Children.Add(startup);
            var startupNote = Ui.Text("登录 Windows 后自动打开小窗", 11, Ui.Muted); startupNote.Margin = new Thickness(29, 6, 0, 19); panel.Children.Add(startupNote);
            var pin = new CheckBox { Content = "保持窗口置顶", IsChecked = document.Settings.AlwaysOnTop };
            Action updatePin = delegate { document.Settings.AlwaysOnTop = pin.IsChecked == true; Topmost = document.Settings.AlwaysOnTop; SaveDocument(); };
            pin.Checked += delegate { updatePin(); }; pin.Unchecked += delegate { updatePin(); }; panel.Children.Add(pin);
            var pinNote = Ui.Text("需要时打开，不遮挡其他工作", 11, Ui.Muted); pinNote.Margin = new Thickness(29, 6, 0, 20); panel.Children.Add(pinNote);
            var shortcut = Ui.Button("创建桌面快捷方式  ↗", delegate
            {
                try { Integration.CreateDesktopShortcut(); Info("桌面快捷方式已创建"); } catch (Exception error) { Info("创建失败：" + error.Message); }
            });
            shortcut.HorizontalAlignment = HorizontalAlignment.Left; shortcut.BorderThickness = new Thickness(1); shortcut.BorderBrush = Ui.Brush(Ui.Line); shortcut.Padding = new Thickness(12, 9, 12, 9); panel.Children.Add(shortcut);
            var preview = Ui.Button("试一下轻提醒", delegate
            {
                ShowTodoView(); if (cards.Count > 0) Pulse(cards.Values.First()); else Pulse(surface); Info("就是这样，轻轻提醒三次");
            }); preview.HorizontalAlignment = HorizontalAlignment.Left; preview.Margin = new Thickness(0, 8, 0, 15); panel.Children.Add(preview);
            var note = Ui.Text("点击 × 会收至托盘，提醒继续。\n完全退出后，不再提醒。", 11, Ui.Muted); note.LineHeight = 19; panel.Children.Add(note);
            var exit = Ui.Button("退出轻待办", delegate { ExitApp(); }); exit.Foreground = Ui.Brush(Ui.Muted); exit.HorizontalAlignment = HorizontalAlignment.Left; exit.Margin = new Thickness(0, 14, 0, 0); panel.Children.Add(exit);
            var settingsScroll = new ScrollViewer { Content = panel }; body.Children.Add(settingsScroll);
        }
        private void OnClosing(object sender, CancelEventArgs e)
        {
            if (exiting) return; e.Cancel = true; HideToTray();
        }
        private void ExitApp()
        {
            saveTimer.Stop(); if (!SaveDocument()) { RestoreManually(); return; }
            exiting = true; reminderTimer.Stop(); topmostTimer.Stop(); infoTimer.Stop(); undoTimer.Stop();
            if (tray != null) { tray.Visible = false; var icon = tray.Icon; tray.Dispose(); if (icon != null) icon.Dispose(); }
            Program.Trace("app_exited"); Application.Current.Shutdown();
        }
    }
}
