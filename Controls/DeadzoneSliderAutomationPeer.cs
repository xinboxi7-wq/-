// DeadzoneSliderAutomationPeer
//
// Extracted verbatim from ControllerLab.cs (lines 10256-10293) on 2026-09-22
// as part of the ControllerLab structural split (batch 2).
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
    public sealed class DeadzoneSliderAutomationPeer : FrameworkElementAutomationPeer, IRangeValueProvider
    {
        private readonly DeadzoneSlider slider;

        public DeadzoneSliderAutomationPeer(DeadzoneSlider owner) : base(owner)
        {
            slider = owner;
        }

        protected override string GetClassNameCore()
        {
            return "Slider";
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.Slider;
        }

        public override object GetPattern(PatternInterface patternInterface)
        {
            if (patternInterface == PatternInterface.RangeValue) return this;
            return base.GetPattern(patternInterface);
        }

        public bool IsReadOnly { get { return false; } }
        public double LargeChange { get { return 0.05; } }
        public double Maximum { get { return 0.25; } }
        public double Minimum { get { return 0.0; } }
        public double SmallChange { get { return 0.01; } }
        public double Value { get { return slider.Value; } }

        public void SetValue(double value)
        {
            if (!slider.IsEnabled) throw new ElementNotEnabledException();
            slider.Dispatcher.Invoke(new Action(delegate { slider.Value = value; }));
        }
    }
}
