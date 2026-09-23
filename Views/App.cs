// App
//
// Extracted verbatim from ControllerLab.cs (lines 299-324) on 2026-09-22
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
    public sealed class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            LabLogger.Info("Application", "ControllerLab starting.");
            RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;
            DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs exception)
            {
                RecordUnhandledException("WPF Dispatcher", exception.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs exception)
            {
                RecordUnhandledException("AppDomain", exception.ExceptionObject as Exception);
            };
        }

        // Keep the native exception behavior for unexpected faults, but leave a
        // useful managed stack trace beside the executable's normal user data.
        // This is especially important for faults reported by Windows only as
        // 0xe0434352, which otherwise have no actionable call stack.
        internal static void RecordUnhandledException(string source, Exception exception)
        {
            LabLogger.Error(source, "Unexpected managed exception.", exception);
        }
    }
}
