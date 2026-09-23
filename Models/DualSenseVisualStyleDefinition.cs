// DualSenseVisualStyleDefinition
//
// Extracted verbatim from ControllerLab.cs (lines 9065-9074) on 2026-09-22
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
    public sealed class DualSenseVisualStyleDefinition
    {
        [DataMember(Name = "id")] public string Id { get; set; }
        [DataMember(Name = "fillOpacity")] public double FillOpacity { get; set; }
        [DataMember(Name = "strokeOpacity")] public double StrokeOpacity { get; set; }
        [DataMember(Name = "strokePixels")] public double StrokePixels { get; set; }
        [DataMember(Name = "glowOpacity")] public double GlowOpacity { get; set; }
        [DataMember(Name = "glowPixels")] public double GlowPixels { get; set; }
    }
}
