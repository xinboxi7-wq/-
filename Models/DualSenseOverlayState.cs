// DualSenseOverlayState
//
// Extracted verbatim from ControllerLab.cs (lines 8917-8972) on 2026-09-22
// as part of the ControllerLab structural split. No logic was changed.
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
    public sealed class DualSenseOverlayState
    {
        public bool Connected;
        public bool ReducedMotion;
        public double DpadUp;
        public double DpadDown;
        public double DpadLeft;
        public double DpadRight;
        public double Cross;
        public double Circle;
        public double Square;
        public double Triangle;
        public double L3;
        public double R3;
        public double L1;
        public double R1;
        public double L2;
        public double R2;
        public double Create;
        public double Options;
        public double Ps;
        public double Microphone;
        public double TouchpadSurface;
        public double TouchpadButton;
        public double LeftX;
        public double LeftY;
        public double RightX;
        public double RightY;
        public bool TouchCoordinatesAvailable;
        public bool HasTouchCoordinates;

        public double ValueFor(string id)
        {
            if (id == "dpad-up") return DpadUp;
            if (id == "dpad-down") return DpadDown;
            if (id == "dpad-left") return DpadLeft;
            if (id == "dpad-right") return DpadRight;
            if (id == "button-cross") return Cross;
            if (id == "button-circle") return Circle;
            if (id == "button-square") return Square;
            if (id == "button-triangle") return Triangle;
            if (id == "button-l3") return L3;
            if (id == "button-r3") return R3;
            if (id == "button-l1") return L1;
            if (id == "button-r1") return R1;
            if (id == "trigger-l2") return L2;
            if (id == "trigger-r2") return R2;
            if (id == "button-create") return Create;
            if (id == "button-options") return Options;
            if (id == "button-ps") return Ps;
            if (id == "button-mic") return Microphone;
            if (id == "touchpad-surface") return TouchpadSurface;
            if (id == "touchpad-button") return TouchpadButton;
            return 0;
        }
    }
}
