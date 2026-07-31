using System;
using System.Collections.Generic;
using System.Globalization;

namespace ControllerLab
{
    public enum JoystickTestMode
    {
        Idle,
        StationaryCountdown,
        StationarySampling,
        CircularityLeft,
        CircularityRight,
        ReturnTest,
        Cancelled
    }

    public enum StickReturnDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    public enum StickHealthStatus
    {
        Excellent,
        Good,
        Warning,
        Critical,
        NotTested
    }

    // Thresholds are centralized here so the UI never owns diagnostic rules.
    public static class JoystickAnalysisConfiguration
    {
        public const int CircularityBinCount = 72;
        public const int MinimumStationarySamples = 30;
        public const int MinimumCircularitySamples = 72;
        public const int MinimumReturnSamples = 12;
        public const double OuterCoverageRadius = 0.80;
        public const double CircularSampleRadius = 0.35;
        public const double CenterThreshold = 0.05;
        public const double StableCenterThreshold = 0.045;
        public const double BounceThreshold = 0.075;
        public const double ReturnStartDelta = 0.035;
        public const double OvershootWarningPercent = 3.0;
        public const double SlowReturnMilliseconds = 260.0;
        public const double PersistentJitterPercent = 1.5;
        public const double MinimumDeadzonePercent = 1.0;
        public const double MaximumDeadzonePercent = 30.0;
        public const double HighDeadzoneWarningPercent = 15.0;
    }

    public sealed class JoystickStationaryResult
    {
        public StickSide StickSide;
        public bool IsValid;
        public string InvalidReason = string.Empty;
        public int SampleCount;
        public double SamplingFrequencyHz;
        public double AverageXPercent;
        public double AverageYPercent;
        public double CenterOffsetPercent;
        public double MaximumOffsetPercent;
        public double StandardDeviationXPercent;
        public double StandardDeviationYPercent;
        public double NoiseLevelPercent;
        public string DriftDirection = "未检测";
        public int StabilityScore;
    }

    public sealed class StickCircularityResult
    {
        public StickSide StickSide;
        public bool IsValid;
        public string InvalidReason = string.Empty;
        public int SampleCount;
        public double SamplingFrequencyHz;
        public int AngleBinCount;
        public double[] MaximumRadiusByAngle = new double[0];
        public double MaximumXPositivePercent;
        public double MaximumXNegativePercent;
        public double MaximumYPositivePercent;
        public double MaximumYNegativePercent;
        public double MaximumRadiusPercent;
        public double MinimumEffectiveRadiusPercent;
        public double AverageRadiusPercent;
        public double OuterCoveragePercent;
        public double CircularityErrorPercent;
        public double QuadrantCoveragePercent;
        public double DirectionalAsymmetryPercent;
        public bool HasMissingTravel;
        public bool IsSquareLimited;
        public bool HasCornerCutting;
        public string DirectionConsistency = "未检测";
        public string MissingDirections = "未检测";
    }

    public sealed class DeadzoneRecommendation
    {
        public StickSide StickSide;
        public double DetectedMaximumOffsetPercent;
        public double NoiseMarginPercent;
        public double MinimumDeadzonePercent;
        public double RecommendedDeadzonePercent;
        public double StableDeadzonePercent;
        public double CircularDeadzonePercent;
        public double AxialDeadzoneXPercent;
        public double AxialDeadzoneYPercent;
        public bool IsAbnormallyHigh;
        public string Warning = string.Empty;
    }

    public sealed class StickReturnResult
    {
        public StickSide StickSide;
        public StickReturnDirection Direction;
        public bool IsValid;
        public string InvalidReason = string.Empty;
        public int SampleCount;
        public double SamplingFrequencyHz;
        public double ReturnStartTimeMilliseconds;
        public double FirstCenterEntryTimeMilliseconds;
        public double StableCenterTimeMilliseconds;
        public double ReturnDurationMilliseconds;
        public double OvershootPercent;
        public int BounceCount;
        public double SettlingTimeMilliseconds;
        public double FinalOffsetPercent;
        public bool IsSlowReturn;
        public bool HasOvershoot;
        public bool HasPersistentJitter;
        public bool FailedToStabilize;
    }

    public sealed class StickHealthScoreResult
    {
        public int Score;
        public StickHealthStatus Status = StickHealthStatus.NotTested;
        public string EnglishStatus = "Not Tested";
        public string ChineseStatus = "未检测";
        public bool IsComplete;
        public double CenterStabilityScore;
        public double NoiseScore;
        public double CircularCoverageScore;
        public double CircularityScore;
        public double DirectionConsistencyScore;
        public double ReturnSpeedScore;
        public double BounceScore;
        public double DeadzoneScore;
    }

    public sealed class StickTestResult
    {
        public StickSide StickSide;
        public JoystickStationaryResult Stationary;
        public StickCircularityResult Circularity;
        public DeadzoneRecommendation Deadzone;
        public readonly List<StickReturnResult> ReturnResults = new List<StickReturnResult>();
        public StickHealthScoreResult Health = new StickHealthScoreResult();
    }

    public static class JoystickAnalyzer
    {
        public static JoystickStationaryResult AnalyzeStationary(StickSide side, IList<StickSample> samples)
        {
            JoystickStationaryResult result = new JoystickStationaryResult { StickSide = side };
            int count = samples == null ? 0 : samples.Count;
            result.SampleCount = count;
            if (count < JoystickAnalysisConfiguration.MinimumStationarySamples)
            {
                result.InvalidReason = "有效采样不足，无法生成静止漂移结论。";
                return result;
            }

            double sumX = 0;
            double sumY = 0;
            double maximum = 0;
            for (int i = 0; i < count; i++)
            {
                StickSample sample = SafeSample(samples[i]);
                sumX += sample.X;
                sumY += sample.Y;
                maximum = Math.Max(maximum, Radius(sample.X, sample.Y));
            }
            double averageX = sumX / count;
            double averageY = sumY / count;
            double varianceX = 0;
            double varianceY = 0;
            for (int i = 0; i < count; i++)
            {
                StickSample sample = SafeSample(samples[i]);
                varianceX += Square(sample.X - averageX);
                varianceY += Square(sample.Y - averageY);
            }
            double standardDeviationX = Math.Sqrt(varianceX / count);
            double standardDeviationY = Math.Sqrt(varianceY / count);
            double noise = Math.Sqrt(Square(standardDeviationX) + Square(standardDeviationY));
            double center = Radius(averageX, averageY);

            result.IsValid = true;
            result.SamplingFrequencyHz = SamplingFrequency(samples);
            result.AverageXPercent = averageX * 100.0;
            result.AverageYPercent = averageY * 100.0;
            result.CenterOffsetPercent = center * 100.0;
            result.MaximumOffsetPercent = maximum * 100.0;
            result.StandardDeviationXPercent = standardDeviationX * 100.0;
            result.StandardDeviationYPercent = standardDeviationY * 100.0;
            result.NoiseLevelPercent = noise * 100.0;
            result.DriftDirection = DirectionLabel(averageX, averageY, center);
            result.StabilityScore = (int)Math.Round(Clamp(100.0 - result.CenterOffsetPercent * 5.0 - result.NoiseLevelPercent * 9.0 - Math.Max(0.0, result.MaximumOffsetPercent - result.CenterOffsetPercent) * 1.5, 0.0, 100.0));
            return result;
        }

        public static DeadzoneRecommendation RecommendDeadzone(StickSide side, JoystickStationaryResult stationary)
        {
            return RecommendDeadzone(side, stationary, 0.5);
        }

        public static DeadzoneRecommendation RecommendDeadzone(StickSide side, JoystickStationaryResult stationary, double safetyMarginPercent)
        {
            DeadzoneRecommendation result = new DeadzoneRecommendation { StickSide = side };
            if (stationary == null || !stationary.IsValid) return result;

            double configuredSafetyMargin = Clamp(safetyMarginPercent, 0.5, 5.0);
            double axisNoise = Math.Max(stationary.StandardDeviationXPercent, stationary.StandardDeviationYPercent);
            double noiseMargin = Math.Max(configuredSafetyMargin, axisNoise * 2.5 + stationary.NoiseLevelPercent * 0.35);
            double minimum = CeilingHalf(stationary.MaximumOffsetPercent + noiseMargin);
            double recommended = CeilingHalf(minimum + Math.Max(1.0, noiseMargin * 0.75));
            double stable = CeilingHalf(recommended + Math.Max(2.0, noiseMargin * 1.25));
            minimum = Clamp(minimum, JoystickAnalysisConfiguration.MinimumDeadzonePercent, JoystickAnalysisConfiguration.MaximumDeadzonePercent);
            recommended = Clamp(Math.Max(minimum, recommended), JoystickAnalysisConfiguration.MinimumDeadzonePercent, JoystickAnalysisConfiguration.MaximumDeadzonePercent);
            stable = Clamp(Math.Max(recommended, stable), JoystickAnalysisConfiguration.MinimumDeadzonePercent, JoystickAnalysisConfiguration.MaximumDeadzonePercent);

            result.DetectedMaximumOffsetPercent = stationary.MaximumOffsetPercent;
            result.NoiseMarginPercent = noiseMargin;
            result.MinimumDeadzonePercent = minimum;
            result.RecommendedDeadzonePercent = recommended;
            result.StableDeadzonePercent = stable;
            result.CircularDeadzonePercent = recommended;
            result.AxialDeadzoneXPercent = Clamp(CeilingHalf(Math.Abs(stationary.AverageXPercent) + stationary.StandardDeviationXPercent * 2.5 + configuredSafetyMargin), JoystickAnalysisConfiguration.MinimumDeadzonePercent, JoystickAnalysisConfiguration.MaximumDeadzonePercent);
            result.AxialDeadzoneYPercent = Clamp(CeilingHalf(Math.Abs(stationary.AverageYPercent) + stationary.StandardDeviationYPercent * 2.5 + configuredSafetyMargin), JoystickAnalysisConfiguration.MinimumDeadzonePercent, JoystickAnalysisConfiguration.MaximumDeadzonePercent);
            result.IsAbnormallyHigh = recommended >= JoystickAnalysisConfiguration.HighDeadzoneWarningPercent || stationary.MaximumOffsetPercent >= 12.0;
            if (result.IsAbnormallyHigh) result.Warning = "摇杆可能存在明显漂移，仅依靠增加死区无法彻底解决。";
            return result;
        }

        public static StickCircularityResult AnalyzeCircularity(StickSide side, IList<StickSample> samples)
        {
            int bins = JoystickAnalysisConfiguration.CircularityBinCount;
            StickCircularityResult result = new StickCircularityResult
            {
                StickSide = side,
                AngleBinCount = bins,
                MaximumRadiusByAngle = new double[bins],
                SampleCount = samples == null ? 0 : samples.Count
            };
            if (samples == null || samples.Count < JoystickAnalysisConfiguration.MinimumCircularitySamples)
            {
                result.InvalidReason = "圆周测试有效采样不足。";
                return result;
            }

            double maxXPositive = 0;
            double maxXNegative = 0;
            double maxYPositive = 0;
            double maxYNegative = 0;
            double maximumRadius = 0;
            for (int i = 0; i < samples.Count; i++)
            {
                StickSample sample = SafeSample(samples[i]);
                double radius = Radius(sample.X, sample.Y);
                maxXPositive = Math.Max(maxXPositive, sample.X);
                maxXNegative = Math.Max(maxXNegative, -sample.X);
                maxYPositive = Math.Max(maxYPositive, sample.Y);
                maxYNegative = Math.Max(maxYNegative, -sample.Y);
                maximumRadius = Math.Max(maximumRadius, radius);
                if (radius < JoystickAnalysisConfiguration.CircularSampleRadius) continue;
                double angle = Math.Atan2(sample.Y, sample.X);
                if (angle < 0) angle += Math.PI * 2.0;
                int bin = Math.Min(bins - 1, (int)Math.Floor(angle / (Math.PI * 2.0) * bins));
                result.MaximumRadiusByAngle[bin] = Math.Max(result.MaximumRadiusByAngle[bin], radius);
            }

            int covered = 0;
            int present = 0;
            double radiusSum = 0;
            double minimumEffective = double.MaxValue;
            int[] quadrantCovered = new int[4];
            List<double> presentRadii = new List<double>();
            for (int i = 0; i < bins; i++)
            {
                double radius = result.MaximumRadiusByAngle[i];
                if (radius > 0)
                {
                    present++;
                    radiusSum += radius;
                    presentRadii.Add(radius);
                    minimumEffective = Math.Min(minimumEffective, radius);
                }
                if (radius >= JoystickAnalysisConfiguration.OuterCoverageRadius)
                {
                    covered++;
                    quadrantCovered[Math.Min(3, i / (bins / 4))]++;
                }
            }
            if (minimumEffective == double.MaxValue) minimumEffective = 0;
            double medianRadius = Median(presentRadii);
            double squaredError = 0;
            for (int i = 0; i < bins; i++)
            {
                double radius = result.MaximumRadiusByAngle[i];
                double comparable = radius > 0 ? radius : 0;
                squaredError += Square(comparable - medianRadius);
            }
            double relativeRmse = medianRadius <= 0 ? 1.0 : Math.Sqrt(squaredError / bins) / medianRadius;
            double excessiveCornerPenalty = Math.Max(0.0, maximumRadius - 1.02) * 70.0;
            double circularityError = relativeRmse * 100.0 + excessiveCornerPenalty;
            double minimumQuadrantCoverage = 100.0;
            for (int i = 0; i < 4; i++) minimumQuadrantCoverage = Math.Min(minimumQuadrantCoverage, quadrantCovered[i] * 100.0 / (bins / 4));

            double[] directions = { maxXPositive, maxXNegative, maxYPositive, maxYNegative };
            double directionMean = Mean(directions);
            double directionRange = Maximum(directions) - Minimum(directions);
            double asymmetry = directionMean <= 0 ? 100.0 : directionRange / directionMean * 100.0;
            double axialRadius = DirectionBandAverage(result.MaximumRadiusByAngle, false);
            double diagonalRadius = DirectionBandAverage(result.MaximumRadiusByAngle, true);
            bool squareLimited = axialRadius > 0.70 && diagonalRadius > axialRadius * 1.12 && maximumRadius > 1.08;
            bool cornerCutting = diagonalRadius > 0 && axialRadius > 0 && diagonalRadius < axialRadius * 0.88;
            string missing = MissingDirectionLabel(result.MaximumRadiusByAngle, directions, quadrantCovered);

            result.IsValid = present >= bins / 2;
            if (!result.IsValid) result.InvalidReason = "角度覆盖不足，未形成可分析的完整圆周。";
            result.SamplingFrequencyHz = SamplingFrequency(samples);
            result.MaximumXPositivePercent = maxXPositive * 100.0;
            result.MaximumXNegativePercent = maxXNegative * 100.0;
            result.MaximumYPositivePercent = maxYPositive * 100.0;
            result.MaximumYNegativePercent = maxYNegative * 100.0;
            result.MaximumRadiusPercent = maximumRadius * 100.0;
            result.MinimumEffectiveRadiusPercent = minimumEffective * 100.0;
            result.AverageRadiusPercent = present == 0 ? 0 : radiusSum / present * 100.0;
            result.OuterCoveragePercent = covered * 100.0 / bins;
            result.CircularityErrorPercent = Clamp(circularityError, 0.0, 100.0);
            result.QuadrantCoveragePercent = minimumQuadrantCoverage;
            result.DirectionalAsymmetryPercent = asymmetry;
            result.IsSquareLimited = squareLimited;
            result.HasCornerCutting = cornerCutting;
            result.HasMissingTravel = covered < bins * 0.85 || Minimum(directions) < 0.85;
            result.DirectionConsistency = asymmetry <= 5.0 ? "优秀" : asymmetry <= 10.0 ? "良好" : asymmetry <= 18.0 ? "需要注意" : "严重不一致";
            result.MissingDirections = missing;
            return result;
        }

        public static StickReturnResult AnalyzeReturn(StickSide side, StickReturnDirection direction, IList<StickSample> samples)
        {
            StickReturnResult result = new StickReturnResult { StickSide = side, Direction = direction, SampleCount = samples == null ? 0 : samples.Count };
            if (samples == null || samples.Count < JoystickAnalysisConfiguration.MinimumReturnSamples)
            {
                result.InvalidReason = "回中测试有效采样不足。";
                return result;
            }

            StickSample first = SafeSample(samples[0]);
            DateTime origin = first.Timestamp;
            double heldProjection = 0;
            int initialWindow = Math.Min(samples.Count, Math.Max(3, samples.Count / 8));
            for (int i = 0; i < initialWindow; i++) heldProjection = Math.Max(heldProjection, Projection(SafeSample(samples[i]), direction));
            if (heldProjection < 0.55)
            {
                result.InvalidReason = "未检测到指定方向的充分外推行程。";
                return result;
            }

            int startIndex = -1;
            for (int i = 1; i < samples.Count; i++)
            {
                double projection = Projection(SafeSample(samples[i]), direction);
                if (projection <= heldProjection - JoystickAnalysisConfiguration.ReturnStartDelta)
                {
                    startIndex = i;
                    break;
                }
            }
            if (startIndex < 0)
            {
                result.InvalidReason = "未检测到摇杆开始回中。";
                return result;
            }

            int firstCenterIndex = -1;
            int stableIndex = -1;
            DateTime stableCandidate = DateTime.MinValue;
            for (int i = startIndex; i < samples.Count; i++)
            {
                StickSample sample = SafeSample(samples[i]);
                double radius = Radius(sample.X, sample.Y);
                if (firstCenterIndex < 0 && radius <= JoystickAnalysisConfiguration.CenterThreshold) firstCenterIndex = i;
                if (radius <= JoystickAnalysisConfiguration.StableCenterThreshold)
                {
                    if (stableCandidate == DateTime.MinValue) stableCandidate = sample.Timestamp;
                    if ((sample.Timestamp - stableCandidate).TotalMilliseconds >= 150.0)
                    {
                        stableIndex = i;
                        break;
                    }
                }
                else stableCandidate = DateTime.MinValue;
            }

            double overshoot = 0;
            int bounceCount = 0;
            bool outsideBounce = false;
            int bounceStart = firstCenterIndex >= 0 ? firstCenterIndex : startIndex;
            for (int i = bounceStart; i < samples.Count; i++)
            {
                StickSample sample = SafeSample(samples[i]);
                overshoot = Math.Max(overshoot, Math.Max(0.0, -Projection(sample, direction)));
                double radius = Radius(sample.X, sample.Y);
                if (!outsideBounce && radius >= JoystickAnalysisConfiguration.BounceThreshold)
                {
                    outsideBounce = true;
                    bounceCount++;
                }
                else if (outsideBounce && radius <= JoystickAnalysisConfiguration.CenterThreshold) outsideBounce = false;
            }

            int tailCount = Math.Min(20, samples.Count);
            double finalSum = 0;
            double finalSumSquared = 0;
            for (int i = samples.Count - tailCount; i < samples.Count; i++)
            {
                StickSample sample = SafeSample(samples[i]);
                double radius = Radius(sample.X, sample.Y);
                finalSum += radius;
                finalSumSquared += radius * radius;
            }
            double finalMean = finalSum / tailCount;
            double finalVariance = Math.Max(0.0, finalSumSquared / tailCount - finalMean * finalMean);
            double finalJitterPercent = Math.Sqrt(finalVariance) * 100.0;

            DateTime startTime = SafeSample(samples[startIndex]).Timestamp;
            result.IsValid = true;
            result.SamplingFrequencyHz = SamplingFrequency(samples);
            result.ReturnStartTimeMilliseconds = Math.Max(0.0, (startTime - origin).TotalMilliseconds);
            result.FirstCenterEntryTimeMilliseconds = firstCenterIndex < 0 ? -1 : (SafeSample(samples[firstCenterIndex]).Timestamp - origin).TotalMilliseconds;
            result.StableCenterTimeMilliseconds = stableIndex < 0 ? -1 : (SafeSample(samples[stableIndex]).Timestamp - origin).TotalMilliseconds;
            result.ReturnDurationMilliseconds = firstCenterIndex < 0 ? -1 : (SafeSample(samples[firstCenterIndex]).Timestamp - startTime).TotalMilliseconds;
            result.OvershootPercent = overshoot * 100.0;
            result.BounceCount = bounceCount;
            result.SettlingTimeMilliseconds = firstCenterIndex < 0 || stableIndex < 0 ? -1 : (SafeSample(samples[stableIndex]).Timestamp - SafeSample(samples[firstCenterIndex]).Timestamp).TotalMilliseconds;
            result.FinalOffsetPercent = finalMean * 100.0;
            result.IsSlowReturn = result.ReturnDurationMilliseconds < 0 || result.ReturnDurationMilliseconds > JoystickAnalysisConfiguration.SlowReturnMilliseconds;
            result.HasOvershoot = result.OvershootPercent >= JoystickAnalysisConfiguration.OvershootWarningPercent;
            result.HasPersistentJitter = finalJitterPercent >= JoystickAnalysisConfiguration.PersistentJitterPercent;
            result.FailedToStabilize = stableIndex < 0;
            return result;
        }

        public static StickHealthScoreResult CalculateHealth(StickTestResult result)
        {
            StickHealthScoreResult health = new StickHealthScoreResult();
            if (result == null || result.Stationary == null || !result.Stationary.IsValid || result.Circularity == null || !result.Circularity.IsValid || result.Deadzone == null || CountValidDirections(result.ReturnResults) < 4)
            {
                SetHealthStatus(health, StickHealthStatus.NotTested);
                return health;
            }

            health.IsComplete = true;
            health.CenterStabilityScore = Clamp(result.Stationary.StabilityScore, 0, 100);
            health.NoiseScore = Clamp(100.0 - result.Stationary.NoiseLevelPercent * 18.0, 0, 100);
            health.CircularCoverageScore = Clamp((result.Circularity.OuterCoveragePercent - 60.0) * 2.5, 0, 100);
            health.CircularityScore = Clamp(100.0 - result.Circularity.CircularityErrorPercent * 7.0 - (result.Circularity.IsSquareLimited ? 25.0 : 0.0) - (result.Circularity.HasCornerCutting ? 18.0 : 0.0), 0, 100);
            health.DirectionConsistencyScore = Clamp(100.0 - result.Circularity.DirectionalAsymmetryPercent * 5.0, 0, 100);

            double returnSpeed = 0;
            double bounce = 0;
            int valid = 0;
            for (int i = 0; i < result.ReturnResults.Count; i++)
            {
                StickReturnResult item = result.ReturnResults[i];
                if (item == null || !item.IsValid) continue;
                valid++;
                double duration = item.ReturnDurationMilliseconds < 0 ? 450.0 : item.ReturnDurationMilliseconds;
                returnSpeed += Clamp(100.0 - Math.Max(0.0, duration - 110.0) * (100.0 / 290.0), 0, 100);
                bounce += Clamp(100.0 - item.BounceCount * 18.0 - item.OvershootPercent * 3.0 - (item.HasPersistentJitter ? 25.0 : 0.0) - (item.FailedToStabilize ? 40.0 : 0.0), 0, 100);
            }
            health.ReturnSpeedScore = valid == 0 ? 0 : returnSpeed / valid;
            health.BounceScore = valid == 0 ? 0 : bounce / valid;
            health.DeadzoneScore = Clamp(100.0 - Math.Max(0.0, result.Deadzone.RecommendedDeadzonePercent - 3.0) * 7.0, 0, 100);
            double score = health.CenterStabilityScore * 0.15 + health.NoiseScore * 0.10 + health.CircularCoverageScore * 0.15 + health.CircularityScore * 0.10 + health.DirectionConsistencyScore * 0.10 + health.ReturnSpeedScore * 0.15 + health.BounceScore * 0.10 + health.DeadzoneScore * 0.15;
            health.Score = (int)Math.Round(Clamp(score, 0, 100));
            SetHealthStatus(health, health.Score >= 90 ? StickHealthStatus.Excellent : health.Score >= 75 ? StickHealthStatus.Good : health.Score >= 50 ? StickHealthStatus.Warning : StickHealthStatus.Critical);
            return health;
        }

        public static string DirectionLabel(StickReturnDirection direction)
        {
            switch (direction)
            {
                case StickReturnDirection.Up: return "上";
                case StickReturnDirection.Down: return "下";
                case StickReturnDirection.Left: return "左";
                default: return "右";
            }
        }

        private static int CountValidDirections(IList<StickReturnResult> results)
        {
            if (results == null) return 0;
            bool[] directions = new bool[4];
            for (int i = 0; i < results.Count; i++) if (results[i] != null && results[i].IsValid) directions[(int)results[i].Direction] = true;
            int count = 0;
            for (int i = 0; i < directions.Length; i++) if (directions[i]) count++;
            return count;
        }

        private static void SetHealthStatus(StickHealthScoreResult health, StickHealthStatus status)
        {
            health.Status = status;
            switch (status)
            {
                case StickHealthStatus.Excellent: health.EnglishStatus = "Excellent"; health.ChineseStatus = "优秀"; break;
                case StickHealthStatus.Good: health.EnglishStatus = "Good"; health.ChineseStatus = "良好"; break;
                case StickHealthStatus.Warning: health.EnglishStatus = "Warning"; health.ChineseStatus = "需要注意"; break;
                case StickHealthStatus.Critical: health.EnglishStatus = "Critical"; health.ChineseStatus = "严重异常"; break;
                default: health.EnglishStatus = "Not Tested"; health.ChineseStatus = "未检测"; break;
            }
        }

        private static double Projection(StickSample sample, StickReturnDirection direction)
        {
            if (direction == StickReturnDirection.Up) return sample.Y;
            if (direction == StickReturnDirection.Down) return -sample.Y;
            if (direction == StickReturnDirection.Left) return -sample.X;
            return sample.X;
        }

        private static string DirectionLabel(double x, double y, double radius)
        {
            if (radius < 0.005) return "无明显方向";
            double angle = Math.Atan2(y, x) * 180.0 / Math.PI;
            if (angle < 0) angle += 360.0;
            string[] labels = { "右", "右上", "上", "左上", "左", "左下", "下", "右下" };
            int index = ((int)Math.Round(angle / 45.0)) % 8;
            return labels[index];
        }

        private static string MissingDirectionLabel(double[] radii, double[] directions, int[] quadrantCovered)
        {
            List<string> missing = new List<string>();
            if (directions[0] < 0.85) missing.Add("右");
            if (directions[1] < 0.85) missing.Add("左");
            if (directions[2] < 0.85) missing.Add("上");
            if (directions[3] < 0.85) missing.Add("下");
            string[] quadrants = { "右上象限", "左上象限", "左下象限", "右下象限" };
            for (int i = 0; i < quadrantCovered.Length; i++) if (quadrantCovered[i] < JoystickAnalysisConfiguration.CircularityBinCount / 8) missing.Add(quadrants[i]);
            return missing.Count == 0 ? "无" : string.Join("、", missing.ToArray());
        }

        private static double DirectionBandAverage(double[] radii, bool diagonal)
        {
            int bins = radii.Length;
            double sum = 0;
            int count = 0;
            for (int i = 0; i < bins; i++)
            {
                double degrees = i * 360.0 / bins;
                double nearestAxis = Math.Min(Math.Min(AngleDistance(degrees, 0), AngleDistance(degrees, 90)), Math.Min(AngleDistance(degrees, 180), AngleDistance(degrees, 270)));
                double nearestDiagonal = Math.Min(Math.Min(AngleDistance(degrees, 45), AngleDistance(degrees, 135)), Math.Min(AngleDistance(degrees, 225), AngleDistance(degrees, 315)));
                bool include = diagonal ? nearestDiagonal <= 10.0 : nearestAxis <= 10.0;
                if (include && radii[i] > 0) { sum += radii[i]; count++; }
            }
            return count == 0 ? 0 : sum / count;
        }

        private static double AngleDistance(double a, double b)
        {
            double distance = Math.Abs(a - b) % 360.0;
            return Math.Min(distance, 360.0 - distance);
        }

        private static double SamplingFrequency(IList<StickSample> samples)
        {
            if (samples == null || samples.Count < 2) return 0;
            double seconds = (SafeSample(samples[samples.Count - 1]).Timestamp - SafeSample(samples[0]).Timestamp).TotalSeconds;
            return seconds <= 0 ? 0 : (samples.Count - 1) / seconds;
        }

        private static StickSample SafeSample(StickSample sample)
        {
            return sample ?? new StickSample(DateTime.UtcNow, 0, 0);
        }

        private static double Radius(double x, double y) { return Math.Sqrt(x * x + y * y); }
        private static double Square(double value) { return value * value; }
        private static double Clamp(double value, double minimum, double maximum) { return Math.Max(minimum, Math.Min(maximum, value)); }
        private static double CeilingHalf(double value) { return Math.Ceiling(value * 2.0) / 2.0; }

        private static double Mean(double[] values)
        {
            if (values == null || values.Length == 0) return 0;
            double sum = 0;
            for (int i = 0; i < values.Length; i++) sum += values[i];
            return sum / values.Length;
        }

        private static double Minimum(double[] values)
        {
            if (values == null || values.Length == 0) return 0;
            double value = double.MaxValue;
            for (int i = 0; i < values.Length; i++) value = Math.Min(value, values[i]);
            return value;
        }

        private static double Maximum(double[] values)
        {
            if (values == null || values.Length == 0) return 0;
            double value = double.MinValue;
            for (int i = 0; i < values.Length; i++) value = Math.Max(value, values[i]);
            return value;
        }

        private static double Median(List<double> values)
        {
            if (values == null || values.Count == 0) return 0;
            double[] copy = values.ToArray();
            Array.Sort(copy);
            int middle = copy.Length / 2;
            return copy.Length % 2 == 0 ? (copy[middle - 1] + copy[middle]) / 2.0 : copy[middle];
        }
    }

    public static class JoystickAnalyzerSelfTest
    {
        public static string Run()
        {
            DateTime start = DateTime.UtcNow;
            List<StickSample> centered = ConstantSamples(start, 120, 0, 0);
            JoystickStationaryResult centeredResult = JoystickAnalyzer.AnalyzeStationary(StickSide.Left, centered);
            Require(centeredResult.IsValid && centeredResult.CenterOffsetPercent < 0.01, "centered data must have low drift");

            JoystickStationaryResult rightDrift = JoystickAnalyzer.AnalyzeStationary(StickSide.Left, ConstantSamples(start, 120, 0.04, 0));
            Require(rightDrift.IsValid && rightDrift.DriftDirection == "右", "right drift direction was not detected");

            List<StickSample> noise = new List<StickSample>();
            for (int i = 0; i < 160; i++) noise.Add(new StickSample(start.AddMilliseconds(i * 8), Math.Sin(i * 1.7) * 0.012, Math.Cos(i * 2.1) * 0.009));
            JoystickStationaryResult noiseResult = JoystickAnalyzer.AnalyzeStationary(StickSide.Left, noise);
            Require(noiseResult.StandardDeviationXPercent > 0.5 && noiseResult.StandardDeviationYPercent > 0.4, "noise standard deviation was not calculated");

            StickCircularityResult circle = JoystickAnalyzer.AnalyzeCircularity(StickSide.Left, CircleSamples(start, 360, 0.96, -1));
            Require(circle.IsValid && circle.OuterCoveragePercent >= 98.0 && circle.CircularityErrorPercent < 3.0, "full circle coverage failed");

            StickCircularityResult missingQuadrant = JoystickAnalyzer.AnalyzeCircularity(StickSide.Left, CircleSamples(start, 360, 0.96, 1));
            Require(missingQuadrant.HasMissingTravel && missingQuadrant.QuadrantCoveragePercent < 25.0 && missingQuadrant.MissingDirections != "无", "missing quadrant was not detected");

            StickCircularityResult square = JoystickAnalyzer.AnalyzeCircularity(StickSide.Left, SquareSamples(start, 360, 0.96));
            Require(square.IsSquareLimited && square.CircularityErrorPercent > circle.CircularityErrorPercent + 5.0, "square input was rated as a perfect circle");

            StickReturnResult overshoot = JoystickAnalyzer.AnalyzeReturn(StickSide.Left, StickReturnDirection.Right, ReturnSamples(start, true, false));
            Require(overshoot.IsValid && overshoot.HasOvershoot && overshoot.OvershootPercent >= 4.0, "return overshoot was not detected");

            StickReturnResult bouncing = JoystickAnalyzer.AnalyzeReturn(StickSide.Left, StickReturnDirection.Right, ReturnSamples(start, false, true));
            Require(bouncing.IsValid && bouncing.BounceCount >= 2, "multiple bounces were not counted");

            DeadzoneRecommendation deadzone = JoystickAnalyzer.RecommendDeadzone(StickSide.Left, rightDrift);
            Require(deadzone.MinimumDeadzonePercent >= rightDrift.MaximumOffsetPercent, "recommended minimum deadzone is below actual drift");

            JoystickStationaryResult emptyStationary = JoystickAnalyzer.AnalyzeStationary(StickSide.Left, new List<StickSample>());
            StickCircularityResult emptyCircle = JoystickAnalyzer.AnalyzeCircularity(StickSide.Left, null);
            StickReturnResult emptyReturn = JoystickAnalyzer.AnalyzeReturn(StickSide.Left, StickReturnDirection.Up, null);
            Require(!emptyStationary.IsValid && !emptyCircle.IsValid && !emptyReturn.IsValid, "empty samples must not produce normal results");

            return "Joystick analyzer self-test passed: centered, right drift, noise deviation, full circle, missing quadrant, square limit, overshoot, bounce count, deadzone floor, empty samples.";
        }

        private static List<StickSample> ConstantSamples(DateTime start, int count, double x, double y)
        {
            List<StickSample> samples = new List<StickSample>();
            for (int i = 0; i < count; i++) samples.Add(new StickSample(start.AddMilliseconds(i * 8), x, y));
            return samples;
        }

        private static List<StickSample> CircleSamples(DateTime start, int count, double radius, int missingQuadrant)
        {
            List<StickSample> samples = new List<StickSample>();
            for (int i = 0; i < count; i++)
            {
                double angle = i * Math.PI * 2.0 / count;
                int quadrant = Math.Min(3, (int)(angle / (Math.PI / 2.0)));
                if (quadrant == missingQuadrant) continue;
                samples.Add(new StickSample(start.AddMilliseconds(i * 8), Math.Cos(angle) * radius, Math.Sin(angle) * radius));
            }
            return samples;
        }

        private static List<StickSample> SquareSamples(DateTime start, int count, double halfExtent)
        {
            List<StickSample> samples = new List<StickSample>();
            for (int i = 0; i < count; i++)
            {
                double angle = i * Math.PI * 2.0 / count;
                double cosine = Math.Cos(angle);
                double sine = Math.Sin(angle);
                double scale = halfExtent / Math.Max(Math.Abs(cosine), Math.Abs(sine));
                samples.Add(new StickSample(start.AddMilliseconds(i * 8), cosine * scale, sine * scale));
            }
            return samples;
        }

        private static List<StickSample> ReturnSamples(DateTime start, bool overshoot, bool bounces)
        {
            List<StickSample> samples = new List<StickSample>();
            for (int i = 0; i < 15; i++) samples.Add(new StickSample(start.AddMilliseconds(i * 8), 0.92, 0));
            double[] values = bounces
                ? new double[] { 0.80, 0.58, 0.32, 0.10, 0.02, -0.10, -0.02, 0.09, 0.02, -0.085, -0.02, 0.08, 0.02, 0.01, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }
                : new double[] { 0.80, 0.60, 0.38, 0.18, 0.04, overshoot ? -0.08 : -0.01, -0.025, 0.01, 0.005, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            for (int i = 0; i < values.Length; i++) samples.Add(new StickSample(start.AddMilliseconds((i + 15) * 8), values[i], 0));
            return samples;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
