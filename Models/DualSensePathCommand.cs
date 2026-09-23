// DualSensePathCommand
//
// Extracted verbatim from ControllerLab.cs (lines 8999-9011) on 2026-09-22
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
    public sealed class DualSensePathCommand
    {
        [DataMember(Name = "op")] public string Op { get; set; }
        [DataMember(Name = "x")] public double X { get; set; }
        [DataMember(Name = "y")] public double Y { get; set; }
        [DataMember(Name = "cx")] public double CX { get; set; }
        [DataMember(Name = "cy")] public double CY { get; set; }
        [DataMember(Name = "c1x")] public double C1X { get; set; }
        [DataMember(Name = "c1y")] public double C1Y { get; set; }
        [DataMember(Name = "c2x")] public double C2X { get; set; }
        [DataMember(Name = "c2y")] public double C2Y { get; set; }
    }
}
