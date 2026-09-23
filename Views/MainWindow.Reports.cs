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
    public sealed partial class MainWindow
    {
        private ControllerReport BuildCurrentReport()
        {
            string guidedStatus = "未运行";
            if (guidedTest.IsComplete) guidedStatus = guidedTest.HasSkipped ? "部分完成" : "全部通过";
            else if (guidedTest.Active) guidedStatus = "进行中";
            string[] guidedResults = new string[6];
            for (int i = 0; i < guidedResults.Length; i++) guidedResults[i] = guidedTest.ResultText(i);
            return new ControllerReport
            {
                GeneratedAt = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                Controller = demoMode ? "动态演示" : (currentState.Connected ? (currentState.Family == ControllerFamily.PlayStation ? currentState.DeviceName : string.Format(CultureInfo.InvariantCulture, "Xbox 玩家 {0}", currentState.Index + 1)) : ControllerFamilySelectionLabel()),
                Connected = currentState.Connected,
                DisplayHz = actualDisplayHz,
                SamplingHz = demoMode ? actualDisplayHz : actualSamplingHz,
                DiagnosticReady = diagnostics.IsReady,
                DiagnosticScore = diagnostics.Score,
                DiagnosticStatus = diagnostics.Status,
                DiagnosticDetail = diagnostics.Detail,
                DiagnosticCoverage = diagnostics.CoverageCount,
                CenterLeft = diagnostics.CenterLeft,
                CenterRight = diagnostics.CenterRight,
                GuidedStatus = guidedStatus,
                GuidedStage = guidedTest.StageTitle,
                GuidedResults = guidedResults,
                LeftX = currentState.LeftNormalizedX,
                LeftY = currentState.LeftNormalizedY,
                RightX = currentState.RightNormalizedX,
                RightY = currentState.RightNormalizedY,
                LeftTrigger = currentState.LeftTrigger / 255.0,
                RightTrigger = currentState.RightTrigger / 255.0,
                LeftTriggerPeak = leftTriggerChart.PeakValue,
                RightTriggerPeak = rightTriggerChart.PeakValue,
                ButtonsHex = "0x" + currentState.Buttons.ToString("X4", CultureInfo.InvariantCulture),
                LeftDeadzone = leftDeadzone.Value,
                RightDeadzone = rightDeadzone.Value,
                OffsetLX = offsetLX,
                OffsetLY = offsetLY,
                OffsetRX = offsetRX,
                OffsetRY = offsetRY,
                ReducedMotion = reducedMotion,
                HistoryPaused = historyPaused,
                LeftTriggerHistory = leftTriggerChart.GetHistorySnapshot(),
                RightTriggerHistory = rightTriggerChart.GetHistorySnapshot()
            };
        }

        private void ExportCurrentReport()
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "导出手柄检测报告",
                Filter = "JSON 报告 (*.json)|*.json|CSV 报告 (*.csv)|*.csv",
                DefaultExt = ".json",
                AddExtension = true,
                OverwritePrompt = true,
                FileName = (currentState.Family == ControllerFamily.PlayStation ? "索尼DS手柄报告_" : "Xbox手柄报告_") + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                ControllerReport report = BuildCurrentReport();
                string extension = System.IO.Path.GetExtension(dialog.FileName);
                string content = string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase)
                    ? ReportExporter.BuildCsv(report)
                    : ReportExporter.BuildJson(report);
                File.WriteAllText(dialog.FileName, content, new UTF8Encoding(true));
                footerStatus.Text = "检测报告已导出：" + System.IO.Path.GetFileName(dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "导出失败：" + ex.Message, "导出报告", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportTriggerHistory()
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "导出 LT / RT 曲线",
                Filter = "CSV 文件 (*.csv)|*.csv",
                DefaultExt = ".csv",
                AddExtension = true,
                FileName = "ControllerLab_Trigger_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                double[] left = leftTriggerChart.GetHistorySnapshot();
                double[] right = rightTriggerChart.GetHistorySnapshot();
                int count = Math.Max(left.Length, right.Length);
                StringBuilder csv = new StringBuilder();
                csv.AppendLine("sample,secondsAgo,LT,RT");
                for (int i = 0; i < count; i++)
                {
                    double secondsAgo = (count - 1 - i) * TriggerChart.SampleIntervalSeconds;
                    string lt = i < left.Length ? (left[i] * 100.0).ToString("0.###", CultureInfo.InvariantCulture) : string.Empty;
                    string rt = i < right.Length ? (right[i] * 100.0).ToString("0.###", CultureInfo.InvariantCulture) : string.Empty;
                    csv.AppendFormat(CultureInfo.InvariantCulture, "{0},{1:0.000},{2},{3}\r\n", i, secondsAgo, lt, rt);
                }
                File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(true));
                if (footerStatus != null) footerStatus.Text = "LT / RT 曲线已导出：" + System.IO.Path.GetFileName(dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "导出 LT / RT 曲线失败：" + ex.Message, "导出曲线", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}
