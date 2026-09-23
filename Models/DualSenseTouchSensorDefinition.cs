// DualSenseTouchSensorDefinition
//
// Extracted verbatim from ControllerLab.cs (lines 9034-9049) on 2026-09-22
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
    [DataContract]
    public sealed class DualSenseTouchSensorDefinition
    {
        [DataMember(Name = "rawWidth")] public int RawWidth { get; set; }
        [DataMember(Name = "rawHeight")] public int RawHeight { get; set; }
        [DataMember(Name = "topLeft")] public DualSenseLogicalPoint TopLeft { get; set; }
        [DataMember(Name = "topRight")] public DualSenseLogicalPoint TopRight { get; set; }
        [DataMember(Name = "bottomLeft")] public DualSenseLogicalPoint BottomLeft { get; set; }
        [DataMember(Name = "bottomRight")] public DualSenseLogicalPoint BottomRight { get; set; }
        // Legacy rectangular values are retained only to load older user overrides. Runtime mapping
        // prefers the four calibrated corners above and never uses a second layout transform.
        [DataMember(Name = "x")] public double X { get; set; }
        [DataMember(Name = "y")] public double Y { get; set; }
        [DataMember(Name = "width")] public double Width { get; set; }
        [DataMember(Name = "height")] public double Height { get; set; }
    }
}
