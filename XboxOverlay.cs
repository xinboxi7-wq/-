using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ControllerLab
{
    [DataContract]
    public sealed class XboxRegionsDocument
    {
        [DataMember(Name = "schemaVersion")] public int SchemaVersion { get; set; }
        [DataMember(Name = "sourceImage")] public string SourceImage { get; set; }
        [DataMember(Name = "imageWidth")] public int ImageWidth { get; set; }
        [DataMember(Name = "imageHeight")] public int ImageHeight { get; set; }
        [DataMember(Name = "logicalWidth")] public int LogicalWidth { get; set; }
        [DataMember(Name = "logicalHeight")] public int LogicalHeight { get; set; }
        [DataMember(Name = "regions")] public List<XboxRegionDefinition> Regions { get; set; }
    }

    [DataContract]
    public sealed class XboxRegionDefinition
    {
        [DataMember(Name = "id")] public string Id { get; set; }
        [DataMember(Name = "kind")] public string Kind { get; set; }
        [DataMember(Name = "cx")] public double CX { get; set; }
        [DataMember(Name = "cy")] public double CY { get; set; }
        [DataMember(Name = "width")] public double Width { get; set; }
        [DataMember(Name = "height")] public double Height { get; set; }
        [DataMember(Name = "cornerRadius")] public double CornerRadius { get; set; }
        [DataMember(Name = "rotation")] public double Rotation { get; set; }
        [DataMember(Name = "rotationOffset")] public double RotationOffset { get; set; }
        [DataMember(Name = "offsetX")] public double OffsetX { get; set; }
        [DataMember(Name = "offsetY")] public double OffsetY { get; set; }
        [DataMember(Name = "baseRegion")] public string BaseRegion { get; set; }
        // The DPadUp path records its own calibration metadata.  At runtime the
        // coordinates are already baked into the PathGeometry, so both values
        // deliberately stay at 1.0 instead of introducing a local transform.
        [DataMember(Name = "scaleX")] public double ScaleX { get; set; }
        [DataMember(Name = "scaleY")] public double ScaleY { get; set; }
        [DataMember(Name = "points")] public List<XboxLogicalPoint> Points { get; set; }
        [DataMember(Name = "pathCommands")] public List<XboxPathCommand> PathCommands { get; set; }
        [DataMember(Name = "motionCenterX")] public double MotionCenterX { get; set; }
        [DataMember(Name = "motionCenterY")] public double MotionCenterY { get; set; }
        [DataMember(Name = "ringWidth")] public double RingWidth { get; set; }
        [DataMember(Name = "ringHeight")] public double RingHeight { get; set; }
        [DataMember(Name = "capWidth")] public double CapWidth { get; set; }
        [DataMember(Name = "capHeight")] public double CapHeight { get; set; }
        [DataMember(Name = "travelX")] public double TravelX { get; set; }
        [DataMember(Name = "travelY")] public double TravelY { get; set; }
        // Per-side visual behaviour.  LT is the canonical left-top visual
        // region and RT is the canonical right-top visual region; LB/RB only
        // feed those same photographed mask surfaces at render time.
        [DataMember(Name = "geometryPath")] public string GeometryPath { get; set; }
        [DataMember(Name = "fillOrigin")] public string FillOrigin { get; set; }
        [DataMember(Name = "fillDirection")] public string FillDirection { get; set; }
        [DataMember(Name = "easing")] public string Easing { get; set; }
        [DataMember(Name = "edgeStrokeWidth")] public double EdgeStrokeWidth { get; set; }
        [DataMember(Name = "outerGlowWidth")] public double OuterGlowWidth { get; set; }
        [DataMember(Name = "outerGlowOpacity")] public double OuterGlowOpacity { get; set; }
        // Source PNG masks keep their own transparent canvas.  Their placement
        // is anchored by the geometric centre of the non-transparent alpha
        // bounds, never by the full PNG canvas top-left.  Offset remains the
        // user-calibration delta from that target anchor; no crop, rotation,
        // redraw, or non-uniform stretch is applied.
        [DataMember(Name = "triggerAnchorX")] public double TriggerAnchorX { get; set; }
        [DataMember(Name = "triggerAnchorY")] public double TriggerAnchorY { get; set; }
        [DataMember(Name = "triggerOffsetX")] public double TriggerOffsetX { get; set; }
        [DataMember(Name = "triggerOffsetY")] public double TriggerOffsetY { get; set; }
        [DataMember(Name = "triggerScale")] public double TriggerScale { get; set; }
        [DataMember(Name = "triggerMaskOpacity")] public double TriggerMaskOpacity { get; set; }
        // Optional point-level corrections applied to a derived path before its
        // established mirror transform. This keeps RT tied to the LT source
        // silhouette while allowing a photographed connection corner to be
        // corrected without changing Offset, Scale, or the unaffected curves.
        [DataMember(Name = "pathPointAdjustments")] public List<XboxPathPointAdjustment> PathPointAdjustments { get; set; }
    }

    [DataContract]
    public sealed class XboxLogicalPoint
    {
        [DataMember(Name = "x")] public double X { get; set; }
        [DataMember(Name = "y")] public double Y { get; set; }
    }

    // Reversible, editable commands for the one Xbox region that needs a
    // genuinely curved physical contour.  The production renderer compiles
    // these values straight to a StreamGeometry; it never applies a per-region
    // Canvas offset, MatrixTransform, or ScaleTransform.
    [DataContract]
    public sealed class XboxPathCommand
    {
        [DataMember(Name = "op")] public string Op { get; set; }
        [DataMember(Name = "x")] public double X { get; set; }
        [DataMember(Name = "y")] public double Y { get; set; }
        [DataMember(Name = "c1x")] public double C1X { get; set; }
        [DataMember(Name = "c1y")] public double C1Y { get; set; }
        [DataMember(Name = "c2x")] public double C2X { get; set; }
        [DataMember(Name = "c2y")] public double C2Y { get; set; }
    }

    [DataContract]
    public sealed class XboxPathPointAdjustment
    {
        [DataMember(Name = "commandIndex")] public int CommandIndex { get; set; }
        [DataMember(Name = "role")] public string Role { get; set; }
        [DataMember(Name = "dx")] public double DX { get; set; }
        [DataMember(Name = "dy")] public double DY { get; set; }
    }

    public sealed class XboxPathCalibrationHandle
    {
        public int CommandIndex { get; set; }
        public string Role { get; set; }
        public Point Point { get; set; }
        public string Key { get { return CommandIndex.ToString(CultureInfo.InvariantCulture) + ":" + Role; } }
    }

    // A recommendation never edits the hand-traced source path.  It contains
    // only the transform that places that source contour over one DPad key.
    public sealed class XboxDPadAutoCalibrationResult
    {
        public string RegionId { get; set; }
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
        public double ScaleX { get; set; }
        public double ScaleY { get; set; }
        public double RotationOffset { get; set; }
        public double AverageEdgeDistance { get; set; }
        public double CurrentAverageEdgeDistance { get; set; }
        public int SampleCount { get; set; }

        public string Describe()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0}Transform\nOffsetX {1:0.00}, OffsetY {2:0.00}\nScaleX {3:0.000}, ScaleY {4:0.000}\nRotation {5:0.00}°\n边缘误差 {6:0.00}px（当前 {7:0.00}px，{8} 个采样点）",
                RegionId, OffsetX, OffsetY, ScaleX, ScaleY, RotationOffset,
                AverageEdgeDistance, CurrentAverageEdgeDistance, SampleCount);
        }
    }

    // Top controls own their source paths.  This recommendation only adjusts
    // their persisted transform metadata; it never rewrites the traced path.
    public sealed class XboxTopControlAutoCalibrationResult
    {
        public string RegionId { get; set; }
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
        public double ScaleX { get; set; }
        public double ScaleY { get; set; }
        public double Rotation { get; set; }
        public double AverageEdgeDistance { get; set; }
        public double CurrentAverageEdgeDistance { get; set; }
        public int SampleCount { get; set; }
        public string Describe()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0} transform\nOffsetX {1:0.00}, OffsetY {2:0.00}\nScaleX {3:0.000}, ScaleY {4:0.000}\nRotation {5:0.00} deg\nEdge distance {6:0.00}px (current {7:0.00}px), {8} samples",
                RegionId, OffsetX, OffsetY, ScaleX, ScaleY, Rotation, AverageEdgeDistance, CurrentAverageEdgeDistance, SampleCount);
        }
    }

    // Compact local edge-distance field built from the original controller.png
    // pixels. It is calibration-only and is never part of the production
    // rendering path or input pipeline.
    public sealed class XboxDPadEdgeAnalysis
    {
        private readonly int left;
        private readonly int top;
        private readonly int width;
        private readonly int height;
        private readonly float[] distances;
        private readonly List<Point> edgePoints;

        internal XboxDPadEdgeAnalysis(int rawLeft, int rawTop, int rawWidth, int rawHeight, float[] distanceField, List<Point> edges)
        {
            left = rawLeft; top = rawTop; width = rawWidth; height = rawHeight;
            distances = distanceField; edgePoints = edges;
        }

        public IList<Point> EdgePoints { get { return edgePoints; } }

        public double DistanceAtStage(Point stagePoint)
        {
            int x = (int)Math.Round(stagePoint.X / XboxRegionManager.SourceScale, MidpointRounding.AwayFromZero) - left;
            int y = (int)Math.Round((stagePoint.Y - XboxRegionManager.SourceTop) / XboxRegionManager.SourceScale, MidpointRounding.AwayFromZero) - top;
            if (x < 0 || y < 0 || x >= width || y >= height) return 12.0;
            return distances[y * width + x] * XboxRegionManager.SourceScale;
        }
    }

    public sealed class TriggerTelemetryStats
    {
        public double Current;
        public double Peak;
        public double Minimum;
        public double Average;
        public double ReleaseSpeedPerSecond;
        public int ChangeCount;
        public double Noise;
        public bool ReachesFullRange;
        public bool ReturnsToZero;
        public bool StableHold;
        public bool HasRandomJumps;
        public string HealthText;
    }

    // Sampling storage is deliberately independent of WPF. The input thread adds
    // at most one value every 30ms, and the UI only reads a copy for rendering.
    public sealed class TriggerTelemetryBuffer
    {
        private const int Capacity = 170;
        private const double SampleSeconds = 0.030;
        private readonly object gate = new object();
        private readonly List<double> values = new List<double>(Capacity);
        private DateTime lastRecorded = DateTime.MinValue;
        private double current;
        private bool paused;

        public void SetPaused(bool value) { lock (gate) { paused = value; if (!paused) lastRecorded = DateTime.MinValue; } }
        public void Record(double value, DateTime timestamp)
        {
            lock (gate)
            {
                current = Clamp(value);
                if (paused || (lastRecorded != DateTime.MinValue && (timestamp - lastRecorded).TotalSeconds < SampleSeconds)) return;
                lastRecorded = timestamp;
                values.Add(current);
                if (values.Count > Capacity) values.RemoveAt(0);
            }
        }
        public void Clear() { lock (gate) { values.Clear(); lastRecorded = DateTime.MinValue; } }
        public double[] GetSnapshot() { lock (gate) { return values.ToArray(); } }
        public TriggerTelemetryStats GetStats()
        {
            lock (gate)
            {
                TriggerTelemetryStats result = new TriggerTelemetryStats { Current = current, Minimum = 0, HealthText = "等待输入" };
                if (values.Count == 0) return result;
                double sum = 0, min = 1, peak = 0, noise = 0, releaseTotal = 0;
                int releaseCount = 0, changes = 0, jumps = 0;
                for (int i = 0; i < values.Count; i++)
                {
                    double sample = values[i]; sum += sample; min = Math.Min(min, sample); peak = Math.Max(peak, sample);
                    if (i == 0) continue;
                    double delta = sample - values[i - 1];
                    noise += Math.Abs(delta);
                    if (Math.Abs(delta) >= 0.02) changes++;
                    if (Math.Abs(delta) >= 0.16) jumps++;
                    if (delta < 0) { releaseTotal += -delta / SampleSeconds; releaseCount++; }
                }
                result.Peak = peak;
                result.Minimum = min;
                result.Average = sum / values.Count;
                result.ChangeCount = changes;
                result.Noise = values.Count > 1 ? noise / (values.Count - 1) : 0;
                result.ReleaseSpeedPerSecond = releaseCount == 0 ? 0 : releaseTotal / releaseCount;
                result.ReachesFullRange = peak >= 0.98;
                result.ReturnsToZero = peak >= 0.10 && current <= 0.02;
                result.HasRandomJumps = jumps >= 3 && result.Noise > 0.035;
                int holdCount = Math.Min(12, values.Count);
                if (holdCount >= 6)
                {
                    double holdAverage = 0;
                    for (int i = values.Count - holdCount; i < values.Count; i++) holdAverage += values[i];
                    holdAverage /= holdCount;
                    double variance = 0;
                    for (int i = values.Count - holdCount; i < values.Count; i++) variance += (values[i] - holdAverage) * (values[i] - holdAverage);
                    result.StableHold = holdAverage >= 0.10 && Math.Sqrt(variance / holdCount) <= 0.012;
                }
                if (result.HasRandomJumps) result.HealthText = "存在输入波动";
                else if (peak >= 0.98 && result.ReturnsToZero) result.HealthText = "响应正常";
                else if (peak >= 0.50 && result.ReturnsToZero && result.ReleaseSpeedPerSecond < 0.15) result.HealthText = "回弹可能偏慢";
                else if (peak >= 0.10 && !result.ReturnsToZero && current <= 0.05) result.HealthText = "可能未完全回零";
                else if (peak < 0.10) result.HealthText = "等待完整行程测试";
                else result.HealthText = "正在采样";
                return result;
            }
        }
        public static string RunSelfTest()
        {
            TriggerTelemetryBuffer buffer = new TriggerTelemetryBuffer();
            DateTime time = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            for (int i = 0; i < 220; i++)
            {
                double value = i < 70 ? i / 69.0 : i < 120 ? 1.0 : Math.Max(0, 1.0 - (i - 120) / 50.0);
                buffer.Record(value, time.AddSeconds(i * 0.03));
            }
            double[] history = buffer.GetSnapshot();
            TriggerTelemetryStats stats = buffer.GetStats();
            if (history.Length != 170) throw new InvalidOperationException("Trigger buffer is not bounded to five seconds.");
            if (!stats.ReachesFullRange || !stats.ReturnsToZero || stats.Peak < 0.98 || stats.Current > 0.02) throw new InvalidOperationException("Trigger full-range/return analysis failed.");
            buffer.SetPaused(true);
            int count = buffer.GetSnapshot().Length;
            buffer.Record(0.5, time.AddSeconds(20));
            if (buffer.GetSnapshot().Length != count) throw new InvalidOperationException("Paused trigger buffer recorded a sample.");
            buffer.Clear();
            if (buffer.GetSnapshot().Length != 0) throw new InvalidOperationException("Trigger history clear failed.");
            return "Trigger chart self-test passed: five-second bounded history, peak, return-to-zero, pause, clear.";
        }
        private static double Clamp(double value) { return Math.Max(0, Math.Min(1, value)); }
    }

    [DataContract]
    public sealed class XboxRegionsOverride
    {
        [DataMember(Name = "schemaVersion")] public int SchemaVersion { get; set; }
        [DataMember(Name = "sourceImage")] public string SourceImage { get; set; }
        [DataMember(Name = "imageWidth")] public int ImageWidth { get; set; }
        [DataMember(Name = "imageHeight")] public int ImageHeight { get; set; }
        [DataMember(Name = "logicalWidth")] public int LogicalWidth { get; set; }
        [DataMember(Name = "logicalHeight")] public int LogicalHeight { get; set; }
        [DataMember(Name = "regions")] public List<XboxRegionDefinition> Regions { get; set; }
    }

    // The Xbox photo is 1586x992. It is placed inside this 1536x1024 stage at
    // 1536x960 with a 32px top/bottom transparent gutter. Every hit Geometry,
    // the bitmap, stick caps, and feedback draw beneath the same stage transform.
    public sealed class XboxRegionManager
    {
        public const int LogicalWidth = 1536;
        public const int LogicalHeight = 1024;
        public const int SourceImageWidth = 1586;
        public const int SourceImageHeight = 992;
        public const double SourceScale = 1536.0 / SourceImageWidth;
        public const double SourceTop = 32.0;
        private const string ResourceName = "ControllerLab.Assets.xboxRegions.json";
        private const int SchemaVersion = 1;
        private readonly HashSet<string> modified = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private XboxRegionsDocument defaults;
        private XboxRegionsDocument document;
        private Dictionary<string, XboxRegionDefinition> byId;
        private Dictionary<string, Geometry> geometries;
        // LT/RT are supplied transparent source cut-outs. The runtime never
        // reconstructs their silhouettes from an editable PathGeometry.
        private BitmapSource leftTriggerMask;
        private BitmapSource rightTriggerMask;
        private BitmapSource leftTriggerMaskEdge;
        private BitmapSource rightTriggerMaskEdge;
        private BitmapSource leftTriggerMaskGlow;
        private BitmapSource rightTriggerMaskGlow;
        private Rect leftTriggerMaskBounds;
        private Rect rightTriggerMaskBounds;

        public XboxRegionsDocument Document { get { return document; } }
        public string LastLoadMessage { get; private set; }
        public static string OverridePath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ControllerLab", "xbox-regions.override.json"); }
        }

        public static XboxRegionManager Load(bool ignoreOverride)
        {
            XboxRegionManager manager = new XboxRegionManager();
            manager.Reload(ignoreOverride);
            return manager;
        }

        public void Reload(bool ignoreOverride)
        {
            defaults = ReadEmbedded<XboxRegionsDocument>(ResourceName);
            string validationMessage;
            if (!ValidateDocument(defaults, out validationMessage)) throw new InvalidDataException(validationMessage);
            LastLoadMessage = validationMessage;
            document = Clone(defaults);
            modified.Clear();
            if (!ignoreOverride && File.Exists(OverridePath))
            {
                try
                {
                    XboxRegionsOverride overrideData = ReadFile<XboxRegionsOverride>(OverridePath);
                    string reason;
                    if (ValidateOverride(overrideData, out reason))
                    {
                        MergeOverride(overrideData);
                        LastLoadMessage = "已加载 Xbox 用户轮廓覆盖。";
                    }
                    else LastLoadMessage = "已忽略无效 Xbox 轮廓覆盖：" + reason;
                }
                catch (Exception ex)
                {
                    LastLoadMessage = "已忽略无法读取的 Xbox 轮廓覆盖：" + ex.Message;
                }
            }
            Rebuild();
            LoadTriggerMasks();
        }

        public static string RunOverlayGeometrySelfTest()
        {
            XboxRegionManager manager = Load(true);
            string[] ids = { "a", "b", "x", "y", "dpad-up", "dpad-down", "dpad-left", "dpad-right", "view", "menu", "guide", "lb", "rb", "lt", "rt", "l3", "r3" };
            if (manager.Document.LogicalWidth != LogicalWidth || manager.Document.LogicalHeight != LogicalHeight) throw new InvalidOperationException("Xbox logical stage must be 1536x1024.");
            for (int i = 0; i < ids.Length; i++)
            {
                if (string.Equals(ids[i], "lt", StringComparison.OrdinalIgnoreCase) || string.Equals(ids[i], "rt", StringComparison.OrdinalIgnoreCase)) continue;
                Geometry geometry = manager.GetGeometry(ids[i]);
                if (geometry == null || geometry.Bounds.IsEmpty || geometry.Bounds.Width <= 0 || geometry.Bounds.Height <= 0) throw new InvalidOperationException("Missing Xbox region: " + ids[i]);
                if (geometry.Transform != null && !geometry.Transform.Value.IsIdentity && !IsDPadTransformRegion(manager.GetRegion(ids[i])) && !IsDerivedTrigger(manager.GetRegion(ids[i]))) throw new InvalidOperationException("Unexpected region transform: " + ids[i]);
                Rect bounds = geometry.Bounds;
                if (bounds.Left < 0 || bounds.Top < 0 || bounds.Right > LogicalWidth || bounds.Bottom > LogicalHeight) throw new InvalidOperationException("Region escapes logical stage: " + ids[i]);
            }
            string[] dpadIds = { "dpad-up", "dpad-down", "dpad-left", "dpad-right" };
            XboxRegionDefinition dpadSourceDefinition = manager.GetRegion("dpad-up");
            int dpadUpPathHandleCount = dpadSourceDefinition == null || dpadSourceDefinition.PathCommands == null ? 0 : dpadSourceDefinition.PathCommands.Count - 1;
            if (dpadUpPathHandleCount < 20) throw new InvalidOperationException("Image-extracted DPadUp source path is incomplete.");
            for (int i = 0; i < dpadIds.Length; i++)
            {
                XboxRegionDefinition dpad = manager.GetRegion(dpadIds[i]);
                Geometry dpadGeometry = manager.GetGeometry(dpadIds[i]);
                if (dpad == null || dpadGeometry == null || dpad.Points != null) throw new InvalidOperationException("DPad must not fall back to a polygon: " + dpadIds[i]);
                if (manager.GetPathHandles(dpadIds[i]).Count != dpadUpPathHandleCount) throw new InvalidOperationException("DPad control point map is incomplete: " + dpadIds[i]);
                if (i == 0)
                {
                    if (!string.Equals(dpad.Kind, "path", StringComparison.OrdinalIgnoreCase) || dpad.PathCommands == null || dpad.PathCommands.Count < 20 || Math.Abs(dpad.ScaleX - 1.0) > 0.0001 || Math.Abs(dpad.ScaleY - 1.0) > 0.0001 || Math.Abs(dpad.Rotation) > 0.0001)
                        throw new InvalidOperationException("DPadUp must remain the locked source PathGeometry.");
                }
                else
                {
                    double expectedRotation = string.Equals(dpadIds[i], "dpad-right", StringComparison.OrdinalIgnoreCase) ? 90 : (string.Equals(dpadIds[i], "dpad-down", StringComparison.OrdinalIgnoreCase) ? 180 : 270);
                    if (!IsDerivedDPad(dpad) || !string.Equals(dpad.BaseRegion, "dpad-up", StringComparison.OrdinalIgnoreCase) || dpad.PathCommands != null || Math.Abs(dpad.Rotation - expectedRotation) > 0.0001 || dpadGeometry.Transform == null || dpadGeometry.Transform.Value.IsIdentity)
                        throw new InvalidOperationException("DPad direction must derive from DPadUp using its own transform calibration: " + dpadIds[i]);
                }
            }
            XboxRegionDefinition dpadUp = manager.GetRegion("dpad-up");
            Geometry dpadUpGeometry = manager.GetGeometry("dpad-up");
            Rect dpadBounds = dpadUpGeometry.Bounds;
            if (dpadBounds.Left < 576 || dpadBounds.Right > 645 || dpadBounds.Top < 424 || dpadBounds.Bottom > 491) throw new InvalidOperationException("DPadUp physical-surface bounds are outside the image-extracted contour.");
            List<XboxPathCalibrationHandle> dpadHandles = manager.GetPathHandles("dpad-up");
            if (dpadHandles.Count != dpadUpPathHandleCount) throw new InvalidOperationException("DPadUp control point map is incomplete.");
            XboxPathCommand lockedCommand = dpadUp.PathCommands[2];
            double lockedX = lockedCommand.X;
            if (manager.MovePathHandle("dpad-up", 2, "P", 1.0, -1.0)) throw new InvalidOperationException("DPadUp source PathGeometry must stay locked during transform calibration.");
            if (!manager.MoveDPadTransform("dpad-up", 1.0, -1.0)) throw new InvalidOperationException("DPadUp transform edit was rejected.");
            if (Math.Abs(dpadUp.PathCommands[2].X - lockedX) > 0.0001 || manager.GetGeometry("dpad-up").Transform == null || manager.GetGeometry("dpad-up").Transform.Value.IsIdentity)
                throw new InvalidOperationException("DPadUp transform calibration changed the base PathGeometry or did not create a local transform.");
            manager.ResetRegion("dpad-up");
            string[] stickIds = { "l3", "r3" };
            for (int i = 0; i < stickIds.Length; i++)
            {
                XboxRegionDefinition stick = manager.GetRegion(stickIds[i]);
                if (stick == null || stick.MotionCenterX == 0 || stick.MotionCenterY == 0 || stick.CapWidth <= 0 || stick.CapHeight <= 0 || stick.RingWidth <= stick.CapWidth || stick.RingHeight <= stick.CapHeight || stick.TravelX <= 0 || stick.TravelY <= 0)
                    throw new InvalidOperationException("Invalid independent Xbox stick motion geometry: " + stickIds[i]);
                Geometry ring = manager.GetStickRingGeometry(stickIds[i]);
                Point ringCenter = new Point(ring.Bounds.Left + ring.Bounds.Width * 0.5, ring.Bounds.Top + ring.Bounds.Height * 0.5);
                if (Math.Abs(ringCenter.X - stick.CX) > 0.001 || Math.Abs(ringCenter.Y - stick.CY) > 0.001)
                    throw new InvalidOperationException("Xbox stick ring must remain at the fixed base center: " + stickIds[i]);
            }
            string[] faceIds = { "a", "b", "x", "y" };
            HashSet<string> faceMeasurements = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < faceIds.Length; i++)
            {
                XboxRegionDefinition face = manager.GetRegion(faceIds[i]);
                if (face == null || face.EdgeStrokeWidth < 1.0 || face.EdgeStrokeWidth > 2.0 || face.OuterGlowWidth < 2.0 || face.OuterGlowWidth > 6.0)
                    throw new InvalidOperationException("Invalid independent face-button visual parameters: " + faceIds[i]);
                faceMeasurements.Add(face.CX.ToString("0.00", CultureInfo.InvariantCulture) + ":" + face.CY.ToString("0.00", CultureInfo.InvariantCulture) + ":" + face.Width.ToString("0.00", CultureInfo.InvariantCulture) + ":" + face.Height.ToString("0.00", CultureInfo.InvariantCulture));
            }
            if (faceMeasurements.Count != faceIds.Length) throw new InvalidOperationException("Face buttons must not share one measurement.");
            XboxRegionDefinition lb = manager.GetRegion("lb");
            XboxRegionDefinition rb = manager.GetRegion("rb");
            XboxRegionDefinition lt = manager.GetRegion("lt");
            XboxRegionDefinition rt = manager.GetRegion("rt");
            if (lb == null || rb == null || lt == null || rt == null || !IsImageMaskTrigger(lt) || !IsImageMaskTrigger(rt) || lt.PathCommands != null || rt.PathCommands != null || lt.TriggerScale <= 0 || rt.TriggerScale <= 0 || lt.TriggerAnchorX == 0 || lt.TriggerAnchorY == 0 || rt.TriggerAnchorX == 0 || rt.TriggerAnchorY == 0 || !string.Equals(lt.GeometryPath, "mask:LeftTopTriggerMask.png", StringComparison.OrdinalIgnoreCase) || !string.Equals(rt.GeometryPath, "mask:RightTopTriggerMask.png", StringComparison.OrdinalIgnoreCase) || manager.GetTriggerMask("lt") == null || manager.GetTriggerMask("rt") == null || manager.GetTriggerMaskEdge("lt") == null || manager.GetTriggerMaskEdge("rt") == null || manager.GetTriggerMaskGlow("lt") == null || manager.GetTriggerMaskGlow("rt") == null || manager.GetTriggerMaskBounds("lt").IsEmpty || manager.GetTriggerMaskBounds("rt").IsEmpty)
                throw new InvalidOperationException("Xbox top controls must use the shared left/right transparent PNG masks.");
            if (!string.Equals(lt.FillOrigin, "left", StringComparison.OrdinalIgnoreCase) || !string.Equals(lt.FillDirection, "leftToRight", StringComparison.OrdinalIgnoreCase) || !string.Equals(rt.FillOrigin, "right", StringComparison.OrdinalIgnoreCase) || !string.Equals(rt.FillDirection, "rightToLeft", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Xbox trigger fill directions are invalid.");
            LinearGradientBrush ltQuarter = CreateTriggerPressureField(lt, manager.GetTriggerMaskBounds("lt"), 0.25, Palette.Green);
            LinearGradientBrush rtQuarter = CreateTriggerPressureField(rt, manager.GetTriggerMaskBounds("rt"), 0.25, Palette.Blue);
            if (ltQuarter.StartPoint.X >= ltQuarter.EndPoint.X || rtQuarter.StartPoint.X <= rtQuarter.EndPoint.X || ltQuarter.MappingMode != BrushMappingMode.Absolute || rtQuarter.MappingMode != BrushMappingMode.Absolute)
                throw new InvalidOperationException("Xbox trigger mask fill directions are invalid.");
            string[] triggerIds = { "lt", "rt" };
            for (int i = 0; i < triggerIds.Length; i++)
            {
                Rect triggerBounds = manager.GetTriggerMaskBounds(triggerIds[i]);
                Point currentAnchor = manager.GetTriggerCurrentAnchor(triggerIds[i]);
                if (Math.Abs(triggerBounds.Left + triggerBounds.Width * 0.5 - currentAnchor.X) > 0.001 || Math.Abs(triggerBounds.Top + triggerBounds.Height * 0.5 - currentAnchor.Y) > 0.001)
                    throw new InvalidOperationException("Xbox trigger alpha bounds must be centred on the configured alpha anchor: " + triggerIds[i]);
            }
            int[] widths = { 1920, 1440, 1280 };
            double[] dpis = { 1.0, 1.25, 1.5 };
            for (int i = 0; i < widths.Length; i++)
            {
                for (int j = 0; j < dpis.Length; j++)
                {
                    Matrix matrix = CreateStageMatrix(widths[i] / dpis[j], 820 / dpis[j]);
                    if (matrix.M11 <= 0 || Math.Abs(matrix.M11 - matrix.M22) > 0.000001) throw new InvalidOperationException("Invalid uniform Xbox stage matrix.");
                }
            }
            return string.Format(CultureInfo.InvariantCulture, "Xbox overlay self-test passed: 15 Geometry regions + 2 source-aligned trigger alpha masks, 1536x1024 shared stage, 3 widths x 3 DPI matrices. Image-extracted DPadUp PathGeometry + 3 calibrated transforms / {0} displayed handles; DPadUp bounds X {1:0.00}, Y {2:0.00}, W {3:0.00}, H {4:0.00}.", dpadUpPathHandleCount * 4, dpadBounds.X, dpadBounds.Y, dpadBounds.Width, dpadBounds.Height);
        }

        public static string RunDPadAutoCalibrationSelfTest()
        {
            XboxRegionManager manager = Load(true);
            BitmapSource photo = LoadEmbeddedPhoto();
            if (photo == null) throw new InvalidOperationException("Xbox controller source image is unavailable.");
            XboxRegionDefinition source = manager.GetRegion("dpad-up");
            double lockedCommandX = source.PathCommands[2].X;
            string[] ids = { "dpad-up", "dpad-down", "dpad-left", "dpad-right" };
            double totalBefore = 0;
            double totalRecommended = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                XboxDPadEdgeAnalysis analysis = manager.BuildDPadEdgeAnalysis(photo, ids[i]);
                XboxDPadAutoCalibrationResult result = manager.FindDPadTransformRecommendation(ids[i], analysis);
                if (result == null || result.SampleCount < 20 || double.IsNaN(result.AverageEdgeDistance) || double.IsInfinity(result.AverageEdgeDistance)) throw new InvalidOperationException("DPad auto-calibration produced no valid recommendation: " + ids[i]);
                XboxRegionDefinition region = manager.GetRegion(ids[i]);
                double originX = region.OffsetX;
                double originY = region.OffsetY;
                double originScaleX = region.ScaleX;
                double originScaleY = region.ScaleY;
                double originRotation = region.RotationOffset;
                if (Math.Abs(result.OffsetX - originX) > 10.001 || Math.Abs(result.OffsetY - originY) > 10.001 || result.ScaleX < originScaleX * 0.949 || result.ScaleX > originScaleX * 1.051 || result.ScaleY < originScaleY * 0.949 || result.ScaleY > originScaleY * 1.051 || Math.Abs(result.RotationOffset - originRotation) > 3.001)
                    throw new InvalidOperationException("DPad auto-calibration escaped its declared search window: " + ids[i]);
                totalBefore += result.CurrentAverageEdgeDistance;
                totalRecommended += result.AverageEdgeDistance;
            }
            if (Math.Abs(source.PathCommands[2].X - lockedCommandX) > 0.0001) throw new InvalidOperationException("DPad auto-calibration changed the base PathGeometry.");
            return string.Format(CultureInfo.InvariantCulture, "Xbox DPad auto-calibration self-test passed: 4 independent transforms, average edge distance {0:0.00}px -> {1:0.00}px; DPadUp source path remains locked.", totalBefore / ids.Length, totalRecommended / ids.Length);
        }

        public static string GetDPadAutoCalibrationReport()
        {
            XboxRegionManager manager = Load(true);
            BitmapSource photo = LoadEmbeddedPhoto();
            if (photo == null) throw new InvalidOperationException("Xbox controller source image is unavailable.");
            StringBuilder report = new StringBuilder();
            string[] ids = { "dpad-up", "dpad-down", "dpad-left", "dpad-right" };
            for (int i = 0; i < ids.Length; i++)
            {
                XboxDPadAutoCalibrationResult result = manager.FindDPadTransformRecommendation(ids[i], manager.BuildDPadEdgeAnalysis(photo, ids[i]));
                if (result == null) throw new InvalidOperationException("Unable to calculate DPad recommendation: " + ids[i]);
                report.AppendFormat(CultureInfo.InvariantCulture, "{0}|{1:0.000}|{2:0.000}|{3:0.000000}|{4:0.000000}|{5:0.000}|{6:0.000}|{7:0.000}\n", result.RegionId, result.OffsetX, result.OffsetY, result.ScaleX, result.ScaleY, result.RotationOffset, result.CurrentAverageEdgeDistance, result.AverageEdgeDistance);
            }
            return report.ToString();
        }

        public static string RunTopControlAutoCalibrationSelfTest()
        {
            XboxRegionManager manager = Load(true);
            BitmapSource photo = LoadEmbeddedPhoto();
            if (photo == null) throw new InvalidOperationException("Xbox controller source image is unavailable.");
            // LT/RT are now alpha-anchored photographed masks, not editable
            // PathGeometry. Their placement is covered by the mask-anchor
            // assertions in RunOverlayGeometrySelfTest. This transform search
            // remains intentionally limited to the two legacy shoulder paths.
            string[] ids = { "lb", "rb" };
            double before = 0;
            double recommended = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                XboxRegionDefinition region = manager.GetRegion(ids[i]);
                XboxTopControlAutoCalibrationResult result = manager.FindTopControlTransformRecommendation(ids[i], manager.BuildTopControlEdgeAnalysis(photo, ids[i]));
                if (result == null || result.SampleCount < 20 || double.IsNaN(result.AverageEdgeDistance) || double.IsInfinity(result.AverageEdgeDistance)) throw new InvalidOperationException("Top-control auto-calibration produced no valid recommendation: " + ids[i]);
                double startScaleX = region.ScaleX == 0 ? 1.0 : region.ScaleX;
                double startScaleY = region.ScaleY == 0 ? 1.0 : region.ScaleY;
                double startRotation = IsTriggerTransformRegion(region) ? region.RotationOffset : region.Rotation;
                if (Math.Abs(result.OffsetX - region.OffsetX) > 10.001 || Math.Abs(result.OffsetY - region.OffsetY) > 10.001 || result.ScaleX < startScaleX * 0.949 || result.ScaleX > startScaleX * 1.051 || result.ScaleY < startScaleY * 0.949 || result.ScaleY > startScaleY * 1.051 || Math.Abs(result.Rotation - startRotation) > 3.001)
                    throw new InvalidOperationException("Top-control auto-calibration escaped its declared search window: " + ids[i]);
                before += result.CurrentAverageEdgeDistance;
                recommended += result.AverageEdgeDistance;
            }
            return string.Format(CultureInfo.InvariantCulture, "Xbox shoulder-path auto-calibration self-test passed: LB/RB average edge distance {0:0.00}px -> {1:0.00}px. LT/RT alpha-mask anchors are validated by the overlay self-test.", before / ids.Length, recommended / ids.Length);
        }

        // Development-only visual proof: renders every default region with the
        // same WPF DrawingContext path used by the live visualizer.  It never
        // participates in controller input or normal application startup.
        public static string RenderDefaultOverlayAudit(string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory)) throw new ArgumentException("Output directory is required.", "outputDirectory");
            Directory.CreateDirectory(outputDirectory);
            XboxRegionManager manager = Load(true);
            BitmapSource photo = LoadEmbeddedPhoto();
            BitmapSource cap = LoadEmbeddedBitmap("ControllerLab.Assets.stick-cap.png");
            if (photo == null) throw new InvalidOperationException("Embedded Xbox controller image is unavailable.");
            KeyValuePair<string, ushort>[] buttons =
            {
                new KeyValuePair<string, ushort>("a", 0x1000), new KeyValuePair<string, ushort>("b", 0x2000),
                new KeyValuePair<string, ushort>("x", 0x4000), new KeyValuePair<string, ushort>("y", 0x8000),
                new KeyValuePair<string, ushort>("dpad-up", 0x0001), new KeyValuePair<string, ushort>("dpad-down", 0x0002),
                new KeyValuePair<string, ushort>("dpad-left", 0x0004), new KeyValuePair<string, ushort>("dpad-right", 0x0008),
                new KeyValuePair<string, ushort>("view", 0x0020), new KeyValuePair<string, ushort>("menu", 0x0010),
                new KeyValuePair<string, ushort>("guide", 0x0400), new KeyValuePair<string, ushort>("lb", 0x0100),
                new KeyValuePair<string, ushort>("rb", 0x0200), new KeyValuePair<string, ushort>("l3", 0x0040),
                new KeyValuePair<string, ushort>("r3", 0x0080)
            };
            int written = 0;
            for (int i = 0; i < buttons.Length; i++)
            {
                InputSnapshot state = new InputSnapshot { Connected = true, Buttons = buttons[i].Value };
                if (buttons[i].Key == "l3") { state.LeftX = 18022; state.LeftY = -9175; }
                if (buttons[i].Key == "r3") { state.RightX = -11468; state.RightY = 14745; }
                RenderAuditFrame(manager, photo, cap, state, Path.Combine(outputDirectory, (i + 1).ToString("00", CultureInfo.InvariantCulture) + "-" + buttons[i].Key + "-active.png"));
                written++;
            }
            RenderAuditFrame(manager, photo, cap, new InputSnapshot { Connected = true, LeftTrigger = 255 }, Path.Combine(outputDirectory, "16-lt-active.png"));
            RenderAuditFrame(manager, photo, cap, new InputSnapshot { Connected = true, RightTrigger = 255 }, Path.Combine(outputDirectory, "17-rt-active.png"));
            written += 2;
            return "Xbox overlay render audit wrote " + written.ToString(CultureInfo.InvariantCulture) + " WPF frames to " + outputDirectory;
        }

        // Expanded visual-only audit for Overlay Polish.  Each image uses the
        // production drawing order and is safe to create without a controller.
        public static string RenderPolishAudit(string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory)) throw new ArgumentException("Output directory is required.", "outputDirectory");
            XboxRegionManager manager = Load(true);
            BitmapSource photo = LoadEmbeddedPhoto();
            BitmapSource cap = LoadEmbeddedBitmap("ControllerLab.Assets.stick-cap.png");
            if (photo == null) throw new InvalidOperationException("Embedded Xbox controller image is unavailable.");
            Directory.CreateDirectory(outputDirectory);
            int written = 0;

            // Face buttons: each one independently pressed.
            KeyValuePair<string, ushort>[] faces =
            {
                new KeyValuePair<string, ushort>("a", 0x1000), new KeyValuePair<string, ushort>("b", 0x2000),
                new KeyValuePair<string, ushort>("x", 0x4000), new KeyValuePair<string, ushort>("y", 0x8000)
            };
            for (int i = 0; i < faces.Length; i++)
            {
                RenderAuditFrame(manager, photo, cap, new InputSnapshot { Connected = true, Buttons = faces[i].Value }, Path.Combine(outputDirectory, "face-" + faces[i].Key + "-pressed.png"));
                written++;
            }

            // Four directions plus two true simultaneous combinations.
            KeyValuePair<string, ushort>[] dpad =
            {
                new KeyValuePair<string, ushort>("up", 0x0001), new KeyValuePair<string, ushort>("down", 0x0002),
                new KeyValuePair<string, ushort>("left", 0x0004), new KeyValuePair<string, ushort>("right", 0x0008),
                new KeyValuePair<string, ushort>("up-right", 0x0001 | 0x0008), new KeyValuePair<string, ushort>("down-left", 0x0002 | 0x0004)
            };
            for (int i = 0; i < dpad.Length; i++)
            {
                RenderAuditFrame(manager, photo, cap, new InputSnapshot { Connected = true, Buttons = dpad[i].Value }, Path.Combine(outputDirectory, "dpad-" + dpad[i].Key + ".png"));
                written++;
            }

            RenderAuditFrame(manager, photo, cap, new InputSnapshot { Connected = true, Buttons = 0x0100 }, Path.Combine(outputDirectory, "shoulder-lb-pressed.png"));
            RenderAuditFrame(manager, photo, cap, new InputSnapshot { Connected = true, Buttons = 0x0200 }, Path.Combine(outputDirectory, "shoulder-rb-pressed.png"));
            written += 2;

            int[] triggerLevels = { 0, 64, 128, 191, 255 };
            for (int i = 0; i < triggerLevels.Length; i++)
            {
                int percent = (int)Math.Round(triggerLevels[i] * 100.0 / 255.0, MidpointRounding.AwayFromZero);
                RenderAuditFrame(manager, photo, cap, new InputSnapshot { Connected = true, LeftTrigger = triggerLevels[i] }, Path.Combine(outputDirectory, "trigger-lt-" + percent.ToString("000", CultureInfo.InvariantCulture) + ".png"));
                RenderAuditFrame(manager, photo, cap, new InputSnapshot { Connected = true, RightTrigger = triggerLevels[i] }, Path.Combine(outputDirectory, "trigger-rt-" + percent.ToString("000", CultureInfo.InvariantCulture) + ".png"));
                written += 2;
            }

            KeyValuePair<string, Point>[] directions =
            {
                new KeyValuePair<string, Point>("center", new Point(0, 0)), new KeyValuePair<string, Point>("up", new Point(0, 1)),
                new KeyValuePair<string, Point>("down", new Point(0, -1)), new KeyValuePair<string, Point>("left", new Point(-1, 0)),
                new KeyValuePair<string, Point>("right", new Point(1, 0)), new KeyValuePair<string, Point>("up-left", new Point(-0.707, 0.707)),
                new KeyValuePair<string, Point>("up-right", new Point(0.707, 0.707)), new KeyValuePair<string, Point>("down-left", new Point(-0.707, -0.707)),
                new KeyValuePair<string, Point>("down-right", new Point(0.707, -0.707))
            };
            for (int i = 0; i < directions.Length; i++)
            {
                int x = (int)Math.Round(directions[i].Value.X * 32767.0, MidpointRounding.AwayFromZero);
                int y = (int)Math.Round(directions[i].Value.Y * 32767.0, MidpointRounding.AwayFromZero);
                RenderAuditFrame(manager, photo, cap, new InputSnapshot { Connected = true, LeftX = x, LeftY = y }, Path.Combine(outputDirectory, "stick-left-" + directions[i].Key + ".png"));
                RenderAuditFrame(manager, photo, cap, new InputSnapshot { Connected = true, RightX = x, RightY = y }, Path.Combine(outputDirectory, "stick-right-" + directions[i].Key + ".png"));
                written += 2;
            }

            // 3 seconds at 30 FPS: both triggers increase together.  The output
            // is intentionally frames; ffmpeg turns them into a portable MP4.
            string videoFrames = Path.Combine(outputDirectory, "dual-trigger-video-frames");
            Directory.CreateDirectory(videoFrames);
            for (int frame = 0; frame < 90; frame++)
            {
                int value = (int)Math.Round(frame * 255.0 / 89.0, MidpointRounding.AwayFromZero);
                RenderAuditFrame(manager, photo, cap, new InputSnapshot { Connected = true, LeftTrigger = value, RightTrigger = value }, Path.Combine(videoFrames, frame.ToString("000", CultureInfo.InvariantCulture) + ".png"));
                written++;
            }
            return "Xbox Overlay Polish audit wrote " + written.ToString(CultureInfo.InvariantCulture) + " WPF frames to " + outputDirectory;
        }

        // Purpose-built proof for the four hand-traced DPad directions. The
        // wireframe frames have no fill or glow and use a 1-screen-pixel pen.
        public static string RenderDPadUpAudit(string outputDirectory) { return RenderDPadAudit(outputDirectory); }

        public static string RenderDPadAudit(string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory)) throw new ArgumentException("Output directory is required.", "outputDirectory");
            Directory.CreateDirectory(outputDirectory);
            XboxRegionManager manager = Load(true);
            BitmapSource photo = LoadEmbeddedPhoto();
            if (photo == null) throw new InvalidOperationException("Embedded Xbox controller image is unavailable.");
            KeyValuePair<string, ushort>[] directions =
            {
                new KeyValuePair<string, ushort>("up", 0x0001), new KeyValuePair<string, ushort>("down", 0x0002),
                new KeyValuePair<string, ushort>("left", 0x0004), new KeyValuePair<string, ushort>("right", 0x0008)
            };
            KeyValuePair<string, ushort>[] diagonals =
            {
                new KeyValuePair<string, ushort>("up-left", 0x0005), new KeyValuePair<string, ushort>("up-right", 0x0009),
                new KeyValuePair<string, ushort>("down-left", 0x0006), new KeyValuePair<string, ushort>("down-right", 0x000A)
            };
            double[] dpiScales = { 1.0, 1.25, 1.5 };
            int written = 0;
            for (int i = 0; i < dpiScales.Length; i++)
            {
                string dpi = ((int)Math.Round(dpiScales[i] * 100.0, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);
                for (int j = 0; j < directions.Length; j++)
                {
                    RenderDPadCloseUp(manager, photo, Path.Combine(outputDirectory, "dpad-" + directions[j].Key + "-wireframe-" + dpi + ".png"), dpiScales[i], "dpad-" + directions[j].Key, 0, true);
                    RenderDPadCloseUp(manager, photo, Path.Combine(outputDirectory, "dpad-" + directions[j].Key + "-active-" + dpi + ".png"), dpiScales[i], null, directions[j].Value, false);
                    written += 2;
                }
                for (int j = 0; j < diagonals.Length; j++)
                {
                    RenderDPadCloseUp(manager, photo, Path.Combine(outputDirectory, "dpad-" + diagonals[j].Key + "-active-" + dpi + ".png"), dpiScales[i], null, diagonals[j].Value, false);
                    written++;
                }
            }
            return "Xbox DPad audit wrote " + written.ToString(CultureInfo.InvariantCulture) + " WPF wireframe and active frames at 100/125/150 DPI scales to " + outputDirectory;
        }

        // LT/RT geometry proof deliberately avoids the normal visual effects
        // in the first two frames.  It lets calibration judge the plastic edge
        // rather than glow size or pressure animation.
        public static string RenderTriggerGeometryAudit(string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory)) throw new ArgumentException("Output directory is required.", "outputDirectory");
            Directory.CreateDirectory(outputDirectory);
            XboxRegionManager manager = Load(true);
            BitmapSource photo = LoadEmbeddedPhoto();
            if (photo == null) throw new InvalidOperationException("Embedded Xbox controller image is unavailable.");
            RenderTriggerCloseUp(manager, photo, Path.Combine(outputDirectory, "trigger-lt-wireframe.png"), "lt", true);
            RenderTriggerCloseUp(manager, photo, Path.Combine(outputDirectory, "trigger-rt-wireframe.png"), "rt", true);
            int[] levels = { 0, 25, 50, 75, 100 };
            for (int i = 0; i < levels.Length; i++)
            {
                double pressure = levels[i] / 100.0;
                RenderTriggerCloseUp(manager, photo, Path.Combine(outputDirectory, "trigger-lt-" + levels[i].ToString("000", CultureInfo.InvariantCulture) + ".png"), "lt", false, pressure);
                RenderTriggerCloseUp(manager, photo, Path.Combine(outputDirectory, "trigger-rt-" + levels[i].ToString("000", CultureInfo.InvariantCulture) + ".png"), "rt", false, pressure);
            }
            return "Xbox trigger mask audit wrote 12 WPF close-up frames to " + outputDirectory;
        }

        private static void RenderTriggerCloseUp(XboxRegionManager manager, BitmapSource photo, string path, string id, bool wireframe, double pressure = 0.50)
        {
            Rect viewport = string.Equals(id, "lt", StringComparison.OrdinalIgnoreCase) ? new Rect(350, 50, 300, 165) : new Rect(885, 50, 300, 165);
            double stageScale = 3.6;
            int width = (int)Math.Ceiling(viewport.Width * stageScale);
            int height = (int)Math.Ceiling(viewport.Height * stageScale);
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(9, 17, 24)), null, new Rect(0, 0, width, height));
                dc.PushTransform(new MatrixTransform(new Matrix(stageScale, 0, 0, stageScale, -viewport.X * stageScale, -viewport.Y * stageScale)));
                manager.DrawPhoto(dc, photo);
                if (wireframe)
                {
                    Color color = string.Equals(id, "lt", StringComparison.OrdinalIgnoreCase) ? Palette.Green : Palette.Blue;
                    // No glow or fill in this frame: the magenta alpha-ring is
                    // the exact source-mask boundary used by the renderer.
                    manager.DrawTriggerMaskBoundary(dc, id, Color.FromRgb(255, 78, 176), 1.0);
                    Rect bounds = manager.GetTriggerMaskBounds(id);
                    Point center = new Point(bounds.Left + bounds.Width * 0.5, bounds.Top + bounds.Height * 0.5);
                    double arm = 4.0 / stageScale;
                    Pen centerPen = new Pen(new SolidColorBrush(color), 1.0 / stageScale);
                    dc.DrawLine(centerPen, new Point(center.X - arm, center.Y), new Point(center.X + arm, center.Y));
                    dc.DrawLine(centerPen, new Point(center.X, center.Y - arm), new Point(center.X, center.Y + arm));
                }
                else manager.DrawActiveFeedback(dc, new InputSnapshot { Connected = true, LeftTrigger = string.Equals(id, "lt", StringComparison.OrdinalIgnoreCase) ? (byte)Math.Round(pressure * 255) : (byte)0, RightTrigger = string.Equals(id, "rt", StringComparison.OrdinalIgnoreCase) ? (byte)Math.Round(pressure * 255) : (byte)0 }, null, string.Equals(id, "lt", StringComparison.OrdinalIgnoreCase) ? pressure : 0, string.Equals(id, "rt", StringComparison.OrdinalIgnoreCase) ? pressure : 0, false);
                dc.Pop();
            }
            RenderTargetBitmap target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            target.Render(visual);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(target));
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None)) encoder.Save(stream);
        }

        private static void RenderDPadCloseUp(XboxRegionManager manager, BitmapSource photo, string path, double dpiScale, string wireframeId, ushort activeButtons, bool wireframe)
        {
            // Calibration camera only: every path remains in its original
            // 1536x1024 stage coordinates; no DPad-specific transform exists.
            Rect viewport = new Rect(500, 400, 220, 218);
            double stageScale = 4.2 * dpiScale;
            int width = (int)Math.Ceiling(viewport.Width * stageScale);
            int height = (int)Math.Ceiling(viewport.Height * stageScale);
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(9, 17, 24)), null, new Rect(0, 0, width, height));
                dc.PushTransform(new MatrixTransform(new Matrix(stageScale, 0, 0, stageScale, -viewport.X * stageScale, -viewport.Y * stageScale)));
                manager.DrawPhoto(dc, photo);
                if (wireframe) DrawDPadCalibrationAdorners(dc, manager, wireframeId, stageScale, null);
                else manager.DrawActiveFeedback(dc, new InputSnapshot { Connected = true, Buttons = activeButtons }, null, 0, 0, false);
                dc.Pop();
            }
            RenderTargetBitmap target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            target.Render(visual);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(target));
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None)) encoder.Save(stream);
        }

        internal static void DrawDPadCalibrationAdorners(DrawingContext dc, XboxRegionManager manager, string id, double stageScale, XboxPathCalibrationHandle selected)
        {
            Geometry geometry = manager.GetGeometry(id);
            XboxRegionDefinition region = manager.GetRegion(id);
            if (geometry == null || region == null) return;
            double pixel = 1.0 / Math.Max(0.001, stageScale);
            Pen outline = new Pen(new SolidColorBrush(Color.FromRgb(255, 78, 176)), pixel);
            outline.LineJoin = PenLineJoin.Round;
            Pen boundsPen = new Pen(new SolidColorBrush(Color.FromArgb(190, Palette.Blue.R, Palette.Blue.G, Palette.Blue.B)), pixel);
            Pen controlPen = new Pen(new SolidColorBrush(Color.FromArgb(165, 184, 196, 208)), pixel);
            controlPen.DashStyle = DashStyles.Dash;
            dc.DrawGeometry(null, outline, geometry);
            Rect bounds = geometry.Bounds;
            dc.DrawRectangle(null, boundsPen, bounds);

            IList<XboxPathCommand> commands = manager.GetPathCommands(id);
            Matrix pathMatrix = manager.GetPathDisplayMatrix(id);
            Point previous = new Point();
            bool hasPrevious = false;
            for (int i = 0; i < commands.Count; i++)
            {
                XboxPathCommand command = commands[i];
                if (command == null || string.Equals(command.Op, "close", StringComparison.OrdinalIgnoreCase)) continue;
                Point anchor = pathMatrix.Transform(new Point(command.X, command.Y));
                if (string.Equals(command.Op, "move", StringComparison.OrdinalIgnoreCase)) { previous = anchor; hasPrevious = true; continue; }
                if (hasPrevious && string.Equals(command.Op, "cubic", StringComparison.OrdinalIgnoreCase))
                {
                    Point c1 = pathMatrix.Transform(new Point(command.C1X, command.C1Y));
                    Point c2 = pathMatrix.Transform(new Point(command.C2X, command.C2Y));
                    dc.DrawLine(controlPen, previous, c1);
                    dc.DrawLine(controlPen, c1, c2);
                    dc.DrawLine(controlPen, c2, anchor);
                }
                previous = anchor; hasPrevious = true;
            }

            List<XboxPathCalibrationHandle> handles = manager.GetPathHandles(id);
            for (int i = 0; i < handles.Count; i++)
            {
                XboxPathCalibrationHandle handle = handles[i];
                bool isSelected = selected != null && selected.CommandIndex == handle.CommandIndex && string.Equals(selected.Role, handle.Role, StringComparison.OrdinalIgnoreCase);
                Color color = string.Equals(handle.Role, "P", StringComparison.OrdinalIgnoreCase) ? Palette.Warning : (string.Equals(handle.Role, "C1", StringComparison.OrdinalIgnoreCase) ? Palette.Green : Palette.Blue);
                double radius = (isSelected ? 5.2 : 3.7) / Math.Max(0.001, stageScale);
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(235, color.R, color.G, color.B)), new Pen(Palette.WindowBrush, pixel), handle.Point, radius, radius);
            }

            Pen centerPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 255, 255)), pixel);
            double arm = 6.0 / Math.Max(0.001, stageScale);
            Point center = new Point(region.CX, region.CY);
            dc.DrawLine(centerPen, new Point(center.X - arm, center.Y), new Point(center.X + arm, center.Y));
            dc.DrawLine(centerPen, new Point(center.X, center.Y - arm), new Point(center.X, center.Y + arm));
        }

        private static void RenderAuditFrame(XboxRegionManager manager, BitmapSource photo, BitmapSource cap, InputSnapshot state, string path)
        {
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(9, 17, 24)), null, new Rect(0, 0, LogicalWidth, LogicalHeight));
                manager.DrawPhoto(dc, photo);
                manager.DrawStickSockets(dc);
                DrawAuditStickCap(dc, manager, "l3", cap, state.LeftNormalizedX, state.LeftNormalizedY, (state.Buttons & 0x0040) != 0 ? 1.0 : 0.0);
                DrawAuditStickCap(dc, manager, "r3", cap, state.RightNormalizedX, state.RightNormalizedY, (state.Buttons & 0x0080) != 0 ? 1.0 : 0.0);
                manager.DrawStickFeedback(dc, state, null, false);
                manager.DrawActiveFeedback(dc, state, null, state.LeftTrigger / 255.0, state.RightTrigger / 255.0, false);
            }
            RenderTargetBitmap target = new RenderTargetBitmap(LogicalWidth, LogicalHeight, 96, 96, PixelFormats.Pbgra32);
            target.Render(visual);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(target));
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None)) encoder.Save(stream);
        }

        private static void DrawAuditStickCap(DrawingContext dc, XboxRegionManager manager, string id, BitmapSource cap, double inputX, double inputY, double pressed)
        {
            if (cap == null) return;
            Point center = manager.GetStickCenter(id);
            Vector travel = manager.GetStickTravel(id);
            Size size = manager.GetStickSize(id);
            Point moved = new Point(center.X + inputX * travel.X, center.Y - inputY * travel.Y + pressed * 2.0);
            dc.DrawImage(cap, new Rect(moved.X - size.Width * 0.5, moved.Y - size.Height * 0.5, size.Width, size.Height));
        }

        private static BitmapSource LoadEmbeddedPhoto()
        {
            return LoadEmbeddedBitmap("ControllerLab.Assets.controller.png");
        }

        public static BitmapSource LoadControllerPhotoForCalibration()
        {
            return LoadEmbeddedPhoto();
        }

        private static BitmapSource LoadEmbeddedBitmap(string resourceName)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream == null) return null;
            try
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            finally { stream.Dispose(); }
        }

        private void LoadTriggerMasks()
        {
            leftTriggerMask = LoadEmbeddedBitmap("ControllerLab.Assets.LeftTopTriggerMask.png");
            rightTriggerMask = LoadEmbeddedBitmap("ControllerLab.Assets.RightTopTriggerMask.png");
            if (leftTriggerMask == null || rightTriggerMask == null)
                throw new InvalidDataException("Xbox trigger alpha mask resources are unavailable.");
            ValidateTriggerMask("lt", leftTriggerMask, "LT");
            ValidateTriggerMask("rt", rightTriggerMask, "RT");
            // These variants are generated directly from the supplied PNG alpha
            // in memory. No geometry, crop or hand-drawn outline is involved.
            leftTriggerMaskEdge = CreateAlphaVariant(leftTriggerMask, true);
            rightTriggerMaskEdge = CreateAlphaVariant(rightTriggerMask, true);
            leftTriggerMaskGlow = CreateAlphaVariant(leftTriggerMask, false);
            rightTriggerMaskGlow = CreateAlphaVariant(rightTriggerMask, false);
            leftTriggerMaskBounds = FindMaskStageBounds("lt", leftTriggerMask);
            rightTriggerMaskBounds = FindMaskStageBounds("rt", rightTriggerMask);
            if (leftTriggerMaskBounds.IsEmpty || rightTriggerMaskBounds.IsEmpty)
                throw new InvalidDataException("Xbox trigger alpha masks contain no visible pixels.");
        }

        private void ValidateTriggerMask(string id, BitmapSource mask, string name)
        {
            XboxRegionDefinition region = GetRegion(id);
            if (!IsImageMaskTrigger(region) || region.TriggerScale <= 0)
                throw new InvalidDataException(name + " trigger mask is missing a valid source-image layout.");
            if (FindAlphaPixelBounds(mask).IsEmpty)
                throw new InvalidDataException(name + " trigger mask contains no transparent-cutout alpha.");
        }

        private static Rect FindAlphaPixelBounds(BitmapSource source)
        {
            FormatConvertedBitmap converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int stride = converted.PixelWidth * 4;
            byte[] pixels = new byte[stride * converted.PixelHeight];
            converted.CopyPixels(pixels, stride, 0);
            int left = converted.PixelWidth;
            int top = converted.PixelHeight;
            int right = -1;
            int bottom = -1;
            for (int y = 0; y < converted.PixelHeight; y++)
            {
                int row = y * stride;
                for (int x = 0; x < converted.PixelWidth; x++)
                {
                    if (pixels[row + x * 4 + 3] <= 16) continue;
                    if (x < left) left = x;
                    if (x > right) right = x;
                    if (y < top) top = y;
                    if (y > bottom) bottom = y;
                }
            }
            if (right < left || bottom < top) return Rect.Empty;
            return new Rect(left, top, right - left + 1, bottom - top + 1);
        }

        private Rect FindMaskStageBounds(string id, BitmapSource source)
        {
            XboxRegionDefinition region = GetRegion(id);
            Rect pixels = FindAlphaPixelBounds(source);
            if (region == null || pixels.IsEmpty) return Rect.Empty;
            Point canvasOrigin = GetTriggerMaskCanvasOrigin(region, pixels);
            return new Rect(canvasOrigin.X + pixels.X * region.TriggerScale, canvasOrigin.Y + pixels.Y * region.TriggerScale, pixels.Width * region.TriggerScale, pixels.Height * region.TriggerScale);
        }

        // The full 1536x1024 PNG is intentionally preserved.  Its transparent
        // margin is not a positioning reference: the visible alpha-bounds
        // centre is placed on the stage anchor and then receives only a small
        // calibration offset.
        private static Point GetTriggerMaskCanvasOrigin(XboxRegionDefinition region, Rect alphaPixels)
        {
            double scale = region.TriggerScale;
            Point alphaCenter = new Point(alphaPixels.X + alphaPixels.Width * 0.5, alphaPixels.Y + alphaPixels.Height * 0.5);
            return new Point(region.TriggerAnchorX + region.TriggerOffsetX - alphaCenter.X * scale, region.TriggerAnchorY + region.TriggerOffsetY - alphaCenter.Y * scale);
        }

        private static BitmapSource CreateAlphaVariant(BitmapSource source, bool innerEdge)
        {
            FormatConvertedBitmap converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int width = converted.PixelWidth;
            int height = converted.PixelHeight;
            int stride = width * 4;
            byte[] input = new byte[stride * height];
            byte[] output = new byte[stride * height];
            converted.CopyPixels(input, stride, 0);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * stride + x * 4;
                    bool active = input[index + 3] > 32;
                    byte alpha = 0;
                    if (innerEdge && active)
                    {
                        bool boundary = false;
                        for (int dy = -1; dy <= 1 && !boundary; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx;
                                int ny = y + dy;
                                if (nx < 0 || nx >= width || ny < 0 || ny >= height || input[ny * stride + nx * 4 + 3] <= 32) { boundary = true; break; }
                            }
                        if (boundary) alpha = 255;
                    }
                    else if (!innerEdge && !active)
                    {
                        int best = 5;
                        for (int dy = -2; dy <= 2; dy++)
                            for (int dx = -2; dx <= 2; dx++)
                            {
                                int distance = dx * dx + dy * dy;
                                int nx = x + dx;
                                int ny = y + dy;
                                if (distance <= 4 && nx >= 0 && nx < width && ny >= 0 && ny < height && input[ny * stride + nx * 4 + 3] > 32 && distance < best) best = distance;
                            }
                        if (best <= 4) alpha = (byte)(best <= 1 ? 78 : 38);
                    }
                    if (alpha == 0) continue;
                    output[index] = 255;
                    output[index + 1] = 255;
                    output[index + 2] = 255;
                    output[index + 3] = alpha;
                }
            }
            BitmapSource result = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, output, stride);
            result.Freeze();
            return result;
        }

        public BitmapSource GetTriggerMask(string id)
        {
            return string.Equals(id, "lt", StringComparison.OrdinalIgnoreCase) ? leftTriggerMask :
                (string.Equals(id, "rt", StringComparison.OrdinalIgnoreCase) ? rightTriggerMask : null);
        }

        public BitmapSource GetTriggerMaskEdge(string id)
        {
            return string.Equals(id, "lt", StringComparison.OrdinalIgnoreCase) ? leftTriggerMaskEdge :
                (string.Equals(id, "rt", StringComparison.OrdinalIgnoreCase) ? rightTriggerMaskEdge : null);
        }

        public BitmapSource GetTriggerMaskGlow(string id)
        {
            return string.Equals(id, "lt", StringComparison.OrdinalIgnoreCase) ? leftTriggerMaskGlow :
                (string.Equals(id, "rt", StringComparison.OrdinalIgnoreCase) ? rightTriggerMaskGlow : null);
        }

        public Rect GetTriggerMaskBounds(string id)
        {
            return string.Equals(id, "lt", StringComparison.OrdinalIgnoreCase) ? leftTriggerMaskBounds :
                (string.Equals(id, "rt", StringComparison.OrdinalIgnoreCase) ? rightTriggerMaskBounds : Rect.Empty);
        }

        public Point GetTriggerTargetAnchor(string id)
        {
            XboxRegionDefinition region = GetRegion(id);
            return region == null ? new Point() : new Point(region.TriggerAnchorX, region.TriggerAnchorY);
        }

        public Point GetTriggerCurrentAnchor(string id)
        {
            XboxRegionDefinition region = GetRegion(id);
            return region == null ? new Point() : new Point(region.TriggerAnchorX + region.TriggerOffsetX, region.TriggerAnchorY + region.TriggerOffsetY);
        }

        private void RefreshTriggerMaskBounds()
        {
            if (leftTriggerMask != null) leftTriggerMaskBounds = FindMaskStageBounds("lt", leftTriggerMask);
            if (rightTriggerMask != null) rightTriggerMaskBounds = FindMaskStageBounds("rt", rightTriggerMask);
        }

        public bool MoveTriggerMask(string id, double dx, double dy)
        {
            XboxRegionDefinition region = GetRegion(id);
            if (!IsImageMaskTrigger(region)) return false;
            region.TriggerOffsetX += dx;
            region.TriggerOffsetY += dy;
            modified.Add(id);
            RefreshTriggerMaskBounds();
            return true;
        }

        public bool ScaleTriggerMask(string id, double multiplier)
        {
            XboxRegionDefinition region = GetRegion(id);
            if (!IsImageMaskTrigger(region) || GetTriggerMask(id) == null || multiplier <= 0) return false;
            region.TriggerScale = Math.Max(0.05, Math.Min(0.60, region.TriggerScale * multiplier));
            modified.Add(id);
            RefreshTriggerMaskBounds();
            return true;
        }

        public bool AdjustTriggerMaskOpacity(string id, double delta)
        {
            XboxRegionDefinition region = GetRegion(id);
            if (!IsImageMaskTrigger(region)) return false;
            double current = region.TriggerMaskOpacity <= 0 ? 1.0 : region.TriggerMaskOpacity;
            region.TriggerMaskOpacity = Math.Max(0.10, Math.Min(1.0, current + delta));
            modified.Add(id);
            return true;
        }

        private static double GetTriggerMaskOpacity(XboxRegionDefinition region)
        {
            return region == null || region.TriggerMaskOpacity <= 0 ? 1.0 : Math.Max(0.10, Math.Min(1.0, region.TriggerMaskOpacity));
        }

        public void DrawTriggerMaskBoundary(DrawingContext dc, string id, Color color, double opacity)
        {
            BitmapSource edge = GetTriggerMaskEdge(id);
            if (edge == null) return;
            DrawThroughMask(dc, id, edge, new SolidColorBrush(Color.FromArgb((byte)Math.Max(0, Math.Min(255, opacity * GetTriggerMaskOpacity(GetRegion(id)) * 255)), color.R, color.G, color.B)));
        }

        private ImageBrush CreateTriggerMaskBrush(string id, ImageSource mask)
        {
            XboxRegionDefinition region = GetRegion(id);
            if (region == null || !(mask is BitmapSource)) return null;
            BitmapSource bitmap = (BitmapSource)mask;
            ImageBrush brush = new ImageBrush(mask);
            brush.Stretch = Stretch.Fill;
            brush.AlignmentX = AlignmentX.Left;
            brush.AlignmentY = AlignmentY.Top;
            brush.ViewboxUnits = BrushMappingMode.Absolute;
            brush.Viewbox = new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight);
            brush.ViewportUnits = BrushMappingMode.Absolute;
            Rect alphaPixels = FindAlphaPixelBounds(bitmap);
            if (alphaPixels.IsEmpty) return null;
            Point canvasOrigin = GetTriggerMaskCanvasOrigin(region, alphaPixels);
            brush.Viewport = new Rect(canvasOrigin.X, canvasOrigin.Y, bitmap.PixelWidth * region.TriggerScale, bitmap.PixelHeight * region.TriggerScale);
            brush.Freeze();
            return brush;
        }

        // The rectangle below is only a full-stage colour field.  Alpha is
        // supplied exclusively by the photographed trigger matte, so it is not
        // a rectangular progress indicator and cannot escape the true shape.
        private void DrawThroughMask(DrawingContext dc, string id, ImageSource mask, Brush field)
        {
            ImageBrush alphaMask = CreateTriggerMaskBrush(id, mask);
            if (alphaMask == null) return;
            dc.PushOpacityMask(alphaMask);
            dc.DrawRectangle(field, null, new Rect(0, 0, LogicalWidth, LogicalHeight));
            dc.Pop();
        }

        public static Matrix CreateStageMatrix(double availableWidth, double availableHeight)
        {
            double scale = Math.Min(availableWidth / LogicalWidth, availableHeight / LogicalHeight);
            double x = Math.Round((availableWidth - LogicalWidth * scale) * 0.5, MidpointRounding.AwayFromZero);
            double y = Math.Round((availableHeight - LogicalHeight * scale) * 0.5, MidpointRounding.AwayFromZero);
            return new Matrix(scale, 0, 0, scale, x, y);
        }

        public static Point SourceToStage(double x, double y)
        {
            return new Point(x * SourceScale, SourceTop + y * SourceScale);
        }

        public Geometry GetGeometry(string id)
        {
            Geometry geometry;
            return geometries != null && geometries.TryGetValue(id, out geometry) ? geometry : null;
        }

        public XboxRegionDefinition GetRegion(string id)
        {
            XboxRegionDefinition region;
            return byId != null && byId.TryGetValue(id, out region) ? region : null;
        }

        public void DrawPhoto(DrawingContext dc, ImageSource photo)
        {
            if (photo == null) return;
            dc.DrawImage(photo, new Rect(0, SourceTop, LogicalWidth, SourceImageHeight * SourceScale));
        }

        public void DrawActiveFeedback(DrawingContext dc, InputSnapshot state, Func<int, double> levelForMask, double leftTrigger, double rightTrigger, bool reducedMotion)
        {
            DrawButton(dc, "a", GetLevel(state, levelForMask, 0x1000), Palette.Blue, reducedMotion);
            DrawButton(dc, "b", GetLevel(state, levelForMask, 0x2000), Palette.Blue, reducedMotion);
            DrawButton(dc, "x", GetLevel(state, levelForMask, 0x4000), Palette.Blue, reducedMotion);
            DrawButton(dc, "y", GetLevel(state, levelForMask, 0x8000), Palette.Blue, reducedMotion);
            DrawButton(dc, "dpad-up", GetLevel(state, levelForMask, 0x0001), Palette.Blue, reducedMotion);
            DrawButton(dc, "dpad-down", GetLevel(state, levelForMask, 0x0002), Palette.Blue, reducedMotion);
            DrawButton(dc, "dpad-left", GetLevel(state, levelForMask, 0x0004), Palette.Blue, reducedMotion);
            DrawButton(dc, "dpad-right", GetLevel(state, levelForMask, 0x0008), Palette.Blue, reducedMotion);
            DrawButton(dc, "view", GetLevel(state, levelForMask, 0x0020), Palette.Blue, reducedMotion);
            DrawButton(dc, "menu", GetLevel(state, levelForMask, 0x0010), Palette.Blue, reducedMotion);
            DrawButton(dc, "guide", GetLevel(state, levelForMask, 0x0400), Palette.Blue, reducedMotion);
            DrawTopControls(dc, state, levelForMask, leftTrigger, rightTrigger, reducedMotion);
        }

        // Each side of the Xbox top shell owns exactly one photographed alpha
        // mask. LB/LT drive the same left mask, RB/RT the same right mask. The
        // inputs stay independent; only the hardware visual surface is unified
        // so there is no synthetic shoulder-to-trigger seam.
        private void DrawTopControls(DrawingContext dc, InputSnapshot state, Func<int, double> levelForMask, double leftTrigger, double rightTrigger, bool reducedMotion)
        {
            // Shoulder input deliberately bypasses the button animation tail:
            // these are digital switches and their top-shell feedback should
            // react on the first sampled XInput bit, not ease in a frame later.
            bool leftShoulderPressed = state != null && (state.Buttons & 0x0100) != 0;
            bool rightShoulderPressed = state != null && (state.Buttons & 0x0200) != 0;
            // Both shoulder switches use the same blue feedback. Both analogue
            // triggers use green so their pressure state reads as one system;
            // RT retains its independent right-to-left fill direction.
            DrawTopControlMask(dc, "lt", leftShoulderPressed ? 1.0 : 0.0, leftTrigger, leftShoulderPressed ? Palette.Blue : Palette.Green, reducedMotion);
            DrawTopControlMask(dc, "rt", rightShoulderPressed ? 1.0 : 0.0, rightTrigger, rightShoulderPressed ? Palette.Blue : Palette.Green, reducedMotion);
        }

        // Kept separate from the rest of the overlay so the moving cap can be
        // rendered first and the fixed socket ring can remain above it.
        public void DrawStickFeedback(DrawingContext dc, InputSnapshot state, Func<int, double> levelForMask, bool reducedMotion)
        {
            if (state == null) return;
            DrawStick(dc, "l3", state.LeftNormalizedX, state.LeftNormalizedY, GetLevel(state, levelForMask, 0x0040), reducedMotion);
            DrawStick(dc, "r3", state.RightNormalizedX, state.RightNormalizedY, GetLevel(state, levelForMask, 0x0080), reducedMotion);
        }

        // The photo has a static thumb cap.  Replace only that central cap area
        // with a fixed recessed socket; ControllerVisual draws the alpha-isolated
        // cap afterwards at its real, live position.  The socket/ring never moves.
        public void DrawStickSockets(DrawingContext dc)
        {
            DrawStickSocket(dc, "l3");
            DrawStickSocket(dc, "r3");
        }

        private void DrawStickSocket(DrawingContext dc, string id)
        {
            XboxRegionDefinition region = GetRegion(id);
            if (region == null) return;
            Point center = GetStickCenter(id);
            double radius = Math.Min(region.RingWidth * 0.5 - 8.0, Math.Max(region.CapWidth, region.CapHeight) * 0.5 + 8.0);
            if (radius <= 0) return;

            RadialGradientBrush socket = new RadialGradientBrush();
            socket.GradientOrigin = new Point(0.43, 0.37);
            socket.Center = new Point(0.5, 0.5);
            socket.RadiusX = 0.5;
            socket.RadiusY = 0.5;
            socket.GradientStops.Add(new GradientStop(Color.FromRgb(31, 35, 38), 0.0));
            socket.GradientStops.Add(new GradientStop(Color.FromRgb(17, 20, 23), 0.63));
            socket.GradientStops.Add(new GradientStop(Color.FromRgb(8, 10, 12), 1.0));
            socket.Freeze();
            Pen rim = new Pen(new SolidColorBrush(Color.FromArgb(145, 2, 5, 7)), 1.45);
            rim.Freeze();
            dc.DrawEllipse(socket, rim, center, radius, radius);
        }

        private static double GetLevel(InputSnapshot state, Func<int, double> levelForMask, int mask)
        {
            return state != null && (state.Buttons & mask) != 0 ? 1.0 : (levelForMask == null ? 0.0 : levelForMask(mask));
        }

        private void DrawButton(DrawingContext dc, string id, double level, Color color, bool reducedMotion)
        {
            if (level < 0.008) return;
            Geometry geometry = GetGeometry(id);
            XboxRegionDefinition region = GetRegion(id);
            if (geometry == null || region == null) return;
            byte edge = (byte)Math.Min(210, 92 + level * 110);
            double edgeWidth = region.EdgeStrokeWidth > 0 ? region.EdgeStrokeWidth : 1.55;
            double outerWidth = region.OuterGlowWidth > 0 ? region.OuterGlowWidth : 4.8;
            byte outerAlpha = (byte)Math.Min(46, Math.Max(5, region.OuterGlowOpacity * level));
            // A low-alpha outer halo is deliberately rendered before the clipped
            // edge stroke.  Its 2px reach is visual only; the fill cannot leave
            // the measured physical button contour.
            if (!reducedMotion && outerAlpha > 0)
                dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(outerAlpha, color.R, color.G, color.B)), outerWidth), geometry);
            // Edge light stays inside the physical hit outline. It is clipped so a
            // pressed face button never gains a larger visible silhouette.
            dc.PushClip(geometry);
            if (!reducedMotion) dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb((byte)(20 + level * 25), color.R, color.G, color.B)), Math.Max(3.0, edgeWidth * 2.2)), geometry);
            dc.DrawGeometry(new SolidColorBrush(Color.FromArgb((byte)(8 + level * 17), color.R, color.G, color.B)), new Pen(new SolidColorBrush(Color.FromArgb(edge, color.R, color.G, color.B)), edgeWidth), geometry);
            dc.Pop();
        }

        private void DrawTopControlMask(DrawingContext dc, string id, double shoulderLevel, double triggerLevel, Color color, bool reducedMotion)
        {
            double level = Math.Max(shoulderLevel, triggerLevel);
            if (level < 0.004) return;
            XboxRegionDefinition region = GetRegion(id);
            BitmapSource mask = GetTriggerMask(id);
            BitmapSource edgeMask = GetTriggerMaskEdge(id);
            BitmapSource glowMask = GetTriggerMaskGlow(id);
            if (region == null || mask == null || edgeMask == null || glowMask == null) return;
            // A digital shoulder press illuminates the full physical side with
            // no easing delay. An analogue trigger still follows its pressure
            // through the same mask; RT remains right-to-left by configuration.
            double progress = shoulderLevel > 0.008 ? 1.0 : ApplyEasing(triggerLevel, region.Easing);
            double opacity = GetTriggerMaskOpacity(region);
            dc.PushOpacity(opacity);
            DrawThroughMask(dc, id, mask, CreateTriggerPressureField(region, GetTriggerMaskBounds(id), progress, color));
            // This ring was generated from the same alpha matte. It keeps a
            // precise edge without an editable PathGeometry or a halo that
            // obscures whether the mask fits the physical trigger.
            DrawThroughMask(dc, id, edgeMask, new SolidColorBrush(Color.FromArgb((byte)Math.Min(205, 92 + level * 86), color.R, color.G, color.B)));
            if (!reducedMotion && region.OuterGlowOpacity > 0)
                DrawThroughMask(dc, id, glowMask, new SolidColorBrush(Color.FromArgb((byte)Math.Min(54, region.OuterGlowOpacity * level), color.R, color.G, color.B)));
            dc.Pop();
        }

        private static LinearGradientBrush CreateTriggerPressureField(XboxRegionDefinition region, Rect bounds, double progress, Color color)
        {
            double clamped = Math.Max(0, Math.Min(1, progress));
            bool rightToLeft = string.Equals(region.FillDirection, "rightToLeft", StringComparison.OrdinalIgnoreCase) || string.Equals(region.FillOrigin, "right", StringComparison.OrdinalIgnoreCase);
            double fadeStart = Math.Max(0, clamped - 0.020);
            double fadeEnd = Math.Min(1, clamped + 0.014);
            LinearGradientBrush brush = new LinearGradientBrush();
            brush.MappingMode = BrushMappingMode.Absolute;
            brush.StartPoint = rightToLeft ? new Point(bounds.Right, bounds.Top) : new Point(bounds.Left, bounds.Top);
            brush.EndPoint = rightToLeft ? new Point(bounds.Left, bounds.Top) : new Point(bounds.Right, bounds.Top);
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(18, color.R, color.G, color.B), 0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(36 + clamped * 66), color.R, color.G, color.B), fadeStart));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), fadeEnd));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1));
            return brush;
        }

        private static double ApplyEasing(double value, string easing)
        {
            double clamped = Math.Max(0, Math.Min(1, value));
            // Input amplitude must remain truthful.  The current visual easing is
            // linear; temporal smoothing is performed before this renderer.
            return clamped;
        }

        private void DrawStick(DrawingContext dc, string id, double x, double y, double pressed, bool reducedMotion)
        {
            Geometry geometry = GetStickRingGeometry(id);
            XboxRegionDefinition region = GetRegion(id);
            if (geometry == null || region == null) return;
            double magnitude = Math.Min(1.0, Math.Sqrt(x * x + y * y));
            byte ringAlpha = (byte)(70 + Math.Max(magnitude, pressed) * 120);
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(ringAlpha, Palette.Blue.R, Palette.Blue.G, Palette.Blue.B)), 1.65), geometry);
            if ((magnitude > 0.01 || pressed > 0.01) && !reducedMotion)
            {
                dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(26, Palette.Blue.R, Palette.Blue.G, Palette.Blue.B)), 3.5), geometry);
            }
        }

        public Point GetStickCenter(string id)
        {
            XboxRegionDefinition region = GetRegion(id);
            return region == null ? new Point(LogicalWidth / 2.0, LogicalHeight / 2.0) : new Point(region.MotionCenterX, region.MotionCenterY);
        }

        public Size GetStickSize(string id)
        {
            XboxRegionDefinition region = GetRegion(id);
            return region == null ? new Size(122, 122) : new Size(region.CapWidth, region.CapHeight);
        }

        public Vector GetStickTravel(string id)
        {
            XboxRegionDefinition region = GetRegion(id);
            return region == null ? new Vector(21, 21) : new Vector(region.TravelX, region.TravelY);
        }

        public Geometry GetStickRingGeometry(string id)
        {
            XboxRegionDefinition region = GetRegion(id);
            if (region == null) return null;
            // The fixed illumination ring belongs to the physical socket centre.
            // The independently sampled cap centre is used only by GetStickCenter.
            EllipseGeometry ring = new EllipseGeometry(new Point(region.CX, region.CY), region.RingWidth / 2.0, region.RingHeight / 2.0);
            ring.Freeze();
            return ring;
        }

        public Point HitTest(Point stagePoint)
        {
            return stagePoint;
        }

        public string HitTestRegion(Point stagePoint)
        {
            if (document == null || document.Regions == null) return null;
            for (int i = document.Regions.Count - 1; i >= 0; i--)
            {
                XboxRegionDefinition region = document.Regions[i];
                Geometry geometry = GetGeometry(region.Id);
                if (geometry != null && geometry.FillContains(stagePoint)) return region.Id;
            }
            return null;
        }

        public void MoveRegion(string id, double dx, double dy)
        {
            XboxRegionDefinition region = GetRegion(id);
            if (region == null) return;
            if (IsImageMaskTrigger(region)) { MoveTriggerMask(id, dx, dy); return; }
            if (IsDPadTransformRegion(region)) { MoveDPadTransform(id, dx, dy); return; }
            if (IsTopControl(region)) { MoveTopControlTransform(id, dx, dy); return; }
            region.CX += dx;
            region.CY += dy;
            if (region.MotionCenterX != 0 || region.MotionCenterY != 0)
            {
                region.MotionCenterX += dx;
                region.MotionCenterY += dy;
            }
            if (region.Points != null)
            {
                for (int i = 0; i < region.Points.Count; i++)
                {
                    region.Points[i].X += dx;
                    region.Points[i].Y += dy;
                }
            }
            if (region.PathCommands != null)
            {
                for (int i = 0; i < region.PathCommands.Count; i++) TranslatePathCommand(region.PathCommands[i], dx, dy);
            }
            modified.Add(id);
            Rebuild();
        }

        public void ScaleRegion(string id, double multiplier)
        {
            XboxRegionDefinition region = GetRegion(id);
            if (region == null || multiplier <= 0) return;
            if (IsImageMaskTrigger(region)) { ScaleTriggerMask(id, multiplier); return; }
            if (IsDPadTransformRegion(region)) { ScaleDPadTransform(id, multiplier, multiplier); return; }
            if (IsTopControl(region)) { ScaleTopControlTransform(id, multiplier, multiplier); return; }
            region.Width = Math.Max(4, region.Width * multiplier);
            region.Height = Math.Max(4, region.Height * multiplier);
            region.CornerRadius = Math.Max(0, region.CornerRadius * multiplier);
            if (region.RingWidth > 0) region.RingWidth *= multiplier;
            if (region.RingHeight > 0) region.RingHeight *= multiplier;
            if (region.CapWidth > 0) region.CapWidth *= multiplier;
            if (region.CapHeight > 0) region.CapHeight *= multiplier;
            if (region.TravelX > 0) region.TravelX *= multiplier;
            if (region.TravelY > 0) region.TravelY *= multiplier;
            if (region.Points != null)
            {
                for (int i = 0; i < region.Points.Count; i++)
                {
                    region.Points[i].X = region.CX + (region.Points[i].X - region.CX) * multiplier;
                    region.Points[i].Y = region.CY + (region.Points[i].Y - region.CY) * multiplier;
                }
            }
            if (region.PathCommands != null)
            {
                for (int i = 0; i < region.PathCommands.Count; i++) ScalePathCommand(region.PathCommands[i], region.CX, region.CY, multiplier);
            }
            modified.Add(id);
            Rebuild();
        }

        public void RotateRegion(string id, double degrees)
        {
            XboxRegionDefinition region = GetRegion(id);
            if (region == null) return;
            if (IsDPadTransformRegion(region)) { RotateDPadTransform(id, degrees); return; }
            if (IsTopControl(region)) { RotateTopControlTransform(id, degrees); return; }
            region.Rotation += degrees;
            modified.Add(id);
            Rebuild();
        }

        public void ResetRegion(string id)
        {
            if (defaults == null || document == null) return;
            XboxRegionDefinition source = Find(defaults.Regions, id);
            XboxRegionDefinition current = Find(document.Regions, id);
            if (source == null || current == null) return;
            int index = document.Regions.IndexOf(current);
            document.Regions[index] = Clone(source);
            modified.Remove(id);
            Rebuild();
            if (IsImageMaskTrigger(source)) RefreshTriggerMaskBounds();
        }

        public bool SaveUserOverride(out string message)
        {
            try
            {
                string directory = Path.GetDirectoryName(OverridePath);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                XboxRegionsOverride output = new XboxRegionsOverride
                {
                    SchemaVersion = SchemaVersion,
                    SourceImage = defaults.SourceImage,
                    ImageWidth = defaults.ImageWidth,
                    ImageHeight = defaults.ImageHeight,
                    LogicalWidth = defaults.LogicalWidth,
                    LogicalHeight = defaults.LogicalHeight,
                    Regions = new List<XboxRegionDefinition>()
                };
                foreach (string id in modified)
                {
                    XboxRegionDefinition region = GetRegion(id);
                    if (region != null) output.Regions.Add(Clone(region));
                }
                WriteFile(OverridePath, output);
                message = "Xbox 用户轮廓覆盖已保存：" + OverridePath;
                return true;
            }
            catch (Exception ex)
            {
                message = "无法保存 Xbox 用户轮廓覆盖：" + ex.Message;
                return false;
            }
        }

        public string Describe(string id)
        {
            XboxRegionDefinition region = GetRegion(id);
            if (region == null) return "未选择区域";
            Geometry geometry = GetGeometry(id);
            Rect bounds = IsImageMaskTrigger(region) ? GetTriggerMaskBounds(id) : (geometry == null ? Rect.Empty : geometry.Bounds);
            StringBuilder result = new StringBuilder();
            result.AppendFormat(CultureInfo.InvariantCulture, "{0}\n{1}\n中心 X {2:0.00}, Y {3:0.00}\n宽 {4:0.00}, 高 {5:0.00}\n旋转 {6:0.00}°\nScaleX {7:0.000}, ScaleY {8:0.000}\n边界 X {9:0.00}, Y {10:0.00}, W {11:0.00}, H {12:0.00}", region.Id, region.Kind, region.CX, region.CY, region.Width, region.Height, region.Rotation, region.ScaleX == 0 ? 1 : region.ScaleX, region.ScaleY == 0 ? 1 : region.ScaleY, bounds.X, bounds.Y, bounds.Width, bounds.Height);
            if (IsDPadTransformRegion(region)) result.AppendFormat(CultureInfo.InvariantCulture, "\n{0}Transform\n基础路径 dpad-up\nOffsetX {1:0.00}, OffsetY {2:0.00}\nRotationOffset {3:0.00}°", region.Id, region.OffsetX, region.OffsetY, region.RotationOffset);
            if (IsImageMaskTrigger(region)) result.AppendFormat(CultureInfo.InvariantCulture, "\nSource alpha mask {0}\nAlpha target anchor X {1:0.00}, Y {2:0.00}\nCalibration OffsetX {3:0.00}, OffsetY {4:0.00}\nCurrent alpha anchor X {5:0.00}, Y {6:0.00}\nTriggerScale {7:0.000}, TriggerMaskOpacity {8:0.00}\nThe complete transparent PNG canvas is mapped from its alpha-bounds centre without crop, rotation, or non-uniform stretch.", region.GeometryPath, region.TriggerAnchorX, region.TriggerAnchorY, region.TriggerOffsetX, region.TriggerOffsetY, region.TriggerAnchorX + region.TriggerOffsetX, region.TriggerAnchorY + region.TriggerOffsetY, region.TriggerScale, GetTriggerMaskOpacity(region));
            else if (IsTriggerTransformRegion(region)) result.AppendFormat(CultureInfo.InvariantCulture, "\nTrigger base {0}\nOffsetX {1:0.00}, OffsetY {2:0.00}\nRotationOffset {3:0.00} deg", IsDerivedTrigger(region) ? region.BaseRegion + " mirrored" : "lt", region.OffsetX, region.OffsetY, region.RotationOffset);
            if (region.PathCommands != null && region.PathCommands.Count > 0)
            {
                result.Append("\nPath commands:");
                for (int i = 0; i < region.PathCommands.Count; i++)
                {
                    XboxPathCommand command = region.PathCommands[i];
                    result.AppendFormat(CultureInfo.InvariantCulture, "\n{0}: {1}  P({2:0.00},{3:0.00})  C1({4:0.00},{5:0.00})  C2({6:0.00},{7:0.00})", i, command.Op, command.X, command.Y, command.C1X, command.C1Y, command.C2X, command.C2Y);
                }
            }
            return result.ToString();
        }

        public List<XboxPathCalibrationHandle> GetPathHandles(string id)
        {
            List<XboxPathCalibrationHandle> result = new List<XboxPathCalibrationHandle>();
            XboxRegionDefinition region = GetRegion(id);
            if (region == null) return result;
            XboxRegionDefinition handleBase = region;
            Matrix derivedMatrix = Matrix.Identity;
            if (IsDPadTransformRegion(region))
            {
                handleBase = GetRegion("dpad-up");
                if (handleBase == null || handleBase.PathCommands == null) return result;
                derivedMatrix = CreateDPadTransformMatrix(handleBase, region);
            }
            else if (IsDerivedTrigger(region))
            {
                handleBase = GetRegion(region.BaseRegion);
                if (handleBase == null || handleBase.PathCommands == null) return result;
                derivedMatrix = CreateDerivedTriggerTransformMatrix(region);
            }
            else if (handleBase.PathCommands == null) return result;
            for (int i = 0; i < handleBase.PathCommands.Count; i++)
            {
                XboxPathCommand command = handleBase.PathCommands[i];
                if (command == null || string.Equals(command.Op, "close", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(command.Op, "cubic", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(new XboxPathCalibrationHandle { CommandIndex = i, Role = "C1", Point = derivedMatrix.Transform(new Point(command.C1X, command.C1Y)) });
                    result.Add(new XboxPathCalibrationHandle { CommandIndex = i, Role = "C2", Point = derivedMatrix.Transform(new Point(command.C2X, command.C2Y)) });
                }
                result.Add(new XboxPathCalibrationHandle { CommandIndex = i, Role = "P", Point = derivedMatrix.Transform(new Point(command.X, command.Y)) });
            }
            return result;
        }

        public bool MovePathHandle(string id, int commandIndex, string role, double dx, double dy)
        {
            XboxRegionDefinition region = GetRegion(id);
            // DPad contour data is locked after the base-path correction.  All
            // four directions, including Up, are calibrated by transform only.
            if (IsDPadTransformRegion(region) || IsDerivedTrigger(region)) return false;
            if (region == null || region.PathCommands == null || commandIndex < 0 || commandIndex >= region.PathCommands.Count) return false;
            XboxPathCommand command = region.PathCommands[commandIndex];
            if (command == null || string.Equals(command.Op, "close", StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(role, "C1", StringComparison.OrdinalIgnoreCase)) { command.C1X += dx; command.C1Y += dy; }
            else if (string.Equals(role, "C2", StringComparison.OrdinalIgnoreCase)) { command.C2X += dx; command.C2Y += dy; }
            else { command.X += dx; command.Y += dy; }
            SynchronizePathMetrics(region);
            modified.Add(id);
            Rebuild();
            return true;
        }

        public bool IsDerivedDPadRegion(string id) { return IsDerivedDPad(GetRegion(id)); }
        public bool IsDPadTransformRegion(string id) { return IsDPadTransformRegion(GetRegion(id)); }

        public bool MoveDPadTransform(string id, double dx, double dy)
        {
            XboxRegionDefinition region = GetRegion(id);
            if (!IsDPadTransformRegion(region)) return false;
            region.OffsetX += dx;
            region.OffsetY += dy;
            modified.Add(id);
            Rebuild();
            SynchronizeDPadMetrics(region);
            return true;
        }

        public bool ScaleDPadTransform(string id, double scaleX, double scaleY)
        {
            XboxRegionDefinition region = GetRegion(id);
            if (!IsDPadTransformRegion(region) || scaleX <= 0 || scaleY <= 0) return false;
            region.ScaleX = Math.Max(0.10, (region.ScaleX == 0 ? 1.0 : region.ScaleX) * scaleX);
            region.ScaleY = Math.Max(0.10, (region.ScaleY == 0 ? 1.0 : region.ScaleY) * scaleY);
            modified.Add(id);
            Rebuild();
            SynchronizeDPadMetrics(region);
            return true;
        }

        public bool RotateDPadTransform(string id, double degrees)
        {
            XboxRegionDefinition region = GetRegion(id);
            if (!IsDPadTransformRegion(region)) return false;
            region.RotationOffset += degrees;
            modified.Add(id);
            Rebuild();
            SynchronizeDPadMetrics(region);
            return true;
        }

        public bool MoveTopControlTransform(string id, double dx, double dy)
        {
            XboxRegionDefinition region = GetRegion(id);
            if (!IsTopControl(region)) return false;
            region.OffsetX += dx;
            region.OffsetY += dy;
            modified.Add(id);
            Rebuild();
            return true;
        }

        public bool ScaleTopControlTransform(string id, double scaleX, double scaleY)
        {
            XboxRegionDefinition region = GetRegion(id);
            if (!IsTopControl(region) || scaleX <= 0 || scaleY <= 0) return false;
            if (IsTriggerTransformRegion(region)) return false;
            region.ScaleX = Math.Max(0.10, (region.ScaleX == 0 ? 1.0 : region.ScaleX) * scaleX);
            region.ScaleY = Math.Max(0.10, (region.ScaleY == 0 ? 1.0 : region.ScaleY) * scaleY);
            modified.Add(id);
            Rebuild();
            return true;
        }

        public bool RotateTopControlTransform(string id, double degrees)
        {
            XboxRegionDefinition region = GetRegion(id);
            if (!IsTopControl(region)) return false;
            if (IsTriggerTransformRegion(region)) region.RotationOffset += degrees;
            else region.Rotation += degrees;
            modified.Add(id);
            Rebuild();
            return true;
        }

        // Backward-compatible names used by the first transform-only
        // calibrator. They now also route DPadUp through its own transform.
        public bool MoveDPadDerivedTransform(string id, double dx, double dy) { return MoveDPadTransform(id, dx, dy); }
        public bool ScaleDPadDerivedTransform(string id, double scaleX, double scaleY) { return ScaleDPadTransform(id, scaleX, scaleY); }
        public bool RotateDPadDerivedTransform(string id, double degrees) { return RotateDPadTransform(id, degrees); }

        public IList<XboxPathCommand> GetPathCommands(string id)
        {
            XboxRegionDefinition region = GetRegion(id);
            if (region == null) return new List<XboxPathCommand>();
            if (IsDPadTransformRegion(region))
            {
                XboxRegionDefinition baseRegion = GetRegion("dpad-up");
                return baseRegion == null || baseRegion.PathCommands == null ? (IList<XboxPathCommand>)new List<XboxPathCommand>() : baseRegion.PathCommands;
            }
            if (IsDerivedTrigger(region))
            {
                XboxRegionDefinition baseRegion = GetRegion(region.BaseRegion);
                return baseRegion == null || baseRegion.PathCommands == null ? (IList<XboxPathCommand>)new List<XboxPathCommand>() : baseRegion.PathCommands;
            }
            return region.PathCommands == null ? (IList<XboxPathCommand>)new List<XboxPathCommand>() : region.PathCommands;
        }

        public Matrix GetPathDisplayMatrix(string id)
        {
            XboxRegionDefinition region = GetRegion(id);
            if (IsDPadTransformRegion(region))
            {
                XboxRegionDefinition baseRegion = GetRegion("dpad-up");
                return baseRegion == null ? Matrix.Identity : CreateDPadTransformMatrix(baseRegion, region);
            }
            if (IsDerivedTrigger(region)) return CreateDerivedTriggerTransformMatrix(region);
            return Matrix.Identity;
        }

        public XboxDPadEdgeAnalysis BuildDPadEdgeAnalysis(BitmapSource photo, string id)
        {
            if (photo == null) throw new ArgumentNullException("photo");
            Geometry geometry = GetGeometry(id);
            if (geometry == null) throw new ArgumentException("Unknown DPad region", "id");
            Rect bounds = geometry.Bounds;
            bounds.Inflate(18, 18);
            int rawLeft = Math.Max(1, (int)Math.Floor(bounds.Left / SourceScale) - 2);
            int rawTop = Math.Max(1, (int)Math.Floor((bounds.Top - SourceTop) / SourceScale) - 2);
            int rawRight = Math.Min(photo.PixelWidth - 2, (int)Math.Ceiling(bounds.Right / SourceScale) + 2);
            int rawBottom = Math.Min(photo.PixelHeight - 2, (int)Math.Ceiling((bounds.Bottom - SourceTop) / SourceScale) + 2);
            int rawWidth = Math.Max(3, rawRight - rawLeft + 1);
            int rawHeight = Math.Max(3, rawBottom - rawTop + 1);
            BitmapSource pixels = photo.Format == PixelFormats.Bgra32 ? photo : new FormatConvertedBitmap(photo, PixelFormats.Bgra32, null, 0);
            int fullStride = pixels.PixelWidth * 4;
            byte[] full = new byte[fullStride * pixels.PixelHeight];
            pixels.CopyPixels(full, fullStride, 0);
            byte[] luma = new byte[rawWidth * rawHeight];
            for (int y = 0; y < rawHeight; y++)
            {
                for (int x = 0; x < rawWidth; x++)
                {
                    int source = ((rawTop + y) * fullStride) + ((rawLeft + x) * 4);
                    luma[y * rawWidth + x] = (byte)((full[source + 2] * 77 + full[source + 1] * 150 + full[source] * 29) >> 8);
                }
            }
            bool[] edges = new bool[rawWidth * rawHeight];
            List<Point> edgePoints = new List<Point>();
            for (int y = 1; y < rawHeight - 1; y++)
            {
                for (int x = 1; x < rawWidth - 1; x++)
                {
                    int gx = Math.Abs(luma[y * rawWidth + x + 1] - luma[y * rawWidth + x - 1]);
                    int gy = Math.Abs(luma[(y + 1) * rawWidth + x] - luma[(y - 1) * rawWidth + x]);
                    if (gx + gy < 68) continue;
                    edges[y * rawWidth + x] = true;
                    // Every second edge pixel is enough for an informative
                    // overlay while the full set remains in the distance field.
                    if (((x + y) & 1) == 0) edgePoints.Add(new Point((rawLeft + x) * SourceScale, (rawTop + y) * SourceScale + SourceTop));
                }
            }
            float[] distance = new float[rawWidth * rawHeight];
            const float infinite = 4096f;
            const float diagonal = 1.41421356f;
            for (int i = 0; i < distance.Length; i++) distance[i] = edges[i] ? 0f : infinite;
            for (int y = 0; y < rawHeight; y++) for (int x = 0; x < rawWidth; x++)
            {
                int index = y * rawWidth + x;
                float value = distance[index];
                if (x > 0) value = Math.Min(value, distance[index - 1] + 1f);
                if (y > 0) value = Math.Min(value, distance[index - rawWidth] + 1f);
                if (x > 0 && y > 0) value = Math.Min(value, distance[index - rawWidth - 1] + diagonal);
                if (x + 1 < rawWidth && y > 0) value = Math.Min(value, distance[index - rawWidth + 1] + diagonal);
                distance[index] = value;
            }
            for (int y = rawHeight - 1; y >= 0; y--) for (int x = rawWidth - 1; x >= 0; x--)
            {
                int index = y * rawWidth + x;
                float value = distance[index];
                if (x + 1 < rawWidth) value = Math.Min(value, distance[index + 1] + 1f);
                if (y + 1 < rawHeight) value = Math.Min(value, distance[index + rawWidth] + 1f);
                if (x + 1 < rawWidth && y + 1 < rawHeight) value = Math.Min(value, distance[index + rawWidth + 1] + diagonal);
                if (x > 0 && y + 1 < rawHeight) value = Math.Min(value, distance[index + rawWidth - 1] + diagonal);
                distance[index] = value;
            }
            return new XboxDPadEdgeAnalysis(rawLeft, rawTop, rawWidth, rawHeight, distance, edgePoints);
        }

        public XboxDPadAutoCalibrationResult FindDPadTransformRecommendation(string id, XboxDPadEdgeAnalysis analysis)
        {
            XboxRegionDefinition region = GetRegion(id);
            XboxRegionDefinition baseRegion = GetRegion("dpad-up");
            if (!IsDPadTransformRegion(region) || baseRegion == null || analysis == null) return null;
            Geometry source = Compile(baseRegion);
            List<Point> samples = FlattenBoundary(source, 1.5);
            if (samples.Count == 0) return null;
            double startX = region.OffsetX;
            double startY = region.OffsetY;
            double startScaleX = region.ScaleX == 0 ? 1.0 : region.ScaleX;
            double startScaleY = region.ScaleY == 0 ? 1.0 : region.ScaleY;
            double startRotation = region.RotationOffset;
            double current = ScoreDPadTransform(baseRegion, region, samples, analysis, startX, startY, startScaleX, startScaleY, startRotation);
            XboxDPadAutoCalibrationResult best = new XboxDPadAutoCalibrationResult
            {
                RegionId = id, OffsetX = startX, OffsetY = startY, ScaleX = startScaleX, ScaleY = startScaleY,
                RotationOffset = startRotation, AverageEdgeDistance = current, CurrentAverageEdgeDistance = current, SampleCount = samples.Count
            };
            // Coarse pass covers the complete requested range: position ±10px,
            // scale ±5%, rotation ±3°. It never touches the source PathGeometry.
            for (double dx = -10; dx <= 10.001; dx += 2)
                for (double dy = -10; dy <= 10.001; dy += 2)
                    for (double sx = -0.05; sx <= 0.0501; sx += 0.02)
                        for (double sy = -0.05; sy <= 0.0501; sy += 0.02)
                            for (double rotation = -3; rotation <= 3.001; rotation += 1)
                                TryCandidate(baseRegion, region, samples, analysis, startX + dx, startY + dy, startScaleX * (1 + sx), startScaleY * (1 + sy), startRotation + rotation, current, best);
            // Refine around the strongest coarse candidate. Values are clamped
            // back to the advertised search window, not allowed to wander.
            for (double dx = -1.5; dx <= 1.501; dx += 0.5)
                for (double dy = -1.5; dy <= 1.501; dy += 0.5)
                    for (double sx = -0.01; sx <= 0.0101; sx += 0.005)
                        for (double sy = -0.01; sy <= 0.0101; sy += 0.005)
                            for (double rotation = -0.75; rotation <= 0.751; rotation += 0.25)
                                TryCandidate(baseRegion, region, samples, analysis,
                                    Clamp(best.OffsetX + dx, startX - 10, startX + 10),
                                    Clamp(best.OffsetY + dy, startY - 10, startY + 10),
                                    Clamp(best.ScaleX * (1 + sx), startScaleX * 0.95, startScaleX * 1.05),
                                    Clamp(best.ScaleY * (1 + sy), startScaleY * 0.95, startScaleY * 1.05),
                                    Clamp(best.RotationOffset + rotation, startRotation - 3, startRotation + 3), current, best);
            return best;
        }

        public bool ApplyDPadTransformRecommendation(XboxDPadAutoCalibrationResult result)
        {
            if (result == null) return false;
            XboxRegionDefinition region = GetRegion(result.RegionId);
            if (!IsDPadTransformRegion(region)) return false;
            region.OffsetX = result.OffsetX;
            region.OffsetY = result.OffsetY;
            if (!IsTriggerTransformRegion(region))
            {
                region.ScaleX = result.ScaleX;
                region.ScaleY = result.ScaleY;
            }
            region.RotationOffset = result.RotationOffset;
            modified.Add(region.Id);
            Rebuild();
            SynchronizeDPadMetrics(region);
            return true;
        }

        public XboxDPadEdgeAnalysis BuildTopControlEdgeAnalysis(BitmapSource photo, string id)
        {
            XboxRegionDefinition region = GetRegion(id);
            if (!IsTopControl(region)) throw new ArgumentException("Unknown Xbox top control", "id");
            if (IsImageMaskTrigger(region)) throw new InvalidOperationException("LT/RT use alpha-mask anchor calibration; use the trigger mask calibration view.");
            return BuildDPadEdgeAnalysis(photo, id);
        }

        public XboxTopControlAutoCalibrationResult FindTopControlTransformRecommendation(string id, XboxDPadEdgeAnalysis analysis)
        {
            XboxRegionDefinition region = GetRegion(id);
            if (!IsTopControl(region) || IsImageMaskTrigger(region) || analysis == null) return null;
            XboxRegionDefinition sourceRegion = Clone(IsDerivedTrigger(region) ? GetRegion(region.BaseRegion) : region);
            sourceRegion.OffsetX = 0;
            sourceRegion.OffsetY = 0;
            sourceRegion.ScaleX = 1;
            sourceRegion.ScaleY = 1;
            sourceRegion.Rotation = 0;
            sourceRegion.RotationOffset = 0;
            Geometry sourceGeometry = Compile(sourceRegion);
            if (IsDerivedTrigger(region))
            {
                sourceGeometry = sourceGeometry.Clone();
                sourceGeometry.Transform = new MatrixTransform(new Matrix(-1, 0, 0, 1, LogicalWidth, 0));
            }
            List<Point> samples = FlattenBoundary(sourceGeometry, 1.5);
            if (samples.Count == 0) return null;
            double startX = region.OffsetX;
            double startY = region.OffsetY;
            double startScaleX = region.ScaleX == 0 ? 1.0 : region.ScaleX;
            double startScaleY = region.ScaleY == 0 ? 1.0 : region.ScaleY;
            double startRotation = IsTriggerTransformRegion(region) ? region.RotationOffset : region.Rotation;
            double current = ScoreTopControlTransform(region, samples, analysis, startX, startY, startScaleX, startScaleY, startRotation);
            XboxTopControlAutoCalibrationResult best = new XboxTopControlAutoCalibrationResult
            {
                RegionId = id, OffsetX = startX, OffsetY = startY, ScaleX = startScaleX, ScaleY = startScaleY,
                Rotation = startRotation, AverageEdgeDistance = current, CurrentAverageEdgeDistance = current, SampleCount = samples.Count
            };
            for (double dx = -10; dx <= 10.001; dx += 2)
                for (double dy = -10; dy <= 10.001; dy += 2)
                    for (double sx = -0.05; sx <= 0.0501; sx += 0.02)
                        for (double sy = -0.05; sy <= 0.0501; sy += 0.02)
                            for (double rotation = -3; rotation <= 3.001; rotation += 1)
                                TryTopControlCandidate(region, samples, analysis, startX + dx, startY + dy, startScaleX * (1 + sx), startScaleY * (1 + sy), startRotation + rotation, best);
            for (double dx = -1.5; dx <= 1.501; dx += 0.5)
                for (double dy = -1.5; dy <= 1.501; dy += 0.5)
                    for (double sx = -0.01; sx <= 0.0101; sx += 0.005)
                        for (double sy = -0.01; sy <= 0.0101; sy += 0.005)
                            for (double rotation = -0.75; rotation <= 0.751; rotation += 0.25)
                                TryTopControlCandidate(region, samples, analysis,
                                    Clamp(best.OffsetX + dx, startX - 10, startX + 10), Clamp(best.OffsetY + dy, startY - 10, startY + 10),
                                    Clamp(best.ScaleX * (1 + sx), startScaleX * 0.95, startScaleX * 1.05), Clamp(best.ScaleY * (1 + sy), startScaleY * 0.95, startScaleY * 1.05),
                                    Clamp(best.Rotation + rotation, startRotation - 3, startRotation + 3), best);
            return best;
        }

        public bool ApplyTopControlTransformRecommendation(XboxTopControlAutoCalibrationResult result)
        {
            if (result == null) return false;
            XboxRegionDefinition region = GetRegion(result.RegionId);
            if (!IsTopControl(region)) return false;
            region.OffsetX = result.OffsetX;
            region.OffsetY = result.OffsetY;
            region.ScaleX = result.ScaleX;
            region.ScaleY = result.ScaleY;
            if (IsTriggerTransformRegion(region)) region.RotationOffset = result.Rotation;
            else region.Rotation = result.Rotation;
            modified.Add(region.Id);
            Rebuild();
            return true;
        }

        private static void TryTopControlCandidate(XboxRegionDefinition region, List<Point> samples, XboxDPadEdgeAnalysis analysis, double offsetX, double offsetY, double scaleX, double scaleY, double rotation, XboxTopControlAutoCalibrationResult best)
        {
            double score = ScoreTopControlTransform(region, samples, analysis, offsetX, offsetY, scaleX, scaleY, rotation);
            if (score >= best.AverageEdgeDistance) return;
            best.OffsetX = offsetX; best.OffsetY = offsetY; best.ScaleX = scaleX; best.ScaleY = scaleY; best.Rotation = rotation; best.AverageEdgeDistance = score;
        }

        private static double ScoreTopControlTransform(XboxRegionDefinition region, List<Point> samples, XboxDPadEdgeAnalysis analysis, double offsetX, double offsetY, double scaleX, double scaleY, double rotation)
        {
            double radians = rotation * Math.PI / 180.0;
            double cos = Math.Cos(radians); double sin = Math.Sin(radians); double total = 0;
            for (int i = 0; i < samples.Count; i++)
            {
                double dx = (samples[i].X - region.CX) * scaleX;
                double dy = (samples[i].Y - region.CY) * scaleY;
                total += analysis.DistanceAtStage(new Point(region.CX + dx * cos - dy * sin + offsetX, region.CY + dx * sin + dy * cos + offsetY));
            }
            return total / samples.Count;
        }

        public List<Point> GetDPadBoundarySamples(string id, double spacing)
        {
            Geometry geometry = GetGeometry(id);
            return geometry == null ? new List<Point>() : FlattenBoundary(geometry, spacing);
        }

        private static void TryCandidate(XboxRegionDefinition baseRegion, XboxRegionDefinition region, List<Point> samples, XboxDPadEdgeAnalysis analysis, double offsetX, double offsetY, double scaleX, double scaleY, double rotationOffset, double current, XboxDPadAutoCalibrationResult best)
        {
            double score = ScoreDPadTransform(baseRegion, region, samples, analysis, offsetX, offsetY, scaleX, scaleY, rotationOffset);
            if (score >= best.AverageEdgeDistance) return;
            best.OffsetX = offsetX; best.OffsetY = offsetY; best.ScaleX = scaleX; best.ScaleY = scaleY;
            best.RotationOffset = rotationOffset; best.AverageEdgeDistance = score; best.CurrentAverageEdgeDistance = current;
        }

        private static double ScoreDPadTransform(XboxRegionDefinition baseRegion, XboxRegionDefinition region, List<Point> samples, XboxDPadEdgeAnalysis analysis, double offsetX, double offsetY, double scaleX, double scaleY, double rotationOffset)
        {
            XboxRegionDefinition candidate = new XboxRegionDefinition { Rotation = region.Rotation, OffsetX = offsetX, OffsetY = offsetY, ScaleX = scaleX, ScaleY = scaleY, RotationOffset = rotationOffset };
            Matrix matrix = CreateDPadTransformMatrix(baseRegion, candidate);
            double total = 0;
            for (int i = 0; i < samples.Count; i++) total += analysis.DistanceAtStage(matrix.Transform(samples[i]));
            return total / samples.Count;
        }

        private static List<Point> FlattenBoundary(Geometry geometry, double spacing)
        {
            List<Point> result = new List<Point>();
            if (geometry == null) return result;
            PathGeometry flattened = geometry.GetFlattenedPathGeometry(0.35, ToleranceType.Absolute);
            for (int f = 0; f < flattened.Figures.Count; f++)
            {
                Point previous = flattened.Figures[f].StartPoint;
                result.Add(previous);
                for (int s = 0; s < flattened.Figures[f].Segments.Count; s++)
                {
                    PolyLineSegment line = flattened.Figures[f].Segments[s] as PolyLineSegment;
                    if (line == null) continue;
                    for (int p = 0; p < line.Points.Count; p++)
                    {
                        Point next = line.Points[p];
                        Vector delta = next - previous;
                        int count = Math.Max(1, (int)Math.Ceiling(delta.Length / Math.Max(0.5, spacing)));
                        for (int i = 1; i <= count; i++) result.Add(new Point(previous.X + delta.X * i / count, previous.Y + delta.Y * i / count));
                        previous = next;
                    }
                }
            }
            return result;
        }

        private static double Clamp(double value, double minimum, double maximum) { return Math.Max(minimum, Math.Min(maximum, value)); }

        private void MergeOverride(XboxRegionsOverride overrideData)
        {
            if (overrideData.Regions == null) return;
            for (int i = 0; i < overrideData.Regions.Count; i++)
            {
                XboxRegionDefinition change = overrideData.Regions[i];
                XboxRegionDefinition existing = Find(document.Regions, change.Id);
                if (existing == null) continue;
                // A pre-transform override may contain an obsolete independent
                // DPad polygon/path.  It cannot be safely merged into the new
                // one-base-path model, so retain the calibrated derived default.
                if (IsDerivedDPad(existing) && (!IsDerivedDPad(change) || !string.Equals(change.BaseRegion, existing.BaseRegion, StringComparison.OrdinalIgnoreCase))) change = Clone(existing);
                // The trigger subsystem was rebuilt from the source image.
                // Discard only retired trigger overrides; unrelated DPad/user
                // calibration remains intact.
                if (string.Equals(existing.Id, "lt", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(change.GeometryPath, existing.GeometryPath, StringComparison.OrdinalIgnoreCase))
                    change = Clone(existing);
                // Trigger mattes are source-size assets, not calibration
                // transforms. Never merge an older PathGeometry override into
                // either of the two image-mask trigger regions.
                if (IsImageMaskTrigger(existing) &&
                    (!IsImageMaskTrigger(change) || !string.Equals(change.GeometryPath, existing.GeometryPath, StringComparison.OrdinalIgnoreCase)))
                    change = Clone(existing);
                // Older trigger overrides were saved using the full transparent
                // PNG canvas top-left. They are incompatible with the
                // alpha-centre anchor model, so retain the new default anchor
                // and discard only those retired placement values.
                if (IsImageMaskTrigger(existing) && change.TriggerAnchorX == 0 && change.TriggerAnchorY == 0)
                {
                    change.TriggerAnchorX = existing.TriggerAnchorX;
                    change.TriggerAnchorY = existing.TriggerAnchorY;
                    change.TriggerOffsetX = 0;
                    change.TriggerOffsetY = 0;
                }
                // RT used to be an independent hand-authored path. Preserve a
                // user's unrelated regions but never migrate its retired Path,
                // Scale or large offset patches into the new mirrored model.
                if (IsDerivedTrigger(existing) && (!IsDerivedTrigger(change) ||
                    !string.Equals(change.BaseRegion, existing.BaseRegion, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(change.GeometryPath, existing.GeometryPath, StringComparison.OrdinalIgnoreCase)))
                    change = Clone(existing);
                if (IsDerivedTrigger(existing) && existing.PathPointAdjustments != null &&
                    existing.PathPointAdjustments.Count > 0 &&
                    (change.PathPointAdjustments == null || change.PathPointAdjustments.Count == 0))
                    change.PathPointAdjustments = Clone(existing).PathPointAdjustments;
                // Schema v1 overrides created before stick motion parameters were
                // introduced have zero-valued new fields. Preserve the calibrated
                // defaults instead of sending the cap to the stage origin.
                if (existing.MotionCenterX != 0 && change.MotionCenterX == 0) change.MotionCenterX = existing.MotionCenterX;
                if (existing.MotionCenterY != 0 && change.MotionCenterY == 0) change.MotionCenterY = existing.MotionCenterY;
                if (existing.RingWidth != 0 && change.RingWidth == 0) change.RingWidth = existing.RingWidth;
                if (existing.RingHeight != 0 && change.RingHeight == 0) change.RingHeight = existing.RingHeight;
                if (existing.CapWidth != 0 && change.CapWidth == 0) change.CapWidth = existing.CapWidth;
                if (existing.CapHeight != 0 && change.CapHeight == 0) change.CapHeight = existing.CapHeight;
                if (existing.TravelX != 0 && change.TravelX == 0) change.TravelX = existing.TravelX;
                if (existing.TravelY != 0 && change.TravelY == 0) change.TravelY = existing.TravelY;
                // Older override files predate the hand-traced DPad Bezier
                // paths. They must not silently replace a corrected direction
                // with the retired straight-edge polygon on first run.
                if (existing.Id != null && existing.Id.StartsWith("dpad-", StringComparison.OrdinalIgnoreCase) && existing.PathCommands != null && existing.PathCommands.Count > 0 && (change.PathCommands == null || change.PathCommands.Count == 0))
                {
                    XboxRegionDefinition calibratedDefault = Clone(existing);
                    change.Kind = calibratedDefault.Kind;
                    change.CX = calibratedDefault.CX;
                    change.CY = calibratedDefault.CY;
                    change.Width = calibratedDefault.Width;
                    change.Height = calibratedDefault.Height;
                    change.Rotation = calibratedDefault.Rotation;
                    change.ScaleX = calibratedDefault.ScaleX;
                    change.ScaleY = calibratedDefault.ScaleY;
                    change.GeometryPath = calibratedDefault.GeometryPath;
                    change.EdgeStrokeWidth = calibratedDefault.EdgeStrokeWidth;
                    change.OuterGlowWidth = calibratedDefault.OuterGlowWidth;
                    change.OuterGlowOpacity = calibratedDefault.OuterGlowOpacity;
                    change.PathCommands = calibratedDefault.PathCommands;
                    change.Points = null;
                }
                int index = document.Regions.IndexOf(existing);
                document.Regions[index] = Clone(change);
                // Preserve previously loaded user edits when a focused
                // calibration session saves one additional region.
                modified.Add(change.Id);
            }
        }

        private bool ValidateDocument(XboxRegionsDocument candidate, out string reason)
        {
            if (candidate == null) { reason = "文件为空。"; return false; }
            if (candidate.SchemaVersion != SchemaVersion) { reason = "schemaVersion 不匹配。"; return false; }
            if (!string.Equals(candidate.SourceImage, "controller.png", StringComparison.OrdinalIgnoreCase)) { reason = "sourceImage 不匹配。"; return false; }
            if (candidate.ImageWidth != SourceImageWidth || candidate.ImageHeight != SourceImageHeight) { reason = "底图尺寸不匹配。"; return false; }
            if (candidate.LogicalWidth != LogicalWidth || candidate.LogicalHeight != LogicalHeight) { reason = "逻辑舞台尺寸不匹配。"; return false; }
            if (candidate.Regions == null || candidate.Regions.Count != 17) { reason = "区域数量不正确。"; return false; }
            string[] required = { "a", "b", "x", "y", "dpad-up", "dpad-down", "dpad-left", "dpad-right", "view", "menu", "guide", "lb", "rb", "lt", "rt", "l3", "r3" };
            for (int i = 0; i < required.Length; i++) if (Find(candidate.Regions, required[i]) == null) { reason = "缺少区域：" + required[i]; return false; }
            reason = "默认 Xbox 区域已加载。";
            return true;
        }

        private bool ValidateOverride(XboxRegionsOverride candidate, out string reason)
        {
            if (candidate == null) { reason = "文件为空。"; return false; }
            if (candidate.SchemaVersion != SchemaVersion || !string.Equals(candidate.SourceImage, defaults.SourceImage, StringComparison.OrdinalIgnoreCase) || candidate.ImageWidth != defaults.ImageWidth || candidate.ImageHeight != defaults.ImageHeight || candidate.LogicalWidth != LogicalWidth || candidate.LogicalHeight != LogicalHeight) { reason = "版本或底图信息不匹配。"; return false; }
            if (candidate.Regions == null) { reason = "没有区域修改。"; return false; }
            for (int i = 0; i < candidate.Regions.Count; i++) if (Find(defaults.Regions, candidate.Regions[i].Id) == null) { reason = "区域名称不匹配：" + candidate.Regions[i].Id; return false; }
            reason = "有效。";
            return true;
        }

        private void Rebuild()
        {
            byId = new Dictionary<string, XboxRegionDefinition>(StringComparer.OrdinalIgnoreCase);
            geometries = new Dictionary<string, Geometry>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < document.Regions.Count; i++)
            {
                XboxRegionDefinition region = document.Regions[i];
                byId[region.Id] = region;
            }
            // Compile the one physical source path once. All four visible
            // directions receive an independent transform of that locked path.
            for (int i = 0; i < document.Regions.Count; i++)
            {
                XboxRegionDefinition region = document.Regions[i];
                if (IsDPadTransformRegion(region) || IsDerivedTrigger(region) || IsImageMaskTrigger(region)) continue;
                Geometry geometry = Compile(region);
                geometry.Freeze();
                geometries[region.Id] = geometry;
            }
            XboxRegionDefinition dpadBase = GetRegion("dpad-up");
            if (dpadBase == null) throw new InvalidDataException("Missing DPadUp source geometry.");
            Geometry dpadSource = Compile(dpadBase);
            for (int i = 0; i < document.Regions.Count; i++)
            {
                XboxRegionDefinition region = document.Regions[i];
                if (!IsDPadTransformRegion(region)) continue;
                Geometry transformed = dpadSource.Clone();
                transformed.Transform = new MatrixTransform(CreateDPadTransformMatrix(dpadBase, region));
                transformed.Freeze();
                geometries[region.Id] = transformed;
            }
            XboxRegionDefinition triggerBase = GetRegion("lt");
            if (triggerBase == null) throw new InvalidDataException("Missing LT trigger region.");
            // Legacy derived trigger files remain readable for old overrides,
            // but the current default does not compile LT/RT into Geometry.
            // Both triggers now render through source-aligned alpha masks.
            if (!IsImageMaskTrigger(triggerBase))
            {
                Geometry triggerSource = Compile(triggerBase);
                for (int i = 0; i < document.Regions.Count; i++)
                {
                    XboxRegionDefinition region = document.Regions[i];
                    if (!IsDerivedTrigger(region)) continue;
                    region.CX = LogicalWidth - triggerBase.CX;
                    region.CY = triggerBase.CY;
                    region.Width = triggerBase.Width;
                    region.Height = triggerBase.Height;
                    XboxRegionDefinition triggerVariant = Clone(triggerBase);
                    ApplyPathPointAdjustments(triggerVariant.PathCommands, region.PathPointAdjustments);
                    Geometry transformed = Compile(triggerVariant);
                    transformed.Transform = new MatrixTransform(CreateDerivedTriggerTransformMatrix(region));
                    transformed.Freeze();
                    geometries[region.Id] = transformed;
                }
            }
        }

        private static bool IsDerivedDPad(XboxRegionDefinition region)
        {
            return region != null && string.Equals(region.Kind, "derivedPath", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(region.BaseRegion);
        }

        private static bool IsDerivedTrigger(XboxRegionDefinition region)
        {
            return region != null && string.Equals(region.Kind, "derivedTriggerPath", StringComparison.OrdinalIgnoreCase) && string.Equals(region.BaseRegion, "lt", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsImageMaskTrigger(XboxRegionDefinition region)
        {
            return region != null && string.Equals(region.Kind, "sourceImageMask", StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(region.Id, "lt", StringComparison.OrdinalIgnoreCase) || string.Equals(region.Id, "rt", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsTriggerTransformRegion(XboxRegionDefinition region)
        {
            return region != null && (IsImageMaskTrigger(region) || IsDerivedTrigger(region));
        }

        private static bool IsDPadTransformRegion(XboxRegionDefinition region)
        {
            if (region == null) return false;
            return string.Equals(region.Id, "dpad-up", StringComparison.OrdinalIgnoreCase) || IsDerivedDPad(region);
        }

        private static bool IsTopControl(XboxRegionDefinition region)
        {
            if (region == null) return false;
            return string.Equals(region.Id, "lb", StringComparison.OrdinalIgnoreCase) || string.Equals(region.Id, "rb", StringComparison.OrdinalIgnoreCase) || string.Equals(region.Id, "lt", StringComparison.OrdinalIgnoreCase) || string.Equals(region.Id, "rt", StringComparison.OrdinalIgnoreCase);
        }

        private static Matrix CreateDPadTransformMatrix(XboxRegionDefinition baseRegion, XboxRegionDefinition derivedRegion)
        {
            double scaleX = derivedRegion.ScaleX == 0 ? 1.0 : derivedRegion.ScaleX;
            double scaleY = derivedRegion.ScaleY == 0 ? 1.0 : derivedRegion.ScaleY;
            double radians = (derivedRegion.Rotation + derivedRegion.RotationOffset) * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            double offsetX = baseRegion.CX + derivedRegion.OffsetX - baseRegion.CX * scaleX * cos + baseRegion.CY * scaleY * sin;
            double offsetY = baseRegion.CY + derivedRegion.OffsetY - baseRegion.CX * scaleX * sin - baseRegion.CY * scaleY * cos;
            return new Matrix(scaleX * cos, scaleX * sin, -scaleY * sin, scaleY * cos, offsetX, offsetY);
        }

        // RT is a true reflected instance of LT. The mirror is applied in the
        // shared 1536x1024 stage first; only then does RT receive its small
        // photo-perspective correction. No second hand-drawn RT path exists.
        private static Matrix CreateDerivedTriggerTransformMatrix(XboxRegionDefinition derivedRegion)
        {
            double radians = (derivedRegion.Rotation + derivedRegion.RotationOffset) * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            Matrix mirror = new Matrix(-1, 0, 0, 1, LogicalWidth, 0);
            Matrix correction = new Matrix(cos, sin, -sin, cos,
                derivedRegion.CX + derivedRegion.OffsetX - derivedRegion.CX * cos + derivedRegion.CY * sin,
                derivedRegion.CY + derivedRegion.OffsetY - derivedRegion.CX * sin - derivedRegion.CY * cos);
            mirror.Append(correction);
            return mirror;
        }

        private static Geometry Compile(XboxRegionDefinition region)
        {
            if (region.PathCommands != null && region.PathCommands.Count > 0)
            {
                StreamGeometry path = new StreamGeometry();
                bool begun = false;
                using (StreamGeometryContext context = path.Open())
                {
                    for (int i = 0; i < region.PathCommands.Count; i++)
                    {
                        XboxPathCommand command = region.PathCommands[i];
                        if (command == null) continue;
                        string op = command.Op ?? string.Empty;
                        if (string.Equals(op, "move", StringComparison.OrdinalIgnoreCase))
                        {
                            context.BeginFigure(Transform(region, new Point(command.X, command.Y)), true, true);
                            begun = true;
                        }
                        else if (begun && string.Equals(op, "line", StringComparison.OrdinalIgnoreCase)) context.LineTo(Transform(region, new Point(command.X, command.Y)), true, false);
                        else if (begun && string.Equals(op, "quadratic", StringComparison.OrdinalIgnoreCase)) context.QuadraticBezierTo(Transform(region, new Point(command.C1X, command.C1Y)), Transform(region, new Point(command.X, command.Y)), true, false);
                        else if (begun && string.Equals(op, "cubic", StringComparison.OrdinalIgnoreCase)) context.BezierTo(Transform(region, new Point(command.C1X, command.C1Y)), Transform(region, new Point(command.C2X, command.C2Y)), Transform(region, new Point(command.X, command.Y)), true, false);
                    }
                }
                return path;
            }
            if (string.Equals(region.Kind, "polygon", StringComparison.OrdinalIgnoreCase) && region.Points != null && region.Points.Count >= 3)
            {
                StreamGeometry geometry = new StreamGeometry();
                using (StreamGeometryContext context = geometry.Open())
                {
                    context.BeginFigure(Transform(region, new Point(region.Points[0].X, region.Points[0].Y)), true, true);
                    for (int i = 1; i < region.Points.Count; i++) context.LineTo(Transform(region, new Point(region.Points[i].X, region.Points[i].Y)), true, false);
                }
                return geometry;
            }
            int count = string.Equals(region.Kind, "ellipse", StringComparison.OrdinalIgnoreCase) ? 48 : 32;
            StreamGeometry result = new StreamGeometry();
            using (StreamGeometryContext context = result.Open())
            {
                for (int i = 0; i < count; i++)
                {
                    double angle = Math.PI * 2.0 * i / count;
                    double x;
                    double y;
                    if (string.Equals(region.Kind, "roundedRect", StringComparison.OrdinalIgnoreCase))
                    {
                        double radius = Math.Min(Math.Min(region.Width, region.Height) / 2.0, Math.Max(0, region.CornerRadius));
                        double perimeter = i / (double)count * 4.0;
                        int corner = (int)Math.Floor(perimeter);
                        double cornerAngle = (perimeter - corner) * Math.PI / 2.0;
                        double cx = corner == 0 || corner == 3 ? region.CX + region.Width / 2.0 - radius : region.CX - region.Width / 2.0 + radius;
                        double cy = corner == 0 || corner == 1 ? region.CY + region.Height / 2.0 - radius : region.CY - region.Height / 2.0 + radius;
                        double start = corner * Math.PI / 2.0;
                        x = cx + Math.Cos(start + cornerAngle) * radius;
                        y = cy + Math.Sin(start + cornerAngle) * radius;
                    }
                    else
                    {
                        x = region.CX + Math.Cos(angle) * region.Width / 2.0;
                        y = region.CY + Math.Sin(angle) * region.Height / 2.0;
                    }
                    Point point = Transform(region, new Point(x, y));
                    if (i == 0) context.BeginFigure(point, true, true); else context.LineTo(point, true, false);
                }
            }
            return result;
        }

        private static Point Transform(XboxRegionDefinition region, Point point)
        {
            if (!IsTopControl(region) && Math.Abs(region.Rotation) < 0.0001) return point;
            double scaleX = IsTopControl(region) && !IsTriggerTransformRegion(region) && region.ScaleX != 0 ? region.ScaleX : 1.0;
            double scaleY = IsTopControl(region) && !IsTriggerTransformRegion(region) && region.ScaleY != 0 ? region.ScaleY : 1.0;
            double radians = (region.Rotation + (IsTriggerTransformRegion(region) ? region.RotationOffset : 0.0)) * Math.PI / 180.0;
            double dx = (point.X - region.CX) * scaleX;
            double dy = (point.Y - region.CY) * scaleY;
            return new Point(region.CX + dx * Math.Cos(radians) - dy * Math.Sin(radians) + (IsTopControl(region) ? region.OffsetX : 0), region.CY + dx * Math.Sin(radians) + dy * Math.Cos(radians) + (IsTopControl(region) ? region.OffsetY : 0));
        }

        private static void TranslatePathCommand(XboxPathCommand command, double dx, double dy)
        {
            if (command == null || string.Equals(command.Op, "close", StringComparison.OrdinalIgnoreCase)) return;
            command.X += dx; command.Y += dy;
            if (string.Equals(command.Op, "cubic", StringComparison.OrdinalIgnoreCase) || string.Equals(command.Op, "quadratic", StringComparison.OrdinalIgnoreCase)) { command.C1X += dx; command.C1Y += dy; }
            if (string.Equals(command.Op, "cubic", StringComparison.OrdinalIgnoreCase)) { command.C2X += dx; command.C2Y += dy; }
        }

        private static void ApplyPathPointAdjustments(List<XboxPathCommand> commands, List<XboxPathPointAdjustment> adjustments)
        {
            if (commands == null || adjustments == null) return;
            for (int i = 0; i < adjustments.Count; i++)
            {
                XboxPathPointAdjustment adjustment = adjustments[i];
                if (adjustment == null || adjustment.CommandIndex < 0 || adjustment.CommandIndex >= commands.Count) continue;
                XboxPathCommand command = commands[adjustment.CommandIndex];
                if (command == null || string.Equals(command.Op, "close", StringComparison.OrdinalIgnoreCase)) continue;
                string role = adjustment.Role ?? "P";
                if (string.Equals(role, "C1", StringComparison.OrdinalIgnoreCase))
                {
                    command.C1X += adjustment.DX;
                    command.C1Y += adjustment.DY;
                }
                else if (string.Equals(role, "C2", StringComparison.OrdinalIgnoreCase))
                {
                    command.C2X += adjustment.DX;
                    command.C2Y += adjustment.DY;
                }
                else
                {
                    command.X += adjustment.DX;
                    command.Y += adjustment.DY;
                }
            }
        }

        private static void ScalePathCommand(XboxPathCommand command, double cx, double cy, double multiplier)
        {
            if (command == null || string.Equals(command.Op, "close", StringComparison.OrdinalIgnoreCase)) return;
            command.X = cx + (command.X - cx) * multiplier; command.Y = cy + (command.Y - cy) * multiplier;
            if (string.Equals(command.Op, "cubic", StringComparison.OrdinalIgnoreCase) || string.Equals(command.Op, "quadratic", StringComparison.OrdinalIgnoreCase)) { command.C1X = cx + (command.C1X - cx) * multiplier; command.C1Y = cy + (command.C1Y - cy) * multiplier; }
            if (string.Equals(command.Op, "cubic", StringComparison.OrdinalIgnoreCase)) { command.C2X = cx + (command.C2X - cx) * multiplier; command.C2Y = cy + (command.C2Y - cy) * multiplier; }
        }

        private static void SynchronizePathMetrics(XboxRegionDefinition region)
        {
            Geometry geometry = Compile(region);
            Rect bounds = geometry.Bounds;
            if (!bounds.IsEmpty)
            {
                region.CX = bounds.X + bounds.Width * 0.5;
                region.CY = bounds.Y + bounds.Height * 0.5;
                region.Width = bounds.Width;
                region.Height = bounds.Height;
            }
            if (region.ScaleX == 0) region.ScaleX = 1.0;
            if (region.ScaleY == 0) region.ScaleY = 1.0;
        }

        private void SynchronizeDPadMetrics(XboxRegionDefinition region)
        {
            if (region == null) return;
            // The source path pivot belongs to DPadUp and must remain stable;
            // updating it from transformed bounds would make later rotations
            // drift. Derived display metrics are safe to refresh.
            if (string.Equals(region.Id, "dpad-up", StringComparison.OrdinalIgnoreCase)) return;
            Geometry geometry = GetGeometry(region.Id);
            if (geometry == null || geometry.Bounds.IsEmpty) return;
            Rect bounds = geometry.Bounds;
            region.CX = bounds.X + bounds.Width * 0.5;
            region.CY = bounds.Y + bounds.Height * 0.5;
            region.Width = bounds.Width;
            region.Height = bounds.Height;
        }

        private static XboxRegionDefinition Find(List<XboxRegionDefinition> regions, string id)
        {
            if (regions == null) return null;
            for (int i = 0; i < regions.Count; i++) if (string.Equals(regions[i].Id, id, StringComparison.OrdinalIgnoreCase)) return regions[i];
            return null;
        }

        private static T ReadEmbedded<T>(string name)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            if (stream == null) throw new FileNotFoundException("Embedded resource not found", name);
            try { return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream); }
            finally { stream.Dispose(); }
        }

        private static T ReadFile<T>(string path)
        {
            using (FileStream stream = File.OpenRead(path)) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }

        private static void WriteFile<T>(string path, T value)
        {
            using (FileStream stream = File.Create(path)) new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
        }

        private static XboxRegionsDocument Clone(XboxRegionsDocument value)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(XboxRegionsDocument));
                serializer.WriteObject(stream, value); stream.Position = 0;
                return serializer.ReadObject(stream) as XboxRegionsDocument;
            }
        }

        private static XboxRegionDefinition Clone(XboxRegionDefinition value)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(XboxRegionDefinition));
                serializer.WriteObject(stream, value); stream.Position = 0;
                return serializer.ReadObject(stream) as XboxRegionDefinition;
            }
        }
    }

    public sealed class XboxCalibrationSurface : FrameworkElement
    {
        private readonly XboxRegionManager manager;
        private readonly ImageSource photo;
        private readonly HashSet<string> allowedIds;
        private string selectedId;
        private Point lastStagePoint;
        private bool dragging;
        public event Action<string> RegionSelected;
        public event Action CoordinatesChanged;
        public string SelectedId { get { return selectedId; } }

        public XboxCalibrationSurface(XboxRegionManager value, ImageSource source, IEnumerable<string> visibleRegionIds = null)
        {
            manager = value; photo = source;
            if (visibleRegionIds != null) allowedIds = new HashSet<string>(visibleRegionIds, StringComparer.OrdinalIgnoreCase);
            Focusable = true; Cursor = Cursors.Cross; SnapsToDevicePixels = true;
            MouseLeftButtonDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseUp;
            PreviewKeyDown += OnKeyDown;
        }

        public void Select(string id) { selectedId = id; InvalidateVisual(); }
        private bool IsAllowed(string id) { return allowedIds == null || (id != null && allowedIds.Contains(id)); }
        private bool IsTriggerCalibration { get { return allowedIds != null && allowedIds.Contains("lt") && allowedIds.Contains("rt"); } }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            Matrix matrix = XboxRegionManager.CreateStageMatrix(ActualWidth, ActualHeight);
            dc.PushTransform(new MatrixTransform(matrix));
            manager.DrawPhoto(dc, photo);
            if (manager.Document != null && manager.Document.Regions != null)
            {
                for (int i = 0; i < manager.Document.Regions.Count; i++)
                {
                    XboxRegionDefinition region = manager.Document.Regions[i];
                    if (!IsAllowed(region.Id)) continue;
                    Geometry geometry = manager.GetGeometry(region.Id);
                    bool selected = string.Equals(selectedId, region.Id, StringComparison.OrdinalIgnoreCase);
                    Color color = string.Equals(region.Id, "lt", StringComparison.OrdinalIgnoreCase) ? Palette.Green : (string.Equals(region.Id, "rt", StringComparison.OrdinalIgnoreCase) ? Palette.Blue : (selected ? Palette.Warning : Palette.Blue));
                    // All calibration overlays omit glow/blur. Trigger mode adds
                    // a deliberately weak interior fill so its full hardware
                    // silhouette can be checked against the source photograph.
                    bool wireframeOnly = allowedIds != null && !IsTriggerCalibration;
                    double pixel = 1.0 / Math.Max(0.001, matrix.M11);
                    Brush fill = wireframeOnly ? null : new SolidColorBrush(Color.FromArgb(IsTriggerCalibration ? (selected ? (byte)46 : (byte)22) : (selected ? (byte)26 : (byte)8), color.R, color.G, color.B));
                    dc.DrawGeometry(fill, new Pen(new SolidColorBrush(Color.FromArgb(selected ? (byte)235 : (byte)175, color.R, color.G, color.B)), wireframeOnly || IsTriggerCalibration ? pixel : (selected ? 2.0 : 1.0)), geometry);
                    if (IsTriggerCalibration)
                    {
                        Point center = new Point(geometry.Bounds.Left + geometry.Bounds.Width * 0.5, geometry.Bounds.Top + geometry.Bounds.Height * 0.5);
                        double arm = 5.0 * pixel;
                        Pen centerPen = new Pen(new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)), pixel);
                        dc.DrawLine(centerPen, new Point(center.X - arm, center.Y), new Point(center.X + arm, center.Y));
                        dc.DrawLine(centerPen, new Point(center.X, center.Y - arm), new Point(center.X, center.Y + arm));
                    }
                    FormattedText label = new FormattedText(region.Id, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Microsoft YaHei UI"), 12, new SolidColorBrush(color), 1.0);
                    dc.DrawText(label, new Point(region.CX + 4, region.CY - 5));
                }
            }
            dc.Pop();
        }

        private Point ToStage(Point screen)
        {
            Matrix matrix = XboxRegionManager.CreateStageMatrix(ActualWidth, ActualHeight);
            matrix.Invert();
            return matrix.Transform(screen);
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            Point point = ToStage(e.GetPosition(this));
            string hit = manager.HitTestRegion(point);
            if (hit == null || !IsAllowed(hit)) return;
            selectedId = hit; lastStagePoint = point; dragging = true; CaptureMouse();
            if (RegionSelected != null) RegionSelected(hit);
            if (CoordinatesChanged != null) CoordinatesChanged();
            InvalidateVisual();
            e.Handled = true;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!dragging || string.IsNullOrEmpty(selectedId) || e.LeftButton != MouseButtonState.Pressed) return;
            Point point = ToStage(e.GetPosition(this));
            manager.MoveRegion(selectedId, point.X - lastStagePoint.X, point.Y - lastStagePoint.Y);
            lastStagePoint = point;
            if (CoordinatesChanged != null) CoordinatesChanged();
            InvalidateVisual();
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            dragging = false; ReleaseMouseCapture();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedId)) return;
            double step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 5 : 1;
            double dx = 0, dy = 0;
            if (e.Key == Key.Left) dx = -step;
            else if (e.Key == Key.Right) dx = step;
            else if (e.Key == Key.Up) dy = -step;
            else if (e.Key == Key.Down) dy = step;
            else return;
            manager.MoveRegion(selectedId, dx, dy);
            if (CoordinatesChanged != null) CoordinatesChanged();
            InvalidateVisual(); e.Handled = true;
        }
    }

    public sealed class XboxCalibrationWindow : Window
    {
        private readonly XboxRegionManager manager;
        private readonly XboxCalibrationSurface surface;
        private readonly ListBox list;
        private readonly TextBlock details;
        private readonly TextBlock status;
        private readonly ImageSource sourcePhoto;
        private readonly bool topControlMode;
        private XboxTopControlAutoCalibrationResult topControlRecommendation;
        public string StatusMessage { get; private set; }

        public XboxCalibrationWindow(XboxRegionManager value, ImageSource photo, IEnumerable<string> visibleRegionIds = null, string title = null)
        {
            manager = value;
            sourcePhoto = photo;
            Title = string.IsNullOrWhiteSpace(title) ? "Xbox Controller Calibration" : title;
            Width = 1420; Height = 860; MinWidth = 1100; MinHeight = 680;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Palette.WindowBrush; Foreground = Palette.TextBrush; FontFamily = new FontFamily("Microsoft YaHei UI");
            UseLayoutRounding = true; SnapsToDevicePixels = true;
            Grid root = new Grid { Margin = new Thickness(14) };
            // The calibrator can run on 100–150% DPI.  Keep only the selector
            // and controls fixed; the shared logical stage consumes the rest.
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
            list = new ListBox { Background = Palette.SurfaceBrush, Foreground = Palette.TextBrush, BorderBrush = Palette.BorderBrush, Margin = new Thickness(0, 0, 10, 0) };
            HashSet<string> visible = visibleRegionIds == null ? null : new HashSet<string>(visibleRegionIds, StringComparer.OrdinalIgnoreCase);
            topControlMode = visible != null && visible.SetEquals(new HashSet<string>(new[] { "lb", "rb", "lt", "rt" }, StringComparer.OrdinalIgnoreCase));
            for (int i = 0; i < manager.Document.Regions.Count; i++) if (visible == null || visible.Contains(manager.Document.Regions[i].Id)) list.Items.Add(manager.Document.Regions[i].Id);
            root.Children.Add(list);
            surface = new XboxCalibrationSurface(manager, photo, visibleRegionIds);
            Border stage = new Border { HorizontalAlignment = HorizontalAlignment.Stretch, BorderBrush = Palette.BorderBrush, BorderThickness = new Thickness(1), Background = Palette.WindowBrush, Margin = new Thickness(0, 0, 10, 0), Child = surface };
            Grid.SetColumn(stage, 1); root.Children.Add(stage);
            StackPanel controls = new StackPanel { Background = Palette.SurfaceBrush };
            ScrollViewer controlScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = controls };
            Border panel = new Border { Background = Palette.SurfaceBrush, BorderBrush = Palette.BorderBrush, BorderThickness = new Thickness(1), Padding = new Thickness(14), Child = controlScroll };
            Grid.SetColumn(panel, 2); root.Children.Add(panel);
            details = MakeText("选择一个区域。", 13, Palette.TextBrush, true);
            status = MakeText(manager.LastLoadMessage, 11, Palette.MutedBrush, false);
            controls.Children.Add(details); controls.Children.Add(status); controls.Children.Add(Line());
            AddButton(controls, "缩小 1%", delegate { AdjustScale(0.99); });
            AddButton(controls, "放大 1%", delegate { AdjustScale(1.01); });
            AddButton(controls, "逆时针 1°", delegate { AdjustRotation(-1); });
            AddButton(controls, "顺时针 1°", delegate { AdjustRotation(1); });
            AddButton(controls, "恢复当前区域默认值", delegate { if (surface.SelectedId != null) { manager.ResetRegion(surface.SelectedId); Refresh(); } });
            controls.Children.Add(Line());
            controls.Children.Add(MakeText("拖动：移动区域\n方向键：1 原图像素\nShift + 方向键：5 原图像素", 11, Palette.MutedBrush, false));
            controls.Children.Add(Line());
            AddButton(controls, "保存用户覆盖", delegate { string message; manager.SaveUserOverride(out message); StatusMessage = message; status.Text = message; });
            AddButton(controls, "重新加载默认 + 覆盖", delegate { manager.Reload(false); surface.Select(null); status.Text = manager.LastLoadMessage; surface.InvalidateVisual(); });
            Button close = new Button { Content = "关闭", Height = 32, Margin = new Thickness(0, 8, 0, 0), Style = LabVisualStyles.SecondaryButtonStyle };
            close.Click += delegate { Close(); }; controls.Children.Add(close);
            list.SelectionChanged += delegate { surface.Select(list.SelectedItem as string); Refresh(); };
            surface.RegionSelected += delegate(string id) { list.SelectedItem = id; Refresh(); };
            surface.CoordinatesChanged += Refresh;
            if (topControlMode)
            {
                controls.Children.Add(Line());
                controls.Children.Add(MakeText("Wireframe only. Auto-search: Offset +/-10px, Scale +/-5%, Rotation +/-3 deg.", 11, Palette.WarningBrush, false));
                AddButton(controls, "Auto-search selected transform", delegate { SearchTopControlRecommendation(); });
                AddButton(controls, "Apply recommended transform", delegate { ApplyTopControlRecommendation(); });
            }
            Content = root;
            Loaded += delegate { surface.Focus(); };
        }

        private void AdjustScale(double multiplier) { if (surface.SelectedId != null) { manager.ScaleRegion(surface.SelectedId, multiplier); Refresh(); } }
        private void AdjustRotation(double degrees) { if (surface.SelectedId != null) { manager.RotateRegion(surface.SelectedId, degrees); Refresh(); } }
        private void SearchTopControlRecommendation()
        {
            try
            {
                if (string.IsNullOrEmpty(surface.SelectedId)) { status.Text = "Select LB, RB, LT, or RT first."; return; }
                XboxDPadEdgeAnalysis analysis = manager.BuildTopControlEdgeAnalysis(sourcePhoto as BitmapSource, surface.SelectedId);
                topControlRecommendation = manager.FindTopControlTransformRecommendation(surface.SelectedId, analysis);
                status.Text = topControlRecommendation == null ? "No reliable edge recommendation was found." : topControlRecommendation.Describe();
            }
            catch (Exception ex) { status.Text = "Auto-search failed: " + ex.Message; }
        }
        private void ApplyTopControlRecommendation()
        {
            if (topControlRecommendation == null || !string.Equals(topControlRecommendation.RegionId, surface.SelectedId, StringComparison.OrdinalIgnoreCase)) { status.Text = "Run auto-search for the selected region first."; return; }
            if (manager.ApplyTopControlTransformRecommendation(topControlRecommendation)) { status.Text = "Recommended transform applied; source PathGeometry was not changed."; Refresh(); }
        }
        private void Refresh() { details.Text = manager.Describe(surface.SelectedId); surface.InvalidateVisual(); }
        private static TextBlock MakeText(string value, double size, Brush brush, bool bold) { return new TextBlock { Text = value, FontSize = size, Foreground = brush, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 7) }; }
        private static Border Line() { return new Border { Height = 1, Background = Palette.BorderBrush, Margin = new Thickness(0, 9, 0, 7) }; }
        private static void AddButton(Panel panel, string label, RoutedEventHandler handler) { Button button = new Button { Content = label, Height = 30, Margin = new Thickness(0, 3, 0, 0), Style = LabVisualStyles.SecondaryButtonStyle }; button.Click += handler; panel.Children.Add(button); }
    }

    public sealed class XboxTriggerCalibrationSurface : FrameworkElement
    {
        private static readonly Rect FocusViewport = new Rect(330, 65, 880, 155);
        private readonly XboxRegionManager manager;
        private readonly ImageSource photo;
        private string selectedId = "lt";
        private Point lastStagePoint;
        private bool draggingRegion;

        public event Action Changed;
        public string SelectedId { get { return selectedId; } }
        public string SelectedHandleText { get { return "透明 PNG Alpha Mask（无 Bezier 控制点）"; } }

        public XboxTriggerCalibrationSurface(XboxRegionManager value, ImageSource source)
        {
            manager = value;
            photo = source;
            Focusable = true;
            Cursor = Cursors.Cross;
            SnapsToDevicePixels = true;
            MouseLeftButtonDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseUp;
            PreviewKeyDown += OnKeyDown;
        }

        public void Select(string id)
        {
            if (!string.Equals(id, "lt", StringComparison.OrdinalIgnoreCase) && !string.Equals(id, "rt", StringComparison.OrdinalIgnoreCase)) return;
            selectedId = id;
            if (Changed != null) Changed();
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            Matrix matrix = CreateFocusMatrix();
            double scale = matrix.M11;
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));
            dc.PushTransform(new MatrixTransform(matrix));
            manager.DrawPhoto(dc, photo);
            DrawTrigger(dc, "lt", Palette.Green, scale);
            DrawTrigger(dc, "rt", Palette.Blue, scale);
            dc.Pop();
            dc.Pop();
            DrawScreenText(dc, "LT / RT source PNG masks | 1px alpha edge | dashed box = alpha bounds | orange cross = target anchor", 12, 12, 12, Palette.WarningBrush);
            DrawScreenText(dc, "Drag = Offset; Arrow = 1px; Shift + Arrow = 5px. Scale remains uniform around the alpha anchor.", 12, Math.Max(32, ActualHeight - 24), 11, Palette.TextBrush);
        }

        private void DrawTrigger(DrawingContext dc, string id, Color color, double scale)
        {
            bool selected = string.Equals(selectedId, id, StringComparison.OrdinalIgnoreCase);
            double pixel = 1.0 / Math.Max(0.001, scale);
            manager.DrawTriggerMaskBoundary(dc, id, selected ? color : Color.FromArgb(180, color.R, color.G, color.B), selected ? 1.0 : 0.70);
            Rect bounds = manager.GetTriggerMaskBounds(id);
            if (bounds.IsEmpty) return;
            Pen boundsPen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(selected ? 230 : 155), color.R, color.G, color.B)), pixel) { DashStyle = DashStyles.Dash };
            dc.DrawRectangle(null, boundsPen, bounds);
            Point target = manager.GetTriggerTargetAnchor(id);
            Pen targetPen = new Pen(new SolidColorBrush(Palette.Warning), pixel);
            double arm = 5.0 * pixel;
            dc.DrawLine(targetPen, new Point(target.X - arm, target.Y), new Point(target.X + arm, target.Y));
            dc.DrawLine(targetPen, new Point(target.X, target.Y - arm), new Point(target.X, target.Y + arm));
            Point center = manager.GetTriggerCurrentAnchor(id);
            Pen centerPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 255, 255)), pixel);
            dc.DrawLine(centerPen, new Point(center.X - arm, center.Y), new Point(center.X + arm, center.Y));
            dc.DrawLine(centerPen, new Point(center.X, center.Y - arm), new Point(center.X, center.Y + arm));
        }

        private Matrix CreateFocusMatrix()
        {
            double scale = Math.Min(ActualWidth / FocusViewport.Width, ActualHeight / FocusViewport.Height);
            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0) scale = 1;
            return new Matrix(scale, 0, 0, scale, (ActualWidth - FocusViewport.Width * scale) * 0.5 - FocusViewport.X * scale, (ActualHeight - FocusViewport.Height * scale) * 0.5 - FocusViewport.Y * scale);
        }

        private Point ToStage(Point screen)
        {
            Matrix matrix = CreateFocusMatrix();
            matrix.Invert();
            return matrix.Transform(screen);
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            Point stage = ToStage(e.GetPosition(this));
            string hit = HitTrigger(stage);
            if (hit == null) return;
            selectedId = hit;
            lastStagePoint = stage;
            draggingRegion = true;
            CaptureMouse();
            Notify();
            e.Handled = true;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!draggingRegion || e.LeftButton != MouseButtonState.Pressed) return;
            Point stage = ToStage(e.GetPosition(this));
            double dx = stage.X - lastStagePoint.X;
            double dy = stage.Y - lastStagePoint.Y;
            manager.MoveTriggerMask(selectedId, dx, dy);
            lastStagePoint = stage;
            Notify();
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            draggingRegion = false;
            ReleaseMouseCapture();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            double step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 5.0 : 1.0;
            double dx = 0, dy = 0;
            if (e.Key == Key.Left) dx = -step;
            else if (e.Key == Key.Right) dx = step;
            else if (e.Key == Key.Up) dy = -step;
            else if (e.Key == Key.Down) dy = step;
            else return;
            manager.MoveTriggerMask(selectedId, dx, dy);
            Notify();
            e.Handled = true;
        }

        private string HitTrigger(Point stage)
        {
            Rect rt = manager.GetTriggerMaskBounds("rt");
            if (rt.Contains(stage)) return "rt";
            Rect lt = manager.GetTriggerMaskBounds("lt");
            return lt.Contains(stage) ? "lt" : null;
        }

        private void Notify()
        {
            if (Changed != null) Changed();
            InvalidateVisual();
        }

        private void DrawScreenText(DrawingContext dc, string text, double x, double y, double size, Brush brush)
        {
            FormattedText value = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Microsoft YaHei UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(value, new Point(x, y));
        }
    }

    public sealed class XboxTriggerCalibrationWindow : Window
    {
        private readonly XboxRegionManager manager;
        private readonly XboxTriggerCalibrationSurface surface;
        private readonly TextBlock details;
        private readonly TextBlock status;
        private readonly ListBox regions;
        public string StatusMessage { get; private set; }

        public XboxTriggerCalibrationWindow(XboxRegionManager value, ImageSource photo)
        {
            manager = value;
            Title = "Xbox Trigger Mask Calibration";
            Width = 1320;
            Height = 760;
            MinWidth = 980;
            MinHeight = 620;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Palette.WindowBrush;
            Foreground = Palette.TextBrush;
            FontFamily = new FontFamily("Microsoft YaHei UI");
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            Grid root = new Grid { Margin = new Thickness(14) };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
            surface = new XboxTriggerCalibrationSurface(manager, photo);
            Border stage = new Border { Background = Palette.WindowBrush, BorderBrush = Palette.BorderBrush, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 10, 0), Child = surface };
            root.Children.Add(stage);

            StackPanel controls = new StackPanel();
            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = controls };
            Border side = new Border { Background = Palette.SurfaceBrush, BorderBrush = Palette.BorderBrush, BorderThickness = new Thickness(1), Padding = new Thickness(14), Child = scroll };
            Grid.SetColumn(side, 1);
            root.Children.Add(side);
            controls.Children.Add(MakeText("LT / RT 扳机 Mask 调试", 18, Palette.TextBrush, true));
            controls.Children.Add(MakeText("显示 controller.png 与原图坐标 Alpha Mask 边界。固定关闭 Fill、Glow 与动画；Mask 不使用 Bezier 控制点。", 11, Palette.MutedBrush, false));
            regions = new ListBox { Height = 68, Background = Palette.WindowBrush, Foreground = Palette.TextBrush, BorderBrush = Palette.BorderBrush, Margin = new Thickness(0, 4, 0, 8) };
            regions.Items.Add("lt");
            regions.Items.Add("rt");
            regions.SelectedItem = "lt";
            regions.SelectionChanged += delegate { surface.Select(regions.SelectedItem as string); Refresh(); };
            controls.Children.Add(regions);
            details = MakeText(string.Empty, 11, Palette.TextBrush, false);
            controls.Children.Add(details);
            controls.Children.Add(Line());
            controls.Children.Add(MakeText("拖动 Mask 可移动位置；方向键为 1px，Shift + 方向键为 5px。缩放始终等比并保持透明区域中心。", 11, Palette.WarningBrush, false));
            AddButton(controls, "缩小 Mask 1%", delegate { manager.ScaleTriggerMask(surface.SelectedId, 0.99); Refresh(); });
            AddButton(controls, "放大 Mask 1%", delegate { manager.ScaleTriggerMask(surface.SelectedId, 1.01); Refresh(); });
            AddButton(controls, "降低光层透明度 10%", delegate { manager.AdjustTriggerMaskOpacity(surface.SelectedId, -0.10); Refresh(); });
            AddButton(controls, "提高光层透明度 10%", delegate { manager.AdjustTriggerMaskOpacity(surface.SelectedId, 0.10); Refresh(); });
            AddButton(controls, "恢复当前 Mask 默认值", delegate { manager.ResetRegion(surface.SelectedId); Refresh(); });
            AddButton(controls, "保存 xbox-regions.override.json", delegate { string message; manager.SaveUserOverride(out message); StatusMessage = message; status.Text = message; });
            AddButton(controls, "重新加载默认 Mask", delegate { manager.Reload(false); Refresh(); status.Text = manager.LastLoadMessage; });
            AddButton(controls, "重新加载默认 + override", delegate { manager.Reload(false); Refresh(); status.Text = manager.LastLoadMessage; });
            status = MakeText(manager.LastLoadMessage, 11, Palette.MutedBrush, false);
            controls.Children.Add(status);
            Button close = new Button { Content = "关闭", Height = 32, Margin = new Thickness(0, 8, 0, 0), Style = LabVisualStyles.SecondaryButtonStyle };
            close.Click += delegate { Close(); };
            controls.Children.Add(close);
            surface.Changed += Refresh;
            Content = root;
            Loaded += delegate { surface.Focus(); Refresh(); };
        }

        private void RotateRt(double degrees)
        {
            XboxRegionDefinition rt = manager.GetRegion("rt");
            if (rt == null) return;
            double target = Math.Max(-3, Math.Min(3, rt.RotationOffset + degrees));
            manager.RotateTopControlTransform("rt", target - rt.RotationOffset);
            Refresh();
        }

        private void Refresh()
        {
            details.Text = manager.Describe(surface.SelectedId) + "\n" + surface.SelectedHandleText;
            surface.InvalidateVisual();
        }

        private static TextBlock MakeText(string value, double size, Brush brush, bool bold)
        {
            return new TextBlock { Text = value, FontSize = size, Foreground = brush, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 7) };
        }

        private static Border Line() { return new Border { Height = 1, Background = Palette.BorderBrush, Margin = new Thickness(0, 9, 0, 7) }; }
        private static void AddButton(Panel panel, string label, RoutedEventHandler handler) { Button button = new Button { Content = label, Height = 30, Margin = new Thickness(0, 3, 0, 0), Style = LabVisualStyles.SecondaryButtonStyle }; button.Click += handler; panel.Children.Add(button); }
    }

    // This DPad-only tool is separate from the general Xbox calibration window.
    // It never exposes non-DPad regions to editing.
    public sealed class XboxDPadCalibrationSurface : FrameworkElement
    {
        private static readonly Rect FocusViewport = new Rect(500, 400, 220, 218);
        private readonly XboxRegionManager manager;
        private readonly ImageSource photo;
        private string selectedId;
        private Point lastStagePoint;
        private bool dragging;
        private XboxDPadEdgeAnalysis edgeAnalysis;
        private XboxDPadAutoCalibrationResult recommendation;

        public event Action Changed;
        public string PointerCoordinates { get; private set; }
        public string SelectedId { get { return selectedId; } }
        public string SelectedHandleText { get { return selectedId + "Transform（基础 Path 已锁定）"; } }

        public XboxDPadCalibrationSurface(XboxRegionManager value, ImageSource source, string initialId)
        {
            manager = value;
            photo = source;
            selectedId = IsDPadId(initialId) ? initialId : "dpad-up";
            Focusable = true;
            Cursor = Cursors.Cross;
            SnapsToDevicePixels = true;
            PointerCoordinates = "原图坐标  X --  Y --";
            MouseLeftButtonDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseUp;
            PreviewKeyDown += OnKeyDown;
        }

        public void Select(string id)
        {
            if (!IsDPadId(id)) return;
            selectedId = id;
            edgeAnalysis = null;
            recommendation = null;
            if (Changed != null) Changed();
            InvalidateVisual();
        }

        public void ShowAutoCalibration(XboxDPadEdgeAnalysis analysis, XboxDPadAutoCalibrationResult result)
        {
            edgeAnalysis = analysis;
            recommendation = result;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            Matrix matrix = CreateFocusMatrix();
            double scale = matrix.M11;
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));
            dc.PushTransform(new MatrixTransform(matrix));
            manager.DrawPhoto(dc, photo);
            string[] ids = { "dpad-up", "dpad-down", "dpad-left", "dpad-right" };
            for (int i = 0; i < ids.Length; i++)
            {
                if (string.Equals(ids[i], selectedId, StringComparison.OrdinalIgnoreCase))
                {
                    DrawEdgeAnalysis(dc, scale);
                    XboxRegionManager.DrawDPadCalibrationAdorners(dc, manager, ids[i], scale, null);
                }
                else
                {
                    Geometry other = manager.GetGeometry(ids[i]);
                    if (other != null) dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(150, Palette.Blue.R, Palette.Blue.G, Palette.Blue.B)), 1.0 / Math.Max(0.001, scale)), other);
                }
            }
            dc.Pop();
            dc.Pop();
            DrawScreenText(dc, "Xbox DPad 自动校准 · 原始边缘=红 · 已匹配=绿 · 差异=黄 · 无 Glow / 1px", 12, 12, 12, Palette.WarningBrush);
            DrawScreenText(dc, PointerCoordinates, 12, Math.Max(30, ActualHeight - 24), 11, Palette.TextBrush);
        }

        private Matrix CreateFocusMatrix()
        {
            double scale = Math.Min(ActualWidth / FocusViewport.Width, ActualHeight / FocusViewport.Height);
            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0) scale = 1.0;
            double x = (ActualWidth - FocusViewport.Width * scale) * 0.5 - FocusViewport.X * scale;
            double y = (ActualHeight - FocusViewport.Height * scale) * 0.5 - FocusViewport.Y * scale;
            return new Matrix(scale, 0, 0, scale, x, y);
        }

        private Point ToStage(Point screen)
        {
            Matrix matrix = CreateFocusMatrix();
            matrix.Invert();
            return matrix.Transform(screen);
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            Point stage = ToStage(e.GetPosition(this));
            Geometry geometry = manager.GetGeometry(selectedId);
            if (geometry == null || !geometry.FillContains(stage)) return;
            lastStagePoint = stage;
            dragging = true;
            CaptureMouse();
            UpdatePointer(stage);
            if (Changed != null) Changed();
            InvalidateVisual();
            e.Handled = true;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            Point stage = ToStage(e.GetPosition(this));
            UpdatePointer(stage);
            if (dragging && e.LeftButton == MouseButtonState.Pressed)
            {
                manager.MoveDPadTransform(selectedId, stage.X - lastStagePoint.X, stage.Y - lastStagePoint.Y);
                lastStagePoint = stage;
                if (Changed != null) Changed();
            }
            InvalidateVisual();
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            dragging = false;
            ReleaseMouseCapture();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            double step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 5.0 : 1.0;
            double dx = 0, dy = 0;
            if (e.Key == Key.Left) dx = -step;
            else if (e.Key == Key.Right) dx = step;
            else if (e.Key == Key.Up) dy = -step;
            else if (e.Key == Key.Down) dy = step;
            else return;
            bool changed = manager.MoveDPadTransform(selectedId, dx, dy);
            if (changed)
            {
                if (Changed != null) Changed();
                InvalidateVisual();
            }
            e.Handled = true;
        }

        private void DrawEdgeAnalysis(DrawingContext dc, double scale)
        {
            if (edgeAnalysis == null) return;
            double pixel = 1.0 / Math.Max(0.001, scale);
            Brush rawEdge = new SolidColorBrush(Color.FromArgb(110, 255, 74, 90));
            IList<Point> edges = edgeAnalysis.EdgePoints;
            for (int i = 0; i < edges.Count; i++) dc.DrawRectangle(rawEdge, null, new Rect(edges[i].X - pixel * 0.5, edges[i].Y - pixel * 0.5, pixel, pixel));
            List<Point> boundary = manager.GetDPadBoundarySamples(selectedId, 1.6);
            Brush matched = new SolidColorBrush(Color.FromArgb(210, 90, 220, 130));
            Brush difference = new SolidColorBrush(Color.FromArgb(230, 255, 190, 48));
            double radius = pixel * 0.8;
            for (int i = 0; i < boundary.Count; i++)
            {
                Brush brush = edgeAnalysis.DistanceAtStage(boundary[i]) <= 1.25 ? matched : difference;
                dc.DrawEllipse(brush, null, boundary[i], radius, radius);
            }
            if (recommendation != null)
            {
                FormattedText value = new FormattedText(string.Format(CultureInfo.InvariantCulture, "推荐误差 {0:0.00}px", recommendation.AverageEdgeDistance), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Microsoft YaHei UI"), 11, Palette.GreenBrush, 1.0);
                dc.DrawText(value, new Point(FocusViewport.X + 3, FocusViewport.Y + 4));
            }
        }

        private void UpdatePointer(Point stage)
        {
            Point source = new Point(stage.X / XboxRegionManager.SourceScale, (stage.Y - XboxRegionManager.SourceTop) / XboxRegionManager.SourceScale);
            PointerCoordinates = string.Format(CultureInfo.InvariantCulture, "逻辑 X {0:0.00}, Y {1:0.00}   原图 X {2:0.00}, Y {3:0.00}", stage.X, stage.Y, source.X, source.Y);
        }

        private static bool IsDPadId(string id)
        {
            return string.Equals(id, "dpad-up", StringComparison.OrdinalIgnoreCase) || string.Equals(id, "dpad-down", StringComparison.OrdinalIgnoreCase) || string.Equals(id, "dpad-left", StringComparison.OrdinalIgnoreCase) || string.Equals(id, "dpad-right", StringComparison.OrdinalIgnoreCase);
        }

        private void DrawScreenText(DrawingContext dc, string text, double x, double y, double size, Brush brush)
        {
            FormattedText value = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(value, new Point(x, y));
        }
    }

    public sealed class XboxDPadCalibrationWindow : Window
    {
        private readonly XboxRegionManager manager;
        private readonly XboxDPadCalibrationSurface surface;
        private readonly TextBlock details;
        private readonly TextBlock status;
        private readonly TextBlock recommendationText;
        private readonly ListBox regions;
        private readonly ImageSource photo;
        private XboxDPadAutoCalibrationResult recommendation;
        public string StatusMessage { get; private set; }

        public XboxDPadCalibrationWindow(XboxRegionManager value, ImageSource photo, string initialId)
        {
            manager = value;
            this.photo = photo;
            Title = "Xbox DPad Precision Calibration";
            Width = 1260;
            Height = 820;
            MinWidth = 980;
            MinHeight = 620;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Palette.WindowBrush;
            Foreground = Palette.TextBrush;
            FontFamily = new FontFamily("Microsoft YaHei UI");
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            Grid root = new Grid { Margin = new Thickness(14) };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(310) });
            surface = new XboxDPadCalibrationSurface(manager, photo, initialId);
            Border stage = new Border { Background = Palette.WindowBrush, BorderBrush = Palette.BorderBrush, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 10, 0), Child = surface };
            root.Children.Add(stage);

            StackPanel controls = new StackPanel { Background = Palette.SurfaceBrush };
            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = controls };
            Border side = new Border { Background = Palette.SurfaceBrush, BorderBrush = Palette.BorderBrush, BorderThickness = new Thickness(1), Padding = new Thickness(14), Child = scroll };
            Grid.SetColumn(side, 1);
            root.Children.Add(side);
            controls.Children.Add(MakeText("Xbox DPad 四方向校准", 18, Palette.TextBrush, true));
            controls.Children.Add(MakeText("底图为原始 controller.png；画布只是放大相机，路径仍在 1536×1024 逻辑坐标中。", 11, Palette.MutedBrush, false));
            controls.Children.Add(MakeText("黄色=P锚点；绿色=C1；青蓝=C2；白色十字=当前中心；青蓝框=最终边界。", 11, Palette.MutedBrush, false));
            regions = new ListBox { Height = 112, Background = Palette.WindowBrush, Foreground = Palette.TextBrush, BorderBrush = Palette.BorderBrush, Margin = new Thickness(0, 3, 0, 8) };
            regions.Items.Add("dpad-up");
            regions.Items.Add("dpad-down");
            regions.Items.Add("dpad-left");
            regions.Items.Add("dpad-right");
            regions.SelectedItem = surface.SelectedId;
            regions.SelectionChanged += delegate { surface.Select(regions.SelectedItem as string); Refresh(); };
            controls.Children.Add(regions);
            controls.Children.Add(Line());
            details = MakeText(string.Empty, 11, Palette.TextBrush, false);
            controls.Children.Add(details);
            controls.Children.Add(Line());
            controls.Children.Add(MakeText("点击控制点后可拖动。方向键移动 1 个逻辑像素；Shift + 方向键移动 5 个逻辑像素。", 11, Palette.WarningBrush, false));
            controls.Children.Add(MakeText("派生方向：拖动或方向键调整 Offset；下列按钮调整各自的 Scale 与 RotationOffset。", 11, Palette.MutedBrush, false));
            AddButton(controls, "派生方向缩小 1%", delegate { AdjustSelectedDerived(0.99, 0.99, 0); });
            AddButton(controls, "派生方向放大 1%", delegate { AdjustSelectedDerived(1.01, 1.01, 0); });
            AddButton(controls, "派生方向仅 X 缩小 1%", delegate { AdjustSelectedDerived(0.99, 1, 0); });
            AddButton(controls, "派生方向仅 X 放大 1%", delegate { AdjustSelectedDerived(1.01, 1, 0); });
            AddButton(controls, "派生方向仅 Y 缩小 1%", delegate { AdjustSelectedDerived(1, 0.99, 0); });
            AddButton(controls, "派生方向仅 Y 放大 1%", delegate { AdjustSelectedDerived(1, 1.01, 0); });
            AddButton(controls, "派生方向逆时针 0.25°", delegate { AdjustSelectedDerived(1, 1, -0.25); });
            AddButton(controls, "派生方向顺时针 0.25°", delegate { AdjustSelectedDerived(1, 1, 0.25); });
            AddButton(controls, "恢复当前方向默认轮廓", delegate { manager.ResetRegion(surface.SelectedId); Refresh(); });
            AddButton(controls, "保存到 xbox-regions.override.json", delegate { string message; manager.SaveUserOverride(out message); StatusMessage = message; status.Text = message; });
            AddButton(controls, "重新加载默认 + override", delegate { manager.Reload(false); Refresh(); status.Text = manager.LastLoadMessage; });
            status = MakeText(manager.LastLoadMessage, 11, Palette.MutedBrush, false);
            controls.Children.Add(status);
            Button close = new Button { Content = "关闭", Height = 32, Margin = new Thickness(0, 8, 0, 0), Style = LabVisualStyles.SecondaryButtonStyle };
            close.Click += delegate { Close(); };
            controls.Children.Add(close);
            controls.Children.Add(Line());
            controls.Children.Add(MakeText("自动搜索范围：Offset ±10px · Scale ±5% · Rotation ±3°。基础 Path 不会被修改。", 11, Palette.WarningBrush, true));
            AddButton(controls, "分析原始边缘并搜索推荐 Transform", delegate { SearchRecommendation(); });
            recommendationText = MakeText("尚未执行自动搜索。", 11, Palette.MutedBrush, false);
            controls.Children.Add(recommendationText);
            AddButton(controls, "应用推荐参数", delegate { ApplyRecommendation(); });
            surface.Changed += Refresh;
            Content = root;
            Loaded += delegate { surface.Focus(); Refresh(); };
        }

        private void Refresh()
        {
            details.Text = manager.Describe(surface.SelectedId) + "\n当前选择：" + surface.SelectedHandleText + "\n" + surface.PointerCoordinates;
            surface.InvalidateVisual();
        }

        private void AdjustSelectedDerived(double scaleX, double scaleY, double rotation)
        {
            if (!manager.IsDPadTransformRegion(surface.SelectedId))
            {
                status.Text = "DPadUp 是基础路径；请编辑它的控制点。";
                return;
            }
            bool changed = rotation == 0
                ? manager.ScaleDPadTransform(surface.SelectedId, scaleX, scaleY)
                : manager.RotateDPadTransform(surface.SelectedId, rotation);
            if (changed) Refresh();
        }

        private void SearchRecommendation()
        {
            try
            {
                XboxDPadEdgeAnalysis analysis = manager.BuildDPadEdgeAnalysis(photo as BitmapSource, surface.SelectedId);
                recommendation = manager.FindDPadTransformRecommendation(surface.SelectedId, analysis);
                if (recommendation == null)
                {
                    recommendationText.Text = "未能从当前原图区域生成可用的边缘评分。";
                    return;
                }
                recommendationText.Text = recommendation.Describe();
                surface.ShowAutoCalibration(analysis, recommendation);
                status.Text = "已完成局部边缘搜索；请检查红/绿/黄差异预览，再决定是否应用。";
            }
            catch (Exception ex)
            {
                recommendationText.Text = "自动搜索失败：" + ex.Message;
            }
        }

        private void ApplyRecommendation()
        {
            if (recommendation == null || !string.Equals(recommendation.RegionId, surface.SelectedId, StringComparison.OrdinalIgnoreCase))
            {
                status.Text = "请先对当前方向执行自动搜索。";
                return;
            }
            if (manager.ApplyDPadTransformRecommendation(recommendation))
            {
                status.Text = "已应用推荐 Transform；基础 Path 保持不变。";
                Refresh();
            }
        }

        private static TextBlock MakeText(string value, double size, Brush brush, bool bold)
        {
            return new TextBlock { Text = value, FontSize = size, Foreground = brush, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 7) };
        }

        private static Border Line() { return new Border { Height = 1, Background = Palette.BorderBrush, Margin = new Thickness(0, 9, 0, 7) }; }
        private static void AddButton(Panel panel, string label, RoutedEventHandler handler) { Button button = new Button { Content = label, Height = 30, Margin = new Thickness(0, 3, 0, 0), Style = LabVisualStyles.SecondaryButtonStyle }; button.Click += handler; panel.Children.Add(button); }
    }
}
