// GuidedTestEngine
//
// Extracted verbatim from ControllerLab.cs (lines 6069-6345) on 2026-09-22
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
    public sealed class GuidedTestEngine
    {
        public static readonly int[] ButtonMasks =
        {
            0x0001, 0x0002, 0x0004, 0x0008,
            0x0010, 0x0020, 0x0040, 0x0080,
            0x0100, 0x0200,
            0x1000, 0x2000, 0x4000, 0x8000
        };

        public static readonly string[] ButtonNames =
        {
            "上", "下", "左", "右",
            "菜单", "视图", "LS", "RS",
            "LB", "RB", "A", "B", "X", "Y"
        };

        private const int AllButtonsMask = 0xF3FF;
        private DateTime lastUpdate;
        private double centerStableSeconds;
        private int leftDirections;
        private int rightDirections;
        private int triggerMask;
        private int seenButtons;
        private bool connectionPassed;
        private bool centerPassed;
        private bool leftPassed;
        private bool rightPassed;
        private bool triggersPassed;
        private bool buttonsPassed;
        private bool centerSkipped;
        private bool leftSkipped;
        private bool rightSkipped;
        private bool triggersSkipped;
        private bool buttonsSkipped;

        public GuidedStage Stage { get; private set; }
        public bool Active { get { return Stage != GuidedStage.Idle && Stage != GuidedStage.Complete; } }
        public bool IsComplete { get { return Stage == GuidedStage.Complete; } }
        public bool ConnectionPassed { get { return connectionPassed; } }
        public bool CenterPassed { get { return centerPassed; } }
        public bool LeftPassed { get { return leftPassed; } }
        public bool RightPassed { get { return rightPassed; } }
        public bool TriggersPassed { get { return triggersPassed; } }
        public bool ButtonsPassed { get { return buttonsPassed; } }
        public double CenterStableSeconds { get { return centerStableSeconds; } }
        public int LeftDirections { get { return leftDirections; } }
        public int RightDirections { get { return rightDirections; } }
        public int TriggerMask { get { return triggerMask; } }
        public int SeenButtons { get { return seenButtons; } }
        public int ButtonCount { get { return CountBits(seenButtons & AllButtonsMask); } }
        public bool HasSkipped { get { return centerSkipped || leftSkipped || rightSkipped || triggersSkipped || buttonsSkipped; } }

        public GuidedTestEngine()
        {
            Stage = GuidedStage.Idle;
        }

        public void Begin()
        {
            lastUpdate = DateTime.UtcNow;
            centerStableSeconds = 0;
            leftDirections = 0;
            rightDirections = 0;
            triggerMask = 0;
            seenButtons = 0;
            connectionPassed = false;
            centerPassed = false;
            leftPassed = false;
            rightPassed = false;
            triggersPassed = false;
            buttonsPassed = false;
            centerSkipped = false;
            leftSkipped = false;
            rightSkipped = false;
            triggersSkipped = false;
            buttonsSkipped = false;
            Stage = GuidedStage.Center;
        }

        public void Update(InputSnapshot state, double samplingHz)
        {
            if (!Active) return;
            DateTime now = DateTime.UtcNow;
            double elapsed = Math.Max(0, Math.Min(0.1, (now - lastUpdate).TotalSeconds));
            lastUpdate = now;
            if (!state.Connected)
            {
                centerStableSeconds = 0;
                return;
            }

            if (samplingHz >= 60) connectionPassed = true;
            double lx = state.LeftNormalizedX;
            double ly = state.LeftNormalizedY;
            double rx = state.RightNormalizedX;
            double ry = state.RightNormalizedY;
            if (Stage == GuidedStage.LeftStick)
            {
                if (lx > 0.75) leftDirections |= 1;
                if (lx < -0.75) leftDirections |= 2;
                if (ly > 0.75) leftDirections |= 4;
                if (ly < -0.75) leftDirections |= 8;
            }
            if (Stage == GuidedStage.RightStick)
            {
                if (rx > 0.75) rightDirections |= 1;
                if (rx < -0.75) rightDirections |= 2;
                if (ry > 0.75) rightDirections |= 4;
                if (ry < -0.75) rightDirections |= 8;
            }
            if (Stage == GuidedStage.Triggers)
            {
                if (state.LeftTrigger >= 230) triggerMask |= 1;
                if (state.RightTrigger >= 230) triggerMask |= 2;
            }
            if (Stage == GuidedStage.Buttons) seenButtons |= state.Buttons & AllButtonsMask;

            if (Stage == GuidedStage.Center)
            {
                double leftMagnitude = Math.Sqrt(lx * lx + ly * ly);
                double rightMagnitude = Math.Sqrt(rx * rx + ry * ry);
                bool stable = leftMagnitude < 0.12 && rightMagnitude < 0.12 && state.LeftTrigger < 14 && state.RightTrigger < 14 && state.Buttons == 0;
                centerStableSeconds = stable ? centerStableSeconds + elapsed : 0;
                if (centerStableSeconds >= 2.0)
                {
                    centerPassed = true;
                    Stage = GuidedStage.LeftStick;
                }
            }
            else if (Stage == GuidedStage.LeftStick && leftDirections == 15)
            {
                leftPassed = true;
                Stage = GuidedStage.RightStick;
            }
            else if (Stage == GuidedStage.RightStick && rightDirections == 15)
            {
                rightPassed = true;
                Stage = GuidedStage.Triggers;
            }
            else if (Stage == GuidedStage.Triggers && triggerMask == 3)
            {
                triggersPassed = true;
                Stage = GuidedStage.Buttons;
            }
            else if (Stage == GuidedStage.Buttons && (seenButtons & AllButtonsMask) == AllButtonsMask)
            {
                buttonsPassed = true;
                Stage = GuidedStage.Complete;
            }
        }

        public void SkipCurrent()
        {
            if (Stage == GuidedStage.Center)
            {
                centerSkipped = true;
                Stage = GuidedStage.LeftStick;
            }
            else if (Stage == GuidedStage.LeftStick)
            {
                leftSkipped = true;
                Stage = GuidedStage.RightStick;
            }
            else if (Stage == GuidedStage.RightStick)
            {
                rightSkipped = true;
                Stage = GuidedStage.Triggers;
            }
            else if (Stage == GuidedStage.Triggers)
            {
                triggersSkipped = true;
                Stage = GuidedStage.Buttons;
            }
            else if (Stage == GuidedStage.Buttons)
            {
                buttonsSkipped = true;
                Stage = GuidedStage.Complete;
            }
        }

        public void Cancel()
        {
            Stage = GuidedStage.Idle;
            lastUpdate = DateTime.UtcNow;
        }

        public int StepNumber
        {
            get
            {
                if (Stage == GuidedStage.Center) return 1;
                if (Stage == GuidedStage.LeftStick) return 2;
                if (Stage == GuidedStage.RightStick) return 3;
                if (Stage == GuidedStage.Triggers) return 4;
                return 5;
            }
        }

        public double Progress
        {
            get
            {
                if (Stage == GuidedStage.Center) return Math.Min(1.0, centerStableSeconds / 2.0);
                if (Stage == GuidedStage.LeftStick) return CountBits(leftDirections) / 4.0;
                if (Stage == GuidedStage.RightStick) return CountBits(rightDirections) / 4.0;
                if (Stage == GuidedStage.Triggers) return CountBits(triggerMask) / 2.0;
                if (Stage == GuidedStage.Buttons) return ButtonCount / 14.0;
                if (Stage == GuidedStage.Complete) return 1.0;
                return 0;
            }
        }

        public string StageTitle
        {
            get
            {
                if (Stage == GuidedStage.Center) return "第 1 步 · 中心基线";
                if (Stage == GuidedStage.LeftStick) return "第 2 步 · 左摇杆行程";
                if (Stage == GuidedStage.RightStick) return "第 3 步 · 右摇杆行程";
                if (Stage == GuidedStage.Triggers) return "第 4 步 · 扳机行程";
                if (Stage == GuidedStage.Buttons) return "第 5 步 · 按键覆盖";
                return "体检完成";
            }
        }

        public string Instruction
        {
            get
            {
                if (Stage == GuidedStage.Center) return "松开所有按键，并保持两个摇杆居中";
                if (Stage == GuidedStage.LeftStick) return "将左摇杆依次推到上、下、左、右边缘";
                if (Stage == GuidedStage.RightStick) return "将右摇杆依次推到上、下、左、右边缘";
                if (Stage == GuidedStage.Triggers) return "分别将 LT 与 RT 扣到底";
                if (Stage == GuidedStage.Buttons) return "按下清单中的每一个按键";
                return HasSkipped ? "体检已完成，部分项目尚未验证" : "全部项目均已验证通过";
            }
        }

        public string Detail
        {
            get
            {
                if (Stage == GuidedStage.Center) return string.Format(CultureInfo.InvariantCulture, "稳定保持 2 秒；当前 {0:0.0} 秒，检测到移动会重新计时。", centerStableSeconds);
                if (Stage == GuidedStage.LeftStick) return string.Format(CultureInfo.InvariantCulture, "已识别 {0}/4 个方向；越过 75% 行程即记录。", CountBits(leftDirections));
                if (Stage == GuidedStage.RightStick) return string.Format(CultureInfo.InvariantCulture, "已识别 {0}/4 个方向；越过 75% 行程即记录。", CountBits(rightDirections));
                if (Stage == GuidedStage.Triggers) return string.Format(CultureInfo.InvariantCulture, "已验证 {0}/2 个扳机；需达到 90% 行程。", CountBits(triggerMask));
                if (Stage == GuidedStage.Buttons) return string.Format(CultureInfo.InvariantCulture, "已识别 {0}/14 个按键；Xbox Guide 键不纳入测试。", ButtonCount);
                return HasSkipped ? "可重新测试未完成项目，或导出当前结果。" : "结果可导出为 JSON 或 CSV，便于留档和复测。";
            }
        }

        public string ResultText(int index)
        {
            if (index == 0)
            {
                if (connectionPassed) return "通过";
                return Stage == GuidedStage.Complete ? "未完成" : "测量中";
            }
            bool passed = index == 1 ? centerPassed : index == 2 ? leftPassed : index == 3 ? rightPassed : index == 4 ? triggersPassed : buttonsPassed;
            bool skipped = index == 1 ? centerSkipped : index == 2 ? leftSkipped : index == 3 ? rightSkipped : index == 4 ? triggersSkipped : buttonsSkipped;
            if (passed) return "通过";
            if (skipped) return "已跳过";
            return Stage == GuidedStage.Complete ? "未完成" : "待测试";
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
