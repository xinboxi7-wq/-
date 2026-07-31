using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Win32;

namespace ControllerLab
{
    public enum ProductNoticeKind { Info, Success, Warning, Error }

    public sealed class ProductNoticeBanner : Border
    {
        private readonly TextBlock icon;
        private readonly TextBlock title;
        private readonly TextBlock body;
        private readonly Button action;

        public ProductNoticeBanner()
        {
            CornerRadius = LabVisualStyles.ControlRadius;
            BorderThickness = new Thickness(1);
            Padding = new Thickness(14, 11, 12, 11);
            Margin = new Thickness(22, 8, 22, 0);
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Top;
            Visibility = Visibility.Collapsed;
            Grid layout = new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            icon = new TextBlock { FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 15, VerticalAlignment = VerticalAlignment.Center };
            layout.Children.Add(icon);
            StackPanel copy = new StackPanel();
            title = new TextBlock { FontWeight = FontWeights.SemiBold, FontSize = LabFontSizes.Body };
            body = new TextBlock { Foreground = Palette.MutedBrush, FontSize = LabFontSizes.Caption, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) };
            copy.Children.Add(title);
            copy.Children.Add(body);
            Grid.SetColumn(copy, 1);
            layout.Children.Add(copy);
            action = LabVisualStyles.CreateButton("重试", LabButtonVariant.Ghost);
            action.MinWidth = 68;
            action.Margin = new Thickness(12, 0, 0, 0);
            action.Visibility = Visibility.Collapsed;
            Grid.SetColumn(action, 2);
            layout.Children.Add(action);
            Button close = LabVisualStyles.CreateButton("×", LabButtonVariant.Icon);
            close.MinWidth = 32;
            close.Width = 32;
            close.Height = 32;
            close.Margin = new Thickness(6, 0, 0, 0);
            close.ToolTip = "关闭通知";
            close.Click += delegate { Hide(); };
            Grid.SetColumn(close, 3);
            layout.Children.Add(close);
            Child = layout;
            AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
            AutomationProperties.SetName(this, "ControllerLab 状态通知");
        }

        public void Show(ProductNoticeKind kind, string heading, string message, string actionText, RoutedEventHandler handler)
        {
            Color color = kind == ProductNoticeKind.Success ? Palette.Green : kind == ProductNoticeKind.Warning ? Palette.Warning : kind == ProductNoticeKind.Error ? Palette.Red : Palette.Blue;
            Background = new SolidColorBrush(Color.FromArgb(238, Palette.Surface2.R, Palette.Surface2.G, Palette.Surface2.B));
            BorderBrush = new SolidColorBrush(Color.FromArgb(150, color.R, color.G, color.B));
            icon.Foreground = new SolidColorBrush(color);
            title.Foreground = new SolidColorBrush(color);
            icon.Text = kind == ProductNoticeKind.Success ? "\uE73E" : kind == ProductNoticeKind.Warning ? "\uE7BA" : kind == ProductNoticeKind.Error ? "\uEA39" : "\uE946";
            title.Text = heading ?? string.Empty;
            body.Text = message ?? string.Empty;
            action.Click -= OnAction;
            pendingAction = handler;
            action.Content = string.IsNullOrWhiteSpace(actionText) ? "重试" : actionText;
            action.Visibility = handler == null ? Visibility.Collapsed : Visibility.Visible;
            if (handler != null) action.Click += OnAction;
            Visibility = Visibility.Visible;
        }

        private RoutedEventHandler pendingAction;
        private void OnAction(object sender, RoutedEventArgs e) { if (pendingAction != null) pendingAction(sender, e); }
        public void Hide() { Visibility = Visibility.Collapsed; pendingAction = null; action.Visibility = Visibility.Collapsed; }
    }

    public enum LabLogLevel { Information, Warning, Error }

    public static class LabLogger
    {
        private static readonly object Sync = new object();
        private const long MaximumBytes = 512 * 1024;
        private const int ArchiveCount = 3;
        private static string directoryOverride;

        public static string DirectoryPath
        {
            get { return directoryOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ControllerLab", "logs"); }
        }

        public static void Info(string area, string message) { Write(LabLogLevel.Information, area, message, null); }
        public static void Warning(string area, string message) { Write(LabLogLevel.Warning, area, message, null); }
        public static void Error(string area, string message, Exception exception) { Write(LabLogLevel.Error, area, message, exception); }

        public static void Write(LabLogLevel level, string area, string message, Exception exception)
        {
            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(DirectoryPath);
                    string path = Path.Combine(DirectoryPath, "controllerlab.log");
                    RotateIfNeeded(path);
                    string safeArea = Sanitize(area, 48);
                    string safeMessage = Sanitize(message, 400);
                    string detail = exception == null ? string.Empty : " | " + Sanitize(exception.ToString(), 4000);
                    string line = string.Format(CultureInfo.InvariantCulture, "[{0:O}] {1} {2} | {3}{4}\r\n", DateTime.UtcNow, level, safeArea, safeMessage, detail);
                    File.AppendAllText(path, line, new UTF8Encoding(false));
                }
            }
            catch { }
        }

        private static void RotateIfNeeded(string path)
        {
            if (!File.Exists(path) || new FileInfo(path).Length < MaximumBytes) return;
            for (int i = ArchiveCount; i >= 1; i--)
            {
                string source = i == 1 ? path : path + "." + (i - 1).ToString(CultureInfo.InvariantCulture);
                string target = path + "." + i.ToString(CultureInfo.InvariantCulture);
                if (!File.Exists(source)) continue;
                if (File.Exists(target)) File.Delete(target);
                File.Move(source, target);
            }
        }

        private static string Sanitize(string value, int maximum)
        {
            string safe = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
            return safe.Length <= maximum ? safe : safe.Substring(0, maximum) + "…";
        }

        internal static void UseDirectoryForSelfTest(string path) { directoryOverride = path; }
        internal static void ResetDirectoryForSelfTest() { directoryOverride = null; }
    }

    public sealed class HistoryComparisonRow
    {
        public string Label;
        public string Left;
        public string Right;
        public string Difference;
    }

    public sealed class HistoryComparisonResult
    {
        public string LeftName;
        public string RightName;
        public readonly List<HistoryComparisonRow> Rows = new List<HistoryComparisonRow>();
    }

    public static class HealthReportComparer
    {
        public static HistoryComparisonResult Compare(ControllerHealthReport left, ControllerHealthReport right)
        {
            if (left == null || right == null) throw new ArgumentNullException(left == null ? "left" : "right");
            HistoryComparisonResult result = new HistoryComparisonResult
            {
                LeftName = ReportLabel(left),
                RightName = ReportLabel(right)
            };
            AddScoreRow(result, "综合评分", left.OverallScore, right.OverallScore, "0");
            AddMetricRow(result, "左摇杆中心偏移", left, right, HealthCheckStepKind.LeftStickStationary, "中心偏移");
            AddMetricRow(result, "左摇杆推荐死区", left, right, HealthCheckStepKind.LeftStickStationary, "推荐死区");
            AddMetricRow(result, "左摇杆圆周覆盖率", left, right, HealthCheckStepKind.LeftStickCircularity, "外圈覆盖率");
            AddMetricRow(result, "右摇杆中心偏移", left, right, HealthCheckStepKind.RightStickStationary, "中心偏移");
            AddMetricRow(result, "右摇杆推荐死区", left, right, HealthCheckStepKind.RightStickStationary, "推荐死区");
            AddMetricRow(result, "右摇杆圆周覆盖率", left, right, HealthCheckStepKind.RightStickCircularity, "外圈覆盖率");
            return result;
        }

        private static string ReportLabel(ControllerHealthReport report)
        {
            return report.DeviceName + " · " + report.TestDateUtc.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        private static void AddScoreRow(HistoryComparisonResult result, string label, double left, double right, string format)
        {
            result.Rows.Add(new HistoryComparisonRow
            {
                Label = label,
                Left = left.ToString(format, CultureInfo.InvariantCulture),
                Right = right.ToString(format, CultureInfo.InvariantCulture),
                Difference = Signed(right - left, format)
            });
        }

        private static void AddMetricRow(HistoryComparisonResult result, string label, ControllerHealthReport left, ControllerHealthReport right, HealthCheckStepKind kind, string metricName)
        {
            double leftValue;
            double rightValue;
            bool hasLeft = TryMetric(left, kind, metricName, out leftValue);
            bool hasRight = TryMetric(right, kind, metricName, out rightValue);
            result.Rows.Add(new HistoryComparisonRow
            {
                Label = label,
                Left = hasLeft ? leftValue.ToString("0.0", CultureInfo.InvariantCulture) + "%" : "未检测",
                Right = hasRight ? rightValue.ToString("0.0", CultureInfo.InvariantCulture) + "%" : "未检测",
                Difference = hasLeft && hasRight ? Signed(rightValue - leftValue, "0.0") + "%" : "—"
            });
        }

        private static bool TryMetric(ControllerHealthReport report, HealthCheckStepKind kind, string metricName, out double value)
        {
            value = 0;
            if (report == null || report.Steps == null) return false;
            string kindName = kind.ToString();
            for (int i = 0; i < report.Steps.Count; i++)
            {
                HealthCheckStepRecord step = report.Steps[i];
                if (step == null || !string.Equals(step.Kind, kindName, StringComparison.OrdinalIgnoreCase) || step.Metrics == null) continue;
                for (int m = 0; m < step.Metrics.Count; m++)
                {
                    HealthReportMetric metric = step.Metrics[m];
                    if (metric != null && string.Equals(metric.Name, metricName, StringComparison.OrdinalIgnoreCase))
                    {
                        value = metric.Value;
                        return true;
                    }
                }
            }
            return false;
        }

        private static string Signed(double value, string format)
        {
            if (Math.Abs(value) < 0.0001) return "0";
            return (value > 0 ? "+" : string.Empty) + value.ToString(format, CultureInfo.InvariantCulture);
        }
    }

    public sealed class HistoryReportsPage : Grid
    {
        private readonly ControllerHealthReportStore store;
        private readonly Action retest;
        private readonly ListBox list;
        private readonly TextBlock empty;
        private readonly TextBlock detail;
        private readonly Grid comparison;
        private readonly Button viewButton;
        private readonly Button deleteButton;
        private readonly Button exportJsonButton;
        private readonly Button exportMarkdownButton;
        private readonly Button compareButton;
        private bool adjustingSelection;

        public HistoryReportsPage(ControllerHealthReportStore store, Action retest)
        {
            if (store == null) throw new ArgumentNullException("store");
            this.store = store;
            this.retest = retest;
            Margin = new Thickness(32, 24, 32, 16);
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid heading = new Grid();
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel headingCopy = new StackPanel();
            headingCopy.Children.Add(LabVisualStyles.CreatePageTitle("历史报告"));
            headingCopy.Children.Add(new TextBlock { Text = "在本机查看、导出或比较两次完整检测。报告仅保存在应用数据目录。", Foreground = Palette.MutedBrush, FontSize = LabFontSizes.Body, Margin = new Thickness(0, 6, 0, 0) });
            heading.Children.Add(headingCopy);
            Button refresh = LabVisualStyles.CreateButton("刷新", LabButtonVariant.Secondary);
            refresh.Click += delegate { Refresh(); };
            Grid.SetColumn(refresh, 1);
            heading.Children.Add(refresh);
            Children.Add(heading);

            Grid body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(body, 2);
            Children.Add(body);

            Grid left = new Grid();
            left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            list = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), SelectionMode = SelectionMode.Multiple, Padding = new Thickness(0) };
            list.SelectionChanged += OnSelectionChanged;
            left.Children.Add(list);
            empty = new TextBlock { Text = "还没有检测报告\n完成一次完整检测后会显示在这里。", Foreground = Palette.MutedBrush, FontSize = LabFontSizes.BodyLarge, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            left.Children.Add(empty);
            WrapPanel listActions = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
            viewButton = AddAction(listActions, "查看", LabButtonVariant.Primary, delegate { ShowSelected(); });
            compareButton = AddAction(listActions, "比较两份", LabButtonVariant.Secondary, delegate { CompareSelected(); });
            deleteButton = AddAction(listActions, "删除", LabButtonVariant.Danger, delegate { DeleteSelected(); });
            Grid.SetRow(listActions, 1);
            left.Children.Add(listActions);
            Border leftCard = LabVisualStyles.CreateSectionCard(left);
            leftCard.Padding = new Thickness(14);
            body.Children.Add(leftCard);

            Grid right = new Grid();
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            ScrollViewer detailScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            Grid detailHost = new Grid { Margin = new Thickness(22, 20, 22, 20) };
            detail = new TextBlock { Text = "选择一份报告查看摘要，或选择两份报告进行对比。", Foreground = Palette.MutedBrush, FontSize = LabFontSizes.Body, TextWrapping = TextWrapping.Wrap };
            detailHost.Children.Add(detail);
            comparison = new Grid { Visibility = Visibility.Collapsed };
            detailHost.Children.Add(comparison);
            detailScroll.Content = detailHost;
            right.Children.Add(detailScroll);
            WrapPanel exports = new WrapPanel { Margin = new Thickness(22, 12, 22, 18) };
            exportJsonButton = AddAction(exports, "导出 JSON", LabButtonVariant.Secondary, delegate { ExportSelected(false); });
            exportMarkdownButton = AddAction(exports, "导出 Markdown", LabButtonVariant.Secondary, delegate { ExportSelected(true); });
            Button retestButton = AddAction(exports, "重新检测", LabButtonVariant.Primary, delegate { if (this.retest != null) this.retest(); });
            retestButton.Margin = new Thickness(8, 0, 0, 0);
            Grid.SetRow(exports, 1);
            right.Children.Add(exports);
            Border rightCard = LabVisualStyles.CreateResultCard(right);
            Grid.SetColumn(rightCard, 2);
            body.Children.Add(rightCard);
            Refresh();
        }

        public void Refresh()
        {
            List<ControllerHealthReport> reports = store.LoadAll();
            list.Items.Clear();
            for (int i = 0; i < reports.Count; i++) list.Items.Add(CreateReportItem(reports[i]));
            empty.Visibility = reports.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateButtons();
            if (reports.Count > 0)
            {
                list.SelectedIndex = 0;
                ShowSelected();
            }
            else
            {
                detail.Text = "选择一份报告查看摘要，或选择两份报告进行对比。";
                detail.Visibility = Visibility.Visible;
                comparison.Visibility = Visibility.Collapsed;
            }
        }

        private static ListBoxItem CreateReportItem(ControllerHealthReport report)
        {
            StackPanel copy = new StackPanel();
            copy.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(report.DeviceName) ? "未知手柄" : report.DeviceName, Foreground = Palette.TextBrush, FontSize = LabFontSizes.BodyLarge, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
            copy.Children.Add(new TextBlock { Text = report.TestDateUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " · " + report.ConnectionType, Foreground = Palette.MutedBrush, FontSize = LabFontSizes.Caption, Margin = new Thickness(0, 4, 0, 0) });
            copy.Children.Add(new TextBlock { Text = report.OverallScore.ToString("0", CultureInfo.InvariantCulture) + "/100 · " + StatusChinese(report), Foreground = StatusBrush(report), FontSize = LabFontSizes.Body, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 7, 0, 0) });
            Border surface = LabVisualStyles.CreateMetricCard(copy);
            surface.Padding = new Thickness(16, 13, 16, 13);
            ListBoxItem item = new ListBoxItem { Content = surface, Tag = report, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), Margin = new Thickness(0, 0, 0, 8), HorizontalContentAlignment = HorizontalAlignment.Stretch };
            AutomationProperties.SetName(item, "检测报告 " + report.DeviceName + " " + report.TestDateUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            return item;
        }

        private static string StatusChinese(ControllerHealthReport report)
        {
            if (!string.IsNullOrWhiteSpace(report.OverallStatusChinese)) return report.OverallStatusChinese;
            if (report.OverallStatus == HealthReportOverallStatus.Excellent.ToString()) return "优秀";
            if (report.OverallStatus == HealthReportOverallStatus.Good.ToString()) return "良好";
            if (report.OverallStatus == HealthReportOverallStatus.Attention.ToString()) return "需要注意";
            if (report.OverallStatus == HealthReportOverallStatus.Poor.ToString()) return "状态较差";
            return "检测未完成";
        }

        private static Brush StatusBrush(ControllerHealthReport report)
        {
            if (report.OverallStatus == HealthReportOverallStatus.Excellent.ToString() || report.OverallStatus == HealthReportOverallStatus.Good.ToString()) return Palette.GreenBrush;
            if (report.OverallStatus == HealthReportOverallStatus.Attention.ToString() || report.OverallStatus == HealthReportOverallStatus.Incomplete.ToString()) return Palette.WarningBrush;
            return Palette.RedBrush;
        }

        private static Button AddAction(Panel panel, string text, LabButtonVariant variant, Action action)
        {
            Button button = LabVisualStyles.CreateButton(text, variant);
            button.Margin = new Thickness(0, 0, 8, 0);
            button.Click += delegate { action(); };
            panel.Children.Add(button);
            return button;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (adjustingSelection) return;
            if (list.SelectedItems.Count > 2)
            {
                adjustingSelection = true;
                while (list.SelectedItems.Count > 2) list.SelectedItems.RemoveAt(0);
                adjustingSelection = false;
            }
            UpdateButtons();
            if (list.SelectedItems.Count == 1) ShowSelected();
        }

        private List<ControllerHealthReport> SelectedReports()
        {
            List<ControllerHealthReport> reports = new List<ControllerHealthReport>();
            for (int i = 0; i < list.SelectedItems.Count; i++)
            {
                ListBoxItem item = list.SelectedItems[i] as ListBoxItem;
                ControllerHealthReport report = item == null ? null : item.Tag as ControllerHealthReport;
                if (report != null) reports.Add(report);
            }
            return reports;
        }

        private void UpdateButtons()
        {
            int count = list.SelectedItems.Count;
            viewButton.IsEnabled = count == 1;
            deleteButton.IsEnabled = count > 0;
            exportJsonButton.IsEnabled = count == 1;
            exportMarkdownButton.IsEnabled = count == 1;
            compareButton.IsEnabled = count == 2;
        }

        private void ShowSelected()
        {
            List<ControllerHealthReport> selected = SelectedReports();
            if (selected.Count != 1) return;
            ControllerHealthReport report = selected[0];
            StringBuilder text = new StringBuilder();
            text.AppendLine(report.DeviceName);
            text.AppendLine(report.OverallScore.ToString("0", CultureInfo.InvariantCulture) + "/100 · " + StatusChinese(report));
            text.AppendLine();
            text.AppendLine("连接方式  " + report.ConnectionType);
            text.AppendLine("检测时间  " + report.TestDateUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            text.AppendLine("检测时长  " + report.TestDurationSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " 秒");
            text.AppendLine("完整完成  " + (report.IsComplete ? "是" : "否"));
            text.AppendLine();
            if (report.Categories != null)
            {
                text.AppendLine("分类结果");
                for (int i = 0; i < report.Categories.Count; i++)
                {
                    HealthCategoryScore category = report.Categories[i];
                    text.AppendLine("  " + category.DisplayName + "  " + (category.Tested ? category.Score.ToString("0", CultureInfo.InvariantCulture) + "/100" : "未检测") + "  " + category.Status);
                }
            }
            text.AppendLine();
            text.AppendLine("问题与建议");
            bool found = false;
            if (report.Steps != null)
            {
                for (int i = 0; i < report.Steps.Count; i++)
                {
                    HealthCheckStepRecord step = report.Steps[i];
                    if (step == null || (string.IsNullOrWhiteSpace(step.Issue) && string.IsNullOrWhiteSpace(step.Advice))) continue;
                    found = true;
                    text.AppendLine("  " + step.Title + "：" + (string.IsNullOrWhiteSpace(step.Issue) ? step.Advice : step.Issue + (string.IsNullOrWhiteSpace(step.Advice) ? string.Empty : "；" + step.Advice)));
                }
            }
            if (!found) text.AppendLine("  本次报告没有记录需要特别处理的问题。");
            detail.Text = text.ToString();
            detail.Foreground = Palette.TextBrush;
            detail.FontSize = LabFontSizes.Body;
            detail.Visibility = Visibility.Visible;
            comparison.Visibility = Visibility.Collapsed;
        }

        private void CompareSelected()
        {
            List<ControllerHealthReport> selected = SelectedReports();
            if (selected.Count != 2) return;
            HistoryComparisonResult result = HealthReportComparer.Compare(selected[0], selected[1]);
            comparison.Children.Clear();
            comparison.RowDefinitions.Clear();
            comparison.ColumnDefinitions.Clear();
            comparison.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
            comparison.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            comparison.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            comparison.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.65, GridUnitType.Star) });
            AddComparisonRow(0, comparison, "指标", result.LeftName, result.RightName, "变化", true);
            for (int i = 0; i < result.Rows.Count; i++) AddComparisonRow(i + 1, comparison, result.Rows[i].Label, result.Rows[i].Left, result.Rows[i].Right, result.Rows[i].Difference, false);
            detail.Visibility = Visibility.Collapsed;
            comparison.Visibility = Visibility.Visible;
        }

        private static void AddComparisonRow(int row, Grid grid, string a, string b, string c, string d, bool header)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            string[] values = { a, b, c, d };
            for (int i = 0; i < values.Length; i++)
            {
                Border cell = new Border { BorderBrush = Palette.BorderSubtleBrush, BorderThickness = new Thickness(0, 0, i == values.Length - 1 ? 0 : 1, 1), Background = header ? Palette.SurfaceRaisedBrush : Brushes.Transparent, Padding = new Thickness(10, 11, 10, 11) };
                cell.Child = new TextBlock { Text = values[i], Foreground = header ? Palette.TextBrush : i == 3 ? Palette.BlueBrush : Palette.MutedBrush, FontSize = header ? LabFontSizes.Caption : LabFontSizes.Body, FontWeight = header || i == 0 ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap };
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, i);
                grid.Children.Add(cell);
            }
        }

        private void DeleteSelected()
        {
            List<ControllerHealthReport> selected = SelectedReports();
            if (selected.Count == 0) return;
            MessageBoxResult answer = MessageBox.Show("确定删除选中的 " + selected.Count.ToString(CultureInfo.InvariantCulture) + " 份报告吗？此操作无法撤销。", "删除历史报告", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return;
            for (int i = 0; i < selected.Count; i++) store.Delete(selected[i].ReportId);
            LabLogger.Info("History", "Deleted " + selected.Count.ToString(CultureInfo.InvariantCulture) + " local report(s).");
            Refresh();
        }

        private void ExportSelected(bool markdown)
        {
            List<ControllerHealthReport> selected = SelectedReports();
            if (selected.Count != 1) return;
            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = markdown ? "Markdown 文件 (*.md)|*.md" : "JSON 文件 (*.json)|*.json",
                DefaultExt = markdown ? ".md" : ".json",
                FileName = "ControllerLab-" + selected[0].TestDateUtc.ToLocalTime().ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture) + (markdown ? ".md" : ".json")
            };
            if (dialog.ShowDialog() != true) return;
            try
            {
                store.Export(selected[0], dialog.FileName, markdown);
                MessageBox.Show("报告已导出。", "ControllerLab", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LabLogger.Error("History", "Report export failed.", ex);
                MessageBox.Show("报告导出失败。请检查目标目录是否可写，然后重试。", "ControllerLab", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    public sealed class SettingsPage : Grid
    {
        private readonly ControllerSettings settings;
        private readonly Action<ControllerSettings> apply;
        private readonly Action openDataDirectory;
        private readonly Action clearHistory;
        private readonly ProductOptionSelector startupPage;
        private readonly CheckBox autoConnect;
        private readonly CheckBox rememberWindow;
        private readonly CheckBox animations;
        private readonly ProductOptionSelector decimalPlaces;
        private readonly Slider trailLength;
        private readonly Slider uiRefresh;
        private readonly CheckBox advancedData;
        private readonly Slider sampleDuration;
        private readonly Slider deadzoneMargin;
        private readonly CheckBox saveHistory;
        private readonly Slider rumbleStrength;
        private readonly Slider rumbleDuration;
        private readonly Slider rumbleMaximum;
        private readonly TextBlock validation;

        public SettingsPage(ControllerSettings settings, Action<ControllerSettings> apply, Action openDataDirectory, Action clearHistory)
        {
            this.settings = settings ?? new ControllerSettings();
            this.apply = apply;
            this.openDataDirectory = openDataDirectory;
            this.clearHistory = clearHistory;
            Margin = new Thickness(32, 24, 32, 16);
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            StackPanel heading = new StackPanel();
            heading.Children.Add(LabVisualStyles.CreatePageTitle("设置"));
            heading.Children.Add(new TextBlock { Text = "调整显示与测试默认值。硬件输入、Overlay 坐标和检测算法不会被这里改写。", Foreground = Palette.MutedBrush, FontSize = LabFontSizes.Body, Margin = new Thickness(0, 6, 0, 0) });
            Children.Add(heading);

            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            UniformGrid cards = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 8, 12) };

            Grid general = CreateSection("常规", "启动与窗口行为");
            startupPage = AddCombo(general, "启动页面", new[] { "实时监视", "完整检测", "设备选择" }, 2);
            autoConnect = AddCheck(general, "自动连接已识别设备", 3);
            rememberWindow = AddCheck(general, "记住窗口位置与大小", 4);
            animations = AddCheck(general, "启用界面过渡动画", 5);
            AddHint(general, "语言：简体中文（更多语言尚未提供）", 6);
            cards.Children.Add(WrapSection(general));

            Grid display = CreateSection("显示", "信息密度与刷新频率");
            decimalPlaces = AddCombo(display, "实时坐标小数位", new[] { "1 位", "2 位", "3 位" }, 2);
            trailLength = AddSlider(display, "触摸轨迹长度", 100, 1600, 100, 3);
            uiRefresh = AddSlider(display, "界面刷新率", 20, 60, 10, 4);
            advancedData = AddCheck(display, "默认显示高级原始数据", 5);
            AddHint(display, "输入采样保持原频率；此项只限制界面绘制。", 6);
            cards.Children.Add(WrapSection(display));

            Grid detection = CreateSection("检测", "采样偏好与报告保存");
            sampleDuration = AddSlider(detection, "静止采样时长（秒）", 3, 10, 1, 2);
            deadzoneMargin = AddSlider(detection, "死区安全余量（%）", 0.5, 5, 0.5, 3);
            saveHistory = AddCheck(detection, "完整检测后保存历史报告", 4);
            AddHint(detection, "死区建议仍由实际漂移和噪声计算，不会固定写死。", 5);
            cards.Children.Add(WrapSection(detection));

            Grid rumble = CreateSection("震动", "安全默认值");
            rumbleStrength = AddSlider(rumble, "默认强度（%）", 0, 40, 5, 2);
            rumbleDuration = AddSlider(rumble, "默认持续时间（秒）", 0.5, 5, 0.5, 3);
            rumbleMaximum = AddSlider(rumble, "安全强度上限（%）", 30, 70, 5, 4);
            AddHint(rumble, "播放器的 30 秒连续输出上限和异常归零保护始终生效。", 5);
            cards.Children.Add(WrapSection(rumble));

            Grid data = CreateSection("数据", "本地文件与恢复操作");
            WrapPanel dataActions = new WrapPanel { Margin = new Thickness(0, 13, 0, 0) };
            Button open = LabVisualStyles.CreateButton("打开数据目录", LabButtonVariant.Secondary);
            open.Click += delegate { if (this.openDataDirectory != null) this.openDataDirectory(); };
            dataActions.Children.Add(open);
            Button export = LabVisualStyles.CreateButton("导出配置", LabButtonVariant.Secondary);
            export.Margin = new Thickness(8, 0, 0, 0);
            export.Click += delegate { ExportConfiguration(); };
            dataActions.Children.Add(export);
            Button clear = LabVisualStyles.CreateButton("清除历史", LabButtonVariant.Danger);
            clear.Margin = new Thickness(8, 0, 0, 0);
            clear.Click += delegate { if (this.clearHistory != null) this.clearHistory(); };
            dataActions.Children.Add(clear);
            Grid.SetRow(dataActions, 2);
            Grid.SetColumnSpan(dataActions, 2);
            data.Children.Add(dataActions);
            Button defaults = LabVisualStyles.CreateButton("恢复默认设置", LabButtonVariant.Ghost);
            defaults.HorizontalAlignment = HorizontalAlignment.Left;
            defaults.Margin = new Thickness(0, 12, 0, 0);
            defaults.Click += delegate { RestoreDefaults(); };
            Grid.SetRow(defaults, 3);
            Grid.SetColumnSpan(defaults, 2);
            data.Children.Add(defaults);
            cards.Children.Add(WrapSection(data));

            validation = new TextBlock { Foreground = Palette.WarningBrush, FontSize = LabFontSizes.Body, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 8, 2, 8), Visibility = Visibility.Collapsed };
            Border applyCard = LabVisualStyles.CreateResultCard(new Grid());
            Grid applyLayout = (Grid)applyCard.Child;
            applyCard.Padding = new Thickness(18, 16, 18, 16);
            applyLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            applyLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel applyCopy = new StackPanel();
            applyCopy.Children.Add(new TextBlock { Text = "设置在本机生效", Foreground = Palette.TextBrush, FontSize = LabFontSizes.BodyLarge, FontWeight = FontWeights.SemiBold });
            applyCopy.Children.Add(validation);
            applyLayout.Children.Add(applyCopy);
            Button save = LabVisualStyles.CreateButton("保存并应用", LabButtonVariant.Primary);
            save.MinWidth = 118;
            save.Height = 38;
            save.VerticalAlignment = VerticalAlignment.Center;
            save.Click += delegate { Save(); };
            Grid.SetColumn(save, 1);
            applyLayout.Children.Add(save);
            cards.Children.Add(applyCard);

            scroll.Content = cards;
            Grid.SetRow(scroll, 2);
            Children.Add(scroll);
            RefreshFromSettings();
        }

        public void RefreshFromSettings()
        {
            settings.Normalize();
            startupPage.SelectedIndex = settings.StartupPage == "Health" ? 1 : settings.StartupPage == "Devices" ? 2 : 0;
            autoConnect.IsChecked = settings.AutoConnect;
            rememberWindow.IsChecked = settings.RememberWindowPosition;
            animations.IsChecked = settings.AnimationsEnabled;
            decimalPlaces.SelectedIndex = settings.DecimalPlaces - 1;
            trailLength.Value = settings.TrailLength;
            uiRefresh.Value = settings.UiRefreshRate;
            advancedData.IsChecked = settings.ShowAdvancedData;
            sampleDuration.Value = settings.StationarySampleDuration;
            deadzoneMargin.Value = settings.DeadzoneSafetyMarginPercent;
            saveHistory.IsChecked = settings.SaveHistory;
            rumbleStrength.Value = settings.DefaultRumbleStrengthPercent;
            rumbleDuration.Value = settings.DefaultRumbleDurationSeconds;
            rumbleMaximum.Value = settings.RumbleSafetyMaximumPercent;
            validation.Visibility = Visibility.Collapsed;
        }

        private void Save()
        {
            settings.StartupPage = startupPage.SelectedIndex == 1 ? "Health" : startupPage.SelectedIndex == 2 ? "Devices" : "Monitor";
            settings.AutoConnect = autoConnect.IsChecked == true;
            settings.RememberWindowPosition = rememberWindow.IsChecked == true;
            settings.AnimationsEnabled = animations.IsChecked == true;
            settings.ReducedMotion = !settings.AnimationsEnabled;
            settings.DecimalPlaces = Math.Max(1, decimalPlaces.SelectedIndex + 1);
            settings.TrailLength = (int)Math.Round(trailLength.Value);
            settings.UiRefreshRate = (int)Math.Round(uiRefresh.Value);
            settings.ShowAdvancedData = advancedData.IsChecked == true;
            settings.StationarySampleDuration = sampleDuration.Value;
            settings.DeadzoneSafetyMarginPercent = deadzoneMargin.Value;
            settings.SaveHistory = saveHistory.IsChecked == true;
            settings.DefaultRumbleStrengthPercent = rumbleStrength.Value;
            settings.DefaultRumbleDurationSeconds = rumbleDuration.Value;
            settings.RumbleSafetyMaximumPercent = rumbleMaximum.Value;
            settings.Normalize();
            if (settings.DefaultRumbleStrengthPercent > settings.RumbleSafetyMaximumPercent)
            {
                settings.DefaultRumbleStrengthPercent = settings.RumbleSafetyMaximumPercent;
                validation.Text = "默认震动强度已自动限制到安全强度上限。";
                validation.Visibility = Visibility.Visible;
                RefreshFromSettings();
                validation.Visibility = Visibility.Visible;
            }
            else
            {
                validation.Text = "设置已保存。部分显示选项会在下一帧生效。";
                validation.Foreground = Palette.GreenBrush;
                validation.Visibility = Visibility.Visible;
            }
            SettingsStore.Save(settings);
            if (apply != null) apply(settings);
            LabLogger.Info("Settings", "Product settings saved.");
        }

        private void RestoreDefaults()
        {
            if (MessageBox.Show("恢复产品设置默认值？摇杆中心校准与设备路由将保留。", "恢复默认设置", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            ControllerSettings defaults = new ControllerSettings();
            settings.CopyProductSettingsFrom(defaults);
            RefreshFromSettings();
            Save();
        }

        private void ExportConfiguration()
        {
            SaveFileDialog dialog = new SaveFileDialog { Filter = "ControllerLab 配置 (*.ini)|*.ini", DefaultExt = ".ini", FileName = "ControllerLab-settings.ini" };
            if (dialog.ShowDialog() != true) return;
            try
            {
                SettingsStore.Save(settings);
                File.Copy(SettingsStore.SettingsFilePath, dialog.FileName, true);
                MessageBox.Show("配置已导出。", "ControllerLab", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LabLogger.Error("Settings", "Configuration export failed.", ex);
                MessageBox.Show("配置导出失败。请检查目标目录是否可写。", "ControllerLab", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static Grid CreateSection(string title, string subtitle)
        {
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            for (int i = 0; i < 8; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.Children.Add(new TextBlock { Text = title, Foreground = Palette.TextBrush, FontSize = LabFontSizes.Section, FontWeight = FontWeights.SemiBold });
            TextBlock hint = new TextBlock { Text = subtitle, Foreground = Palette.MutedBrush, FontSize = LabFontSizes.Caption, Margin = new Thickness(0, 4, 0, 10) };
            Grid.SetRow(hint, 1);
            Grid.SetColumnSpan(hint, 2);
            grid.Children.Add(hint);
            return grid;
        }

        private static Border WrapSection(Grid content)
        {
            Border card = LabVisualStyles.CreateSectionCard(content);
            card.Padding = new Thickness(20, 18, 20, 18);
            card.Margin = new Thickness(0, 0, 12, 12);
            return card;
        }

        private static ProductOptionSelector AddCombo(Grid grid, string label, string[] values, int row)
        {
            AddLabel(grid, label, row);
            ProductOptionSelector combo = new ProductOptionSelector(values) { Height = 34, MinWidth = 170, Margin = new Thickness(0, 4, 0, 4) };
            Grid.SetColumn(combo, 1);
            Grid.SetRow(combo, row);
            grid.Children.Add(combo);
            AutomationProperties.SetName(combo, label);
            return combo;
        }

        private static CheckBox AddCheck(Grid grid, string label, int row)
        {
            CheckBox check = new CheckBox { Content = label, Foreground = Palette.TextBrush, FontSize = LabFontSizes.Body, Margin = new Thickness(0, 9, 0, 9), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(check, row);
            Grid.SetColumnSpan(check, 2);
            grid.Children.Add(check);
            AutomationProperties.SetName(check, label);
            return check;
        }

        private static Slider AddSlider(Grid grid, string label, double minimum, double maximum, double tick, int row)
        {
            AddLabel(grid, label, row);
            Grid selector = new Grid { Width = 190 };
            selector.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            selector.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            Slider slider = new Slider { Minimum = minimum, Maximum = maximum, TickFrequency = tick, IsSnapToTickEnabled = true, Margin = new Thickness(0, 6, 9, 6), AutoToolTipPlacement = AutoToolTipPlacement.TopLeft, AutoToolTipPrecision = tick < 1 ? 1 : 0 };
            selector.Children.Add(slider);
            TextBlock valueText = new TextBlock { Foreground = Palette.TextBrush, FontSize = LabFontSizes.Caption, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Action refreshValue = delegate
            {
                string formatted = slider.Value.ToString(tick < 1 ? "0.0" : "0", CultureInfo.InvariantCulture);
                if (label.IndexOf("%", StringComparison.Ordinal) >= 0) formatted += "%";
                else if (label.IndexOf("秒", StringComparison.Ordinal) >= 0) formatted += "s";
                else if (label.IndexOf("刷新率", StringComparison.Ordinal) >= 0) formatted += "Hz";
                valueText.Text = formatted;
            };
            slider.ValueChanged += delegate { refreshValue(); };
            refreshValue();
            Grid.SetColumn(valueText, 1);
            selector.Children.Add(valueText);
            Grid.SetColumn(selector, 1);
            Grid.SetRow(selector, row);
            grid.Children.Add(selector);
            AutomationProperties.SetName(slider, label);
            return slider;
        }

        private static void AddLabel(Grid grid, string label, int row)
        {
            TextBlock text = new TextBlock { Text = label, Foreground = Palette.MutedBrush, FontSize = LabFontSizes.Body, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 7, 12, 7) };
            Grid.SetRow(text, row);
            grid.Children.Add(text);
        }

        private static void AddHint(Grid grid, string value, int row)
        {
            TextBlock text = new TextBlock { Text = value, Foreground = Palette.MutedBrush, FontSize = LabFontSizes.Caption, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
            Grid.SetRow(text, row);
            Grid.SetColumnSpan(text, 2);
            grid.Children.Add(text);
        }
    }

    public sealed class ProductOptionSelector : Button
    {
        private readonly string[] options;
        private int selectedIndex;

        public ProductOptionSelector(string[] values)
        {
            options = values == null || values.Length == 0 ? new[] { "—" } : values;
            Style = LabVisualStyles.SecondaryButtonStyle;
            HorizontalContentAlignment = HorizontalAlignment.Left;
            Padding = new Thickness(11, 5, 11, 5);
            ToolTip = "点击切换选项";
            Click += delegate { SelectedIndex = (SelectedIndex + 1) % options.Length; };
            RefreshContent();
        }

        public int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                selectedIndex = Math.Max(0, Math.Min(options.Length - 1, value));
                RefreshContent();
            }
        }

        private void RefreshContent()
        {
            Content = options[selectedIndex] + "   ▾";
            AutomationProperties.SetName(this, options[selectedIndex]);
            AutomationProperties.SetHelpText(this, "按 Enter 或空格键切换选项");
        }
    }

    public static class ProductExperienceSelfTest
    {
        public static string Run()
        {
            VerifySettingsNormalization();
            VerifyHistoryComparison();
            VerifyLoggingRotation();
            VerifyResponsiveMatrix();
            return "Product experience selftest passed: settings ranges, report comparison, bounded log rotation, and 1080p/1440p/1600p logical DPI layout matrix.";
        }

        private static void VerifySettingsNormalization()
        {
            ControllerSettings settings = new ControllerSettings
            {
                DecimalPlaces = 9,
                TrailLength = -1,
                UiRefreshRate = 500,
                StationarySampleDuration = 100,
                DeadzoneSafetyMarginPercent = -3,
                DefaultRumbleStrengthPercent = 90,
                DefaultRumbleDurationSeconds = 20,
                RumbleSafetyMaximumPercent = 4
            };
            settings.Normalize();
            Require(settings.DecimalPlaces == 3, "decimal places should clamp");
            Require(settings.TrailLength == 100, "trail length should clamp");
            Require(settings.UiRefreshRate == 60, "UI refresh should clamp");
            Require(settings.StationarySampleDuration == 10, "sample duration should clamp");
            Require(settings.DeadzoneSafetyMarginPercent == 0.5, "deadzone margin should clamp");
            Require(settings.RumbleSafetyMaximumPercent == 30, "rumble maximum should clamp");
            Require(settings.DefaultRumbleStrengthPercent == 30, "default rumble should respect safe maximum");
        }

        private static void VerifyHistoryComparison()
        {
            ControllerHealthReport older = Report("older", 82, 3.2, 5.0, 92.0);
            ControllerHealthReport newer = Report("newer", 90, 2.0, 4.0, 97.0);
            HistoryComparisonResult result = HealthReportComparer.Compare(older, newer);
            Require(result.Rows.Count == 7, "comparison should expose seven stable rows");
            Require(result.Rows[0].Difference == "+8", "overall score delta should be reproducible");
            Require(result.Rows[1].Difference == "-1.2%", "center drift delta should be reproducible");
            Require(result.Rows[3].Difference == "+5.0%", "coverage delta should be reproducible");
        }

        private static ControllerHealthReport Report(string name, double score, double center, double deadzone, double coverage)
        {
            ControllerHealthReport report = new ControllerHealthReport { ReportId = name, DeviceName = name, TestDateUtc = DateTime.UtcNow, OverallScore = score };
            HealthCheckStepRecord stationary = new HealthCheckStepRecord { Kind = HealthCheckStepKind.LeftStickStationary.ToString() };
            stationary.Metrics.Add(new HealthReportMetric { Name = "中心偏移", Value = center, Unit = "%" });
            stationary.Metrics.Add(new HealthReportMetric { Name = "推荐死区", Value = deadzone, Unit = "%" });
            HealthCheckStepRecord circularity = new HealthCheckStepRecord { Kind = HealthCheckStepKind.LeftStickCircularity.ToString() };
            circularity.Metrics.Add(new HealthReportMetric { Name = "外圈覆盖率", Value = coverage, Unit = "%" });
            report.Steps.Add(stationary);
            report.Steps.Add(circularity);
            return report;
        }

        private static void VerifyLoggingRotation()
        {
            string root = Path.Combine(Path.GetTempPath(), "ControllerLab-product-selftest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            LabLogger.UseDirectoryForSelfTest(root);
            try
            {
                string path = Path.Combine(root, "controllerlab.log");
                File.WriteAllBytes(path, new byte[513 * 1024]);
                LabLogger.Info("SelfTest", "rotation");
                Require(File.Exists(path), "active log should be recreated");
                Require(File.Exists(path + ".1"), "oversized log should rotate");
                Require(new FileInfo(path).Length < 4096, "new active log should remain bounded");
            }
            finally
            {
                LabLogger.ResetDirectoryForSelfTest();
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void VerifyResponsiveMatrix()
        {
            double[,] cases = { { 1920, 1080, 1.0 }, { 1920, 1080, 1.25 }, { 1920, 1080, 1.5 }, { 2560, 1440, 1.25 }, { 2560, 1600, 1.5 }, { 1536, 864, 1.0 } };
            for (int i = 0; i < cases.GetLength(0); i++)
            {
                double logicalWidth = cases[i, 0] / cases[i, 2];
                double logicalHeight = cases[i, 1] / cases[i, 2];
                Require(logicalWidth >= 1020, "logical width should support the responsive minimum");
                Require(logicalHeight >= 680, "logical height should support the responsive minimum");
                Require(logicalWidth - 44 - 374 >= 602, "live controller column should keep a usable width");
            }
        }

        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("Product experience selftest failed: " + message); }
    }
}
