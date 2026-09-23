// Palette
//
// Extracted verbatim from ControllerLab.cs (lines 495-532) on 2026-09-22
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
    public static class Palette
    {
        public static readonly Color Window = Color.FromRgb(10, 17, 23);
        public static readonly Color Surface = Color.FromRgb(17, 29, 39);
        public static readonly Color Surface2 = Color.FromRgb(23, 38, 50);
        public static readonly Color SurfaceHover = Color.FromRgb(29, 47, 60);
        public static readonly Color SurfaceRaised = Color.FromRgb(35, 56, 70);
        public static readonly Color Border = Color.FromRgb(81, 101, 114);
        public static readonly Color Muted = Color.FromRgb(142, 161, 173);
        public static readonly Color Text = Color.FromRgb(235, 242, 247);
        public static readonly Color Green = Color.FromRgb(88, 201, 133);
        public static readonly Color Blue = Color.FromRgb(64, 186, 227);
        public static readonly Color AccentHover = Color.FromRgb(103, 205, 238);
        public static readonly Color TopLeftShoulder = Color.FromRgb(45, 218, 191);
        public static readonly Color TopRightShoulder = Color.FromRgb(151, 112, 245);
        public static readonly Color Red = Color.FromRgb(239, 107, 98);
        public static readonly Color Warning = Color.FromRgb(235, 184, 79);
        public static readonly SolidColorBrush WindowBrush = Freeze(new SolidColorBrush(Window));
        public static readonly SolidColorBrush SurfaceBrush = Freeze(new SolidColorBrush(Surface));
        public static readonly SolidColorBrush Surface2Brush = Freeze(new SolidColorBrush(Surface2));
        public static readonly SolidColorBrush SurfaceHoverBrush = Freeze(new SolidColorBrush(SurfaceHover));
        public static readonly SolidColorBrush SurfaceRaisedBrush = Freeze(new SolidColorBrush(SurfaceRaised));
        public static readonly SolidColorBrush BorderBrush = Freeze(new SolidColorBrush(Border));
        public static readonly SolidColorBrush BorderSubtleBrush = Freeze(new SolidColorBrush(Color.FromArgb(128, Border.R, Border.G, Border.B)));
        public static readonly SolidColorBrush MutedBrush = Freeze(new SolidColorBrush(Muted));
        public static readonly SolidColorBrush TextBrush = Freeze(new SolidColorBrush(Text));
        public static readonly SolidColorBrush GreenBrush = Freeze(new SolidColorBrush(Green));
        public static readonly SolidColorBrush BlueBrush = Freeze(new SolidColorBrush(Blue));
        public static readonly SolidColorBrush AccentHoverBrush = Freeze(new SolidColorBrush(AccentHover));
        public static readonly SolidColorBrush RedBrush = Freeze(new SolidColorBrush(Red));
        public static readonly SolidColorBrush WarningBrush = Freeze(new SolidColorBrush(Warning));

        private static T Freeze<T>(T value) where T : Freezable
        {
            value.Freeze();
            return value;
        }
    }
}
