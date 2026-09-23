// DiagnosticEngine
//
// Extracted verbatim from ControllerLab.cs (lines 5912-6066) on 2026-09-22
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
    public sealed class DiagnosticEngine
    {
        private const int RequiredBaselineSamples = 45;
        private double centerLeft;
        private double centerRight;
        private bool hasCenterBaseline;
        private int baselineSamples;
        private double baselineLeftSum;
        private double baselineRightSum;
        private int coverageMask;
        private int score;

        public int Score { get { return score; } }
        public bool IsReady { get { return score >= 0 && hasCenterBaseline; } }
        public bool HasBaseline { get { return hasCenterBaseline; } }
        public double BaselineProgress { get { return Math.Min(1.0, baselineSamples / (double)RequiredBaselineSamples); } }
        public double CenterLeft { get { return centerLeft; } }
        public double CenterRight { get { return centerRight; } }
        public int CoverageMask { get { return coverageMask; } }
        public int CoverageCount { get { return CountBits(coverageMask); } }
        public string Status { get; private set; }
        public string Detail { get; private set; }

        public DiagnosticEngine()
        {
            Reset();
        }

        public void Reset()
        {
            centerLeft = 0;
            centerRight = 0;
            hasCenterBaseline = false;
            baselineSamples = 0;
            baselineLeftSum = 0;
            baselineRightSum = 0;
            coverageMask = 0;
            score = -1;
            Status = "等待手柄";
            Detail = "连接后自动检查中心、采样与操作覆盖";
        }

        public void UseDemoBaseline()
        {
            centerLeft = 0.004;
            centerRight = 0.004;
            hasCenterBaseline = true;
            baselineSamples = RequiredBaselineSamples;
            baselineLeftSum = centerLeft * RequiredBaselineSamples;
            baselineRightSum = centerRight * RequiredBaselineSamples;
            score = -1;
            Status = "评估中";
            Detail = "演示中心基线已准备 · 正在测量刷新率";
        }

        public void Update(InputSnapshot state, double samplingHz, double leftDeadzone, double rightDeadzone)
        {
            if (!state.Connected)
            {
                centerLeft = 0;
                centerRight = 0;
                hasCenterBaseline = false;
                baselineSamples = 0;
                baselineLeftSum = 0;
                baselineRightSum = 0;
                coverageMask = 0;
                score = -1;
                Status = "等待手柄";
                Detail = "连接后自动检查中心、采样与操作覆盖";
                return;
            }

            double leftMagnitude = Math.Min(1.0, Math.Sqrt(state.LeftNormalizedX * state.LeftNormalizedX + state.LeftNormalizedY * state.LeftNormalizedY));
            double rightMagnitude = Math.Min(1.0, Math.Sqrt(state.RightNormalizedX * state.RightNormalizedX + state.RightNormalizedY * state.RightNormalizedY));

            if (leftMagnitude > 0.82) coverageMask |= 1;
            if (rightMagnitude > 0.82) coverageMask |= 2;
            if (state.LeftTrigger > 229) coverageMask |= 4;
            if (state.RightTrigger > 229) coverageMask |= 8;
            if ((state.Buttons & 0xF000) != 0) coverageMask |= 16;
            if ((state.Buttons & 0x000F) != 0) coverageMask |= 32;

            bool stableCenter = leftMagnitude < 0.12 && rightMagnitude < 0.12 && state.LeftTrigger < 14 && state.RightTrigger < 14;
            if (!hasCenterBaseline)
            {
                if (stableCenter)
                {
                    baselineSamples++;
                    baselineLeftSum += leftMagnitude;
                    baselineRightSum += rightMagnitude;
                    if (baselineSamples >= RequiredBaselineSamples)
                    {
                        centerLeft = baselineLeftSum / baselineSamples;
                        centerRight = baselineRightSum / baselineSamples;
                        hasCenterBaseline = true;
                    }
                }
                else
                {
                    baselineSamples = 0;
                    baselineLeftSum = 0;
                    baselineRightSum = 0;
                }

                if (!hasCenterBaseline)
                {
                    score = -1;
                    Status = "评估中";
                    Detail = stableCenter
                        ? string.Format(CultureInfo.InvariantCulture, "正在建立中心基线 {0:0}% · 请保持摇杆居中", BaselineProgress * 100.0)
                        : "请松开摇杆与扳机，以建立中心基线";
                    return;
                }
            }
            else if (leftMagnitude < 0.25 && rightMagnitude < 0.25)
            {
                centerLeft = centerLeft * 0.965 + leftMagnitude * 0.035;
                centerRight = centerRight * 0.965 + rightMagnitude * 0.035;
            }

            if (samplingHz <= 0)
            {
                score = -1;
                Status = "评估中";
                Detail = "中心基线已建立 · 正在测量实际采样率";
                return;
            }

            int penalty = 0;
            if (samplingHz < 120) penalty += (int)Math.Min(20, Math.Round((120 - samplingHz) / 6.0));
            penalty += DriftPenalty(centerLeft, Math.Max(0.02, leftDeadzone));
            penalty += DriftPenalty(centerRight, Math.Max(0.02, rightDeadzone));
            score = Math.Max(0, Math.Min(100, 100 - penalty));
            Status = score >= 90 ? "状态良好" : score >= 75 ? "建议观察" : "需要检查";
            int coverage = CountBits(coverageMask);
            Detail = string.Format(CultureInfo.InvariantCulture, "操作覆盖 {0}/6 · 中心 L {1:0.000} / R {2:0.000}", coverage, centerLeft, centerRight);
        }

        private static int DriftPenalty(double magnitude, double reference)
        {
            if (magnitude <= reference) return 0;
            return (int)Math.Min(25, Math.Round((magnitude - reference) * 180.0));
        }

        private static int CountBits(int value)
        {
            int count = 0;
            while (value != 0)
            {
                count += value & 1;
                value >>= 1;
            }
            return count;
        }
    }
}
