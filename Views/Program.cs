// Program
//
// Extracted verbatim from ControllerLab.cs (lines 31-297) on 2026-09-22
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
    public static class Program
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string className, string windowName);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr window, int command);

        [STAThread]
        public static void Main()
        {
            if (HasArgument("--startup-selftest"))
            {
                try
                {
                    App startupApp = new App();
                    MainWindow startupWindow = new MainWindow();
                    startupWindow.Close();
                    startupApp.Shutdown();
                    Console.WriteLine("ControllerLab startup construction self-test passed.");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.ToString());
                    Environment.ExitCode = 1;
                }
                return;
            }
            if (HasArgument("--runtime-selftest"))
            {
                Exception runtimeFailure = null;
                App runtimeApp = new App();
                runtimeApp.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e)
                {
                    runtimeFailure = e.Exception;
                    e.Handled = true;
                    runtimeApp.Shutdown(-1);
                };
                MainWindow runtimeWindow = new MainWindow();
                runtimeWindow.Loaded += delegate
                {
                    DispatcherTimer shutdownTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
                    shutdownTimer.Tick += delegate
                    {
                        shutdownTimer.Stop();
                        runtimeWindow.Close();
                    };
                    shutdownTimer.Start();
                };
                runtimeApp.Run(runtimeWindow);
                if (runtimeFailure != null)
                {
                    Console.Error.WriteLine(runtimeFailure.ToString());
                    Environment.ExitCode = 1;
                }
                else Console.WriteLine("ControllerLab displayed-runtime self-test passed.");
                return;
            }
            if (HasArgument("--controller-navigation-selftest"))
            {
                Exception navigationFailure = null;
                string navigationResult = null;
                App navigationApp = new App();
                MainWindow navigationWindow = new MainWindow();
                navigationWindow.Width = 1100;
                navigationWindow.Height = 720;
                navigationWindow.Loaded += delegate
                {
                    DispatcherTimer navigationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
                    navigationTimer.Tick += delegate
                    {
                        navigationTimer.Stop();
                        try { navigationResult = navigationWindow.RunControllerNavigationSelfTest(); }
                        catch (Exception ex) { navigationFailure = ex; }
                        finally { navigationWindow.Close(); }
                    };
                    navigationTimer.Start();
                };
                navigationApp.Run(navigationWindow);
                if (navigationFailure != null)
                {
                    Console.Error.WriteLine(navigationFailure.ToString());
                    Environment.ExitCode = 1;
                }
                else Console.WriteLine(navigationResult);
                return;
            }
            if (HasArgument("--xbox-trigger-calibrate"))
            {
                App calibrationApp = new App();
                XboxTriggerCalibrationWindow calibrationWindow = new XboxTriggerCalibrationWindow(
                    XboxRegionManager.Load(false), XboxRegionManager.LoadControllerPhotoForCalibration());
                calibrationApp.Run(calibrationWindow);
                return;
            }
            if (HasArgument("--xbox-face-calibrate"))
            {
                App calibrationApp = new App();
                XboxCalibrationWindow calibrationWindow = new XboxCalibrationWindow(
                    XboxRegionManager.Load(false), XboxRegionManager.LoadControllerPhotoForCalibration(),
                    new[] { "a", "b", "x", "y" }, "Xbox A/B/X/Y 手动校准");
                calibrationApp.Run(calibrationWindow);
                return;
            }
            if (HasArgument("--xbox-dpad-up-calibrate") || HasArgument("--xbox-dpad-calibrate"))
            {
                App calibrationApp = new App();
                XboxDPadCalibrationWindow calibrationWindow = new XboxDPadCalibrationWindow(XboxRegionManager.Load(false), XboxRegionManager.LoadControllerPhotoForCalibration(), "dpad-up");
                calibrationApp.Run(calibrationWindow);
                return;
            }
            if (HasArgument("--ds5-touch-parser-selftest"))
            {
                Console.WriteLine(SonyInputManager.RunTouchParserSelfTest());
                return;
            }
            if (HasArgument("--ds5-motion-selftest"))
            {
                Console.WriteLine(DualSenseMotionSelfTest.Run());
                return;
            }
            if (HasArgument("--dualsense-advanced-selftest"))
            {
                Console.WriteLine(DualSenseAdvancedSelfTest.Run());
                return;
            }
            if (HasArgument("--ds5-overlay-selftest"))
            {
                Console.WriteLine(DualSenseRegionManager.RunOverlayGeometrySelfTest());
                return;
            }
            if (HasArgument("--xbox-overlay-selftest"))
            {
                Console.WriteLine(XboxRegionManager.RunOverlayGeometrySelfTest());
                return;
            }
            if (HasArgument("--xbox-top-controls-autocal-selftest"))
            {
                Console.WriteLine(XboxRegionManager.RunTopControlAutoCalibrationSelfTest());
                return;
            }
            if (HasArgument("--xbox-dpad-autocal-selftest"))
            {
                Console.WriteLine(XboxRegionManager.RunDPadAutoCalibrationSelfTest());
                return;
            }
            if (HasArgument("--xbox-dpad-autocal-report"))
            {
                Console.WriteLine(XboxRegionManager.GetDPadAutoCalibrationReport());
                return;
            }
            if (HasArgument("--xbox-dpad-up-render-audit"))
            {
                string auditDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audit", "xbox-dpadup-2026-07-23");
                Console.WriteLine(XboxRegionManager.RenderDPadUpAudit(auditDirectory));
                return;
            }
            if (HasArgument("--xbox-overlay-render-audit"))
            {
                string auditDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audit", "xbox-polish-2026-07-23", "overlays");
                Console.WriteLine(XboxRegionManager.RenderDefaultOverlayAudit(auditDirectory));
                return;
            }
            if (HasArgument("--xbox-polish-render-audit"))
            {
                string auditDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audit", "xbox-polish-2026-07-23", "polish-final");
                Console.WriteLine(XboxRegionManager.RenderPolishAudit(auditDirectory));
                return;
            }
            if (HasArgument("--xbox-trigger-geometry-render-audit"))
            {
                string auditDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audit", "xbox-trigger-geometry");
                Console.WriteLine(XboxRegionManager.RenderTriggerGeometryAudit(auditDirectory));
                return;
            }
            if (HasArgument("--trigger-chart-selftest"))
            {
                Console.WriteLine(TriggerTelemetryBuffer.RunSelfTest());
                return;
            }
            if (HasArgument("--rumble-selftest"))
            {
                Console.WriteLine(ControllerRumbleSelfTest.Run());
                return;
            }
            if (HasArgument("--controller-core-selftest"))
            {
                Console.WriteLine(ControllerCoreSelfTest.Run());
                return;
            }
            if (HasArgument("--stick-drift-selftest"))
            {
                Console.WriteLine(ControllerCoreSelfTest.RunStickDriftSelfTest());
                return;
            }
            if (HasArgument("--joystick-analyzer-selftest"))
            {
                Console.WriteLine(JoystickAnalyzerSelfTest.Run());
                return;
            }
            if (HasArgument("--health-check-selftest"))
            {
                Console.WriteLine(ControllerHealthCheckSelfTest.Run());
                return;
            }
            if (HasArgument("--product-experience-selftest"))
            {
                Console.WriteLine(ProductExperienceSelfTest.Run());
                return;
            }
            if (HasArgument("--product-ui-render-audit"))
            {
                try
                {
                    App auditApp = new App();
                    MainWindow auditWindow = new MainWindow();
                    string directory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audit", "product-ui-2026-07-31");
                    Console.WriteLine(auditWindow.RenderProductUiAudit(directory));
                    auditWindow.Close();
                    auditApp.Shutdown();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.ToString());
                    Environment.ExitCode = 1;
                }
                return;
            }
            if (HasArgument("--device-manager-selftest"))
            {
                Console.WriteLine(ControllerCoreSelfTest.RunDeviceManagerSelfTest());
                return;
            }
            bool demo = HasArgument("--demo") || HasArgument("--sony-demo") || HasArgument("--multi-demo");
            string mutexName = HasArgument("--multi-demo") ? "Local\\ControllerLab.MultiDemo" : (demo ? "Local\\ControllerLab.Demo" : "Local\\ControllerLab");
            bool created;
            using (Mutex mutex = new Mutex(true, mutexName, out created))
            {
                if (!created)
                {
                    IntPtr existing = FindWindow(null, "手柄实验室");
                    if (existing == IntPtr.Zero) existing = FindWindow(null, "Xbox 手柄实验室");
                    if (existing != IntPtr.Zero)
                    {
                        ShowWindow(existing, 9);
                        SetForegroundWindow(existing);
                    }
                    return;
                }
                App app = new App();
                app.Run(new MainWindow());
            }
        }

        private static bool HasArgument(string value)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], value, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
