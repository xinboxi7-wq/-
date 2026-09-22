// ReportExporter
//
// Extracted verbatim from ControllerLab.cs (lines 5541-5750) on 2026-09-22
// as part of the ControllerLab structural split (batch 3).
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
    public static class ReportExporter
    {
        public static string BuildJson(ControllerReport report)
        {
            if (report == null) throw new ArgumentNullException("report");
            StringBuilder builder = new StringBuilder();
            builder.Append("{\n");
            AppendString(builder, "generatedAt", report.GeneratedAt, true);
            AppendString(builder, "controller", report.Controller, true);
            AppendBoolean(builder, "connected", report.Connected, true);
            AppendNumber(builder, "displayHz", report.DisplayHz, true);
            AppendNumber(builder, "samplingHz", report.SamplingHz, true);
            AppendBoolean(builder, "diagnosticReady", report.DiagnosticReady, true);
            AppendInteger(builder, "diagnosticScore", report.DiagnosticScore, true);
            AppendString(builder, "diagnosticStatus", report.DiagnosticStatus, true);
            AppendString(builder, "diagnosticDetail", report.DiagnosticDetail, true);
            AppendInteger(builder, "diagnosticCoverage", report.DiagnosticCoverage, true);
            AppendNumber(builder, "centerLeft", report.CenterLeft, true);
            AppendNumber(builder, "centerRight", report.CenterRight, true);
            AppendString(builder, "guidedStatus", report.GuidedStatus, true);
            AppendString(builder, "guidedStage", report.GuidedStage, true);
            AppendStringArray(builder, "guidedResults", report.GuidedResults, true);
            AppendNumber(builder, "leftX", report.LeftX, true);
            AppendNumber(builder, "leftY", report.LeftY, true);
            AppendNumber(builder, "rightX", report.RightX, true);
            AppendNumber(builder, "rightY", report.RightY, true);
            AppendNumber(builder, "leftTrigger", report.LeftTrigger, true);
            AppendNumber(builder, "rightTrigger", report.RightTrigger, true);
            AppendNumber(builder, "leftTriggerPeak", report.LeftTriggerPeak, true);
            AppendNumber(builder, "rightTriggerPeak", report.RightTriggerPeak, true);
            AppendString(builder, "buttons", report.ButtonsHex, true);
            AppendNumber(builder, "leftDeadzone", report.LeftDeadzone, true);
            AppendNumber(builder, "rightDeadzone", report.RightDeadzone, true);
            AppendNumber(builder, "offsetLX", report.OffsetLX, true);
            AppendNumber(builder, "offsetLY", report.OffsetLY, true);
            AppendNumber(builder, "offsetRX", report.OffsetRX, true);
            AppendNumber(builder, "offsetRY", report.OffsetRY, true);
            AppendBoolean(builder, "reducedMotion", report.ReducedMotion, true);
            AppendBoolean(builder, "historyPaused", report.HistoryPaused, true);
            AppendNumberArray(builder, "leftTriggerHistory", report.LeftTriggerHistory, true);
            AppendNumberArray(builder, "rightTriggerHistory", report.RightTriggerHistory, false);
            builder.Append("}\n");
            return builder.ToString();
        }

        public static string BuildCsv(ControllerReport report)
        {
            if (report == null) throw new ArgumentNullException("report");
            StringBuilder builder = new StringBuilder();
            builder.Append("项目,值\r\n");
            CsvRow(builder, "生成时间", report.GeneratedAt);
            CsvRow(builder, "设备", report.Controller);
            CsvRow(builder, "连接状态", report.Connected ? "已连接" : "未连接");
            CsvRow(builder, "显示刷新率 Hz", Number(report.DisplayHz));
            CsvRow(builder, "输入采样率 Hz", Number(report.SamplingHz));
            CsvRow(builder, "基础健康已就绪", report.DiagnosticReady ? "是" : "否");
            CsvRow(builder, "基础健康分", report.DiagnosticScore.ToString(CultureInfo.InvariantCulture));
            CsvRow(builder, "基础健康状态", report.DiagnosticStatus);
            CsvRow(builder, "基础健康详情", report.DiagnosticDetail);
            CsvRow(builder, "操作覆盖", report.DiagnosticCoverage.ToString(CultureInfo.InvariantCulture) + "/6");
            CsvRow(builder, "左摇杆中心幅度", Number(report.CenterLeft));
            CsvRow(builder, "右摇杆中心幅度", Number(report.CenterRight));
            CsvRow(builder, "自动体检", report.GuidedStatus);
            CsvRow(builder, "自动体检阶段", report.GuidedStage);
            string[] resultNames = { "连接与采样", "中心基线", "左摇杆行程", "右摇杆行程", "LT / RT 扳机", "14 个按键" };
            for (int i = 0; i < resultNames.Length; i++)
            {
                string value = report.GuidedResults != null && i < report.GuidedResults.Length ? report.GuidedResults[i] : "未运行";
                CsvRow(builder, "体检 - " + resultNames[i], value);
            }
            CsvRow(builder, "左摇杆 X", Number(report.LeftX));
            CsvRow(builder, "左摇杆 Y", Number(report.LeftY));
            CsvRow(builder, "右摇杆 X", Number(report.RightX));
            CsvRow(builder, "右摇杆 Y", Number(report.RightY));
            CsvRow(builder, "LT 当前", Number(report.LeftTrigger));
            CsvRow(builder, "RT 当前", Number(report.RightTrigger));
            CsvRow(builder, "LT 近 5 秒峰值", Number(report.LeftTriggerPeak));
            CsvRow(builder, "RT 近 5 秒峰值", Number(report.RightTriggerPeak));
            CsvRow(builder, "按键位掩码", report.ButtonsHex);
            CsvRow(builder, "左参考死区", Number(report.LeftDeadzone));
            CsvRow(builder, "右参考死区", Number(report.RightDeadzone));
            CsvRow(builder, "中心偏移 LX", Number(report.OffsetLX));
            CsvRow(builder, "中心偏移 LY", Number(report.OffsetLY));
            CsvRow(builder, "中心偏移 RX", Number(report.OffsetRX));
            CsvRow(builder, "中心偏移 RY", Number(report.OffsetRY));
            CsvRow(builder, "减少动态效果", report.ReducedMotion ? "是" : "否");
            CsvRow(builder, "历史曲线暂停", report.HistoryPaused ? "是" : "否");
            CsvRow(builder, "LT 历史", JoinNumbers(report.LeftTriggerHistory));
            CsvRow(builder, "RT 历史", JoinNumbers(report.RightTriggerHistory));
            return builder.ToString();
        }

        private static void AppendName(StringBuilder builder, string name)
        {
            builder.Append("  \"").Append(EscapeJson(name)).Append("\": ");
        }

        private static void AppendString(StringBuilder builder, string name, string value, bool comma)
        {
            AppendName(builder, name);
            builder.Append('"').Append(EscapeJson(value ?? string.Empty)).Append('"');
            EndProperty(builder, comma);
        }

        private static void AppendBoolean(StringBuilder builder, string name, bool value, bool comma)
        {
            AppendName(builder, name);
            builder.Append(value ? "true" : "false");
            EndProperty(builder, comma);
        }

        private static void AppendInteger(StringBuilder builder, string name, int value, bool comma)
        {
            AppendName(builder, name);
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
            EndProperty(builder, comma);
        }

        private static void AppendNumber(StringBuilder builder, string name, double value, bool comma)
        {
            AppendName(builder, name);
            builder.Append(Number(value));
            EndProperty(builder, comma);
        }

        private static void AppendStringArray(StringBuilder builder, string name, string[] values, bool comma)
        {
            AppendName(builder, name);
            builder.Append('[');
            if (values != null)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    if (i > 0) builder.Append(", ");
                    builder.Append('"').Append(EscapeJson(values[i] ?? string.Empty)).Append('"');
                }
            }
            builder.Append(']');
            EndProperty(builder, comma);
        }

        private static void AppendNumberArray(StringBuilder builder, string name, double[] values, bool comma)
        {
            AppendName(builder, name);
            builder.Append('[');
            if (values != null)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    if (i > 0) builder.Append(", ");
                    builder.Append(Number(values[i]));
                }
            }
            builder.Append(']');
            EndProperty(builder, comma);
        }

        private static void EndProperty(StringBuilder builder, bool comma)
        {
            if (comma) builder.Append(',');
            builder.Append('\n');
        }

        private static string Number(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "0";
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static string JoinNumbers(double[] values)
        {
            if (values == null || values.Length == 0) return string.Empty;
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) builder.Append(';');
                builder.Append(Number(values[i]));
            }
            return builder.ToString();
        }

        private static void CsvRow(StringBuilder builder, string name, string value)
        {
            builder.Append(EscapeCsv(name)).Append(',').Append(EscapeCsv(value ?? string.Empty)).Append("\r\n");
        }

        private static string EscapeCsv(string value)
        {
            bool quote = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
            string escaped = value.Replace("\"", "\"\"");
            return quote ? "\"" + escaped + "\"" : escaped;
        }

        private static string EscapeJson(string value)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\') builder.Append("\\\\");
                else if (c == '"') builder.Append("\\\"");
                else if (c == '\n') builder.Append("\\n");
                else if (c == '\r') builder.Append("\\r");
                else if (c == '\t') builder.Append("\\t");
                else if (c < 32) builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else builder.Append(c);
            }
            return builder.ToString();
        }
    }
}
