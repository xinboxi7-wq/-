// DualSenseCalibrationWindow
//
// Extracted verbatim from ControllerLab.cs (lines 5654-5886) on 2026-09-22
// as part of the ControllerLab structural split (batch 4).
// No logic was changed.
// See docs/redesign/ControllerLab-结构拆分施工图.md
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Threading;

namespace ControllerLab
{
    public sealed class DualSenseCalibrationWindow : Window
    {
        private readonly DualSenseRegionManager manager;
        private readonly DualSenseCalibrationSurface surface;
        private readonly ListBox regionsList;
        private readonly TextBlock selectedText;
        private readonly TextBlock coordinatesText;
        private readonly TextBlock statusText;
        private readonly Stack<DualSenseCalibrationSnapshot> undo = new Stack<DualSenseCalibrationSnapshot>();
        private readonly Stack<DualSenseCalibrationSnapshot> redo = new Stack<DualSenseCalibrationSnapshot>();
        private DualSenseCalibrationSnapshot committedSnapshot;
        private bool snapshotPending;

        public string StatusMessage { get; private set; }

        public DualSenseCalibrationWindow(DualSenseRegionManager value, ImageSource photo)
        {
            manager = value;
            committedSnapshot = manager.CreateSnapshot();
            Title = "DS5 轮廓校准";
            Width = 1480;
            Height = 920;
            MinWidth = 1120;
            MinHeight = 700;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Palette.WindowBrush;
            Foreground = Palette.TextBrush;
            FontFamily = new FontFamily("Microsoft YaHei UI");
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            Grid root = new Grid { Margin = new Thickness(14) };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(212) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(282) });

            regionsList = new ListBox { Background = Palette.SurfaceBrush, BorderBrush = Palette.BorderBrush, Foreground = Palette.TextBrush, Margin = new Thickness(0, 0, 10, 0) };
            PopulateRegionList();
            regionsList.SelectionChanged += delegate { surface.Select(regionsList.SelectedItem as string); RefreshSelected(); };
            root.Children.Add(regionsList);

            Border stageBorder = new Border { Background = Palette.WindowBrush, BorderBrush = Palette.BorderBrush, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 10, 0) };
            surface = new DualSenseCalibrationSurface(manager, photo);
            surface.RegionSelected += OnSurfaceRegionSelected;
            surface.EditStarted += PushUndo;
            surface.CoordinatesChanged += delegate(string valueText) { coordinatesText.Text = valueText; };
            stageBorder.Child = surface;
            Grid.SetColumn(stageBorder, 1);
            root.Children.Add(stageBorder);

            StackPanel panel = new StackPanel { Background = Palette.SurfaceBrush, Margin = new Thickness(0), Orientation = Orientation.Vertical };
            Border panelBorder = new Border { Background = Palette.SurfaceBrush, BorderBrush = Palette.BorderBrush, BorderThickness = new Thickness(1), Padding = new Thickness(14), Child = panel };
            Grid.SetColumn(panelBorder, 2);
            root.Children.Add(panelBorder);
            selectedText = Text("未选择区域", 14, Palette.TextBrush, true);
            coordinatesText = Text("X 0, Y 0", 11, Palette.MutedBrush, false);
            statusText = Text(manager.LastLoadMessage, 11, Palette.MutedBrush, false);
            panel.Children.Add(selectedText);
            panel.Children.Add(coordinatesText);
            panel.Children.Add(statusText);
            panel.Children.Add(Separator());

            AddButton(panel, "撤销", delegate { Undo(); });
            AddButton(panel, "重做", delegate { Redo(); });
            AddButton(panel, "恢复当前区域默认值", delegate { if (surface.SelectedId != null) { PushUndo(); manager.ResetRegion(surface.SelectedId); surface.InvalidateVisual(); RefreshSelected(); } });
            panel.Children.Add(Separator());
            AddSlider(panel, "底图透明度", 0.15, 1.0, surface.BackgroundOpacity, delegate(double v) { surface.BackgroundOpacity = v; surface.InvalidateVisual(); });
            AddSlider(panel, "区域透明度", 0.15, 1.0, surface.OverlayOpacity, delegate(double v) { surface.OverlayOpacity = v; surface.InvalidateVisual(); });
            CheckBox outlineView = new CheckBox { Content = "实体轮廓校准视图（无 Glow / 1px）", Foreground = Palette.WarningBrush, Margin = new Thickness(0, 2, 0, 0), ToolTip = "透明填充、1 个屏幕像素描边；先用此视图贴合实体边缘，再检查正式光效" };
            outlineView.Checked += delegate { surface.OutlineCalibrationView = true; surface.InvalidateVisual(); };
            outlineView.Unchecked += delegate { surface.OutlineCalibrationView = false; surface.InvalidateVisual(); };
            panel.Children.Add(outlineView);
            CheckBox lockImage = new CheckBox { Content = "锁定底图", IsChecked = true, Foreground = Palette.TextBrush, Margin = new Thickness(0, 8, 0, 0) };
            lockImage.Checked += delegate { surface.ImageLocked = true; };
            lockImage.Unchecked += delegate { surface.ImageLocked = false; };
            panel.Children.Add(lockImage);
            panel.Children.Add(Separator());
            AddStyleSliders(panel);
            panel.Children.Add(Separator());
            AddButton(panel, "导出完整 Geometry JSON", ExportDocument);
            AddButton(panel, "导入完整 Geometry JSON", ImportDocument);
            AddButton(panel, "重新加载默认与用户覆盖", delegate { Reload(); });
            AddButton(panel, "保存用户校准覆盖", delegate { Save(); });
            Button close = new Button { Content = "关闭", Height = 34, Margin = new Thickness(0, 7, 0, 0), Background = Palette.Surface2Brush, Foreground = Palette.TextBrush, BorderBrush = Palette.BorderBrush };
            close.Click += delegate { Close(); };
            panel.Children.Add(close);

            Content = root;
            // The editor is transactional: a successful save/reload advances the committed snapshot;
            // closing after an unsaved drag restores that last committed state in the live monitor.
            Closed += delegate { if (!ReferenceEquals(committedSnapshot, null)) manager.RestoreSnapshot(committedSnapshot); };
            Loaded += delegate { surface.Focus(); };
        }

        private void PopulateRegionList()
        {
            regionsList.Items.Clear();
            if (manager.Document.Regions != null)
            {
                for (int i = 0; i < manager.Document.Regions.Count; i++) regionsList.Items.Add(manager.Document.Regions[i].Id);
            }
            if (manager.Document.MotionRanges != null)
            {
                for (int i = 0; i < manager.Document.MotionRanges.Count; i++) regionsList.Items.Add(manager.Document.MotionRanges[i].Id);
            }
        }

        private void OnSurfaceRegionSelected(string id)
        {
            regionsList.SelectedItem = id;
            RefreshSelected();
        }

        private void RefreshSelected()
        {
            string id = surface.SelectedId;
            if (string.IsNullOrEmpty(id)) { selectedText.Text = "未选择区域"; return; }
            List<DualSenseCalibrationHandle> handles = manager.GetHandles(id);
            selectedText.Text = id + " · " + handles.Count.ToString(CultureInfo.InvariantCulture) + " 个可编辑锚点";
        }

        private void PushUndo()
        {
            if (snapshotPending) return;
            undo.Push(manager.CreateSnapshot());
            redo.Clear();
            snapshotPending = true;
            Dispatcher.BeginInvoke(new Action(delegate { snapshotPending = false; }), DispatcherPriority.Background);
        }

        private void Undo()
        {
            if (undo.Count == 0) return;
            redo.Push(manager.CreateSnapshot());
            manager.RestoreSnapshot(undo.Pop());
            PopulateRegionList();
            surface.InvalidateVisual();
            RefreshSelected();
            SetStatus("已撤销上一步 Geometry 编辑。");
        }

        private void Redo()
        {
            if (redo.Count == 0) return;
            undo.Push(manager.CreateSnapshot());
            manager.RestoreSnapshot(redo.Pop());
            PopulateRegionList();
            surface.InvalidateVisual();
            RefreshSelected();
            SetStatus("已重做 Geometry 编辑。");
        }

        private void Save()
        {
            string message;
            if (manager.SaveUserOverride(out message)) committedSnapshot = manager.CreateSnapshot();
            SetStatus(message);
        }

        private void Reload()
        {
            manager.Reload();
            committedSnapshot = manager.CreateSnapshot();
            undo.Clear();
            redo.Clear();
            PopulateRegionList();
            surface.Select(null);
            surface.InvalidateVisual();
            SetStatus(manager.LastLoadMessage);
        }

        private void ExportDocument(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog { Filter = "JSON 文件|*.json", FileName = "dualSenseRegions-export.json" };
            if (dialog.ShowDialog(this) != true) return;
            string message;
            manager.ExportDocument(dialog.FileName, out message);
            SetStatus(message);
        }

        private void ImportDocument(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog { Filter = "JSON 文件|*.json" };
            if (dialog.ShowDialog(this) != true) return;
            PushUndo();
            string message;
            manager.ImportDocument(dialog.FileName, out message);
            PopulateRegionList();
            surface.InvalidateVisual();
            RefreshSelected();
            SetStatus(message);
        }

        private void AddStyleSliders(StackPanel panel)
        {
            DualSenseVisualStyleDefinition active = manager.FindStyle("active");
            if (active == null) return;
            AddSlider(panel, "描边宽度", 0.5, 3.0, active.StrokePixels, delegate(double v) { PushUndo(); active.StrokePixels = v; manager.MarkStylesModified(); surface.InvalidateVisual(); });
            AddSlider(panel, "发光强度", 0.0, 1.0, active.GlowOpacity, delegate(double v) { PushUndo(); active.GlowOpacity = v; manager.MarkStylesModified(); surface.InvalidateVisual(); });
        }

        private void AddSlider(StackPanel panel, string label, double min, double max, double value, Action<double> changed)
        {
            panel.Children.Add(Text(label, 11, Palette.MutedBrush, false));
            Slider slider = new Slider { Minimum = min, Maximum = max, Value = value, Margin = new Thickness(0, 2, 0, 6) };
            slider.ValueChanged += delegate(object sender, RoutedPropertyChangedEventArgs<double> e) { changed(e.NewValue); };
            panel.Children.Add(slider);
        }

        private void AddButton(StackPanel panel, string content, RoutedEventHandler click)
        {
            Button button = new Button { Content = content, Height = 30, Margin = new Thickness(0, 4, 0, 0), Background = Palette.Surface2Brush, Foreground = Palette.TextBrush, BorderBrush = Palette.BorderBrush };
            button.Click += click;
            panel.Children.Add(button);
        }

        private static TextBlock Text(string value, double size, Brush brush, bool bold)
        {
            return new TextBlock { Text = value, FontSize = size, Foreground = brush, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 5) };
        }

        private static Border Separator()
        {
            return new Border { Height = 1, Background = Palette.BorderBrush, Margin = new Thickness(0, 10, 0, 8) };
        }

        private void SetStatus(string value)
        {
            StatusMessage = value;
            statusText.Text = value;
        }

    }
}
