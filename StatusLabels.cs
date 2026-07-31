using System;

namespace ControllerLab
{
    public static class ControllerLabStatusLabels
    {
        public static string Overall(string value)
        {
            if (string.Equals(value, HealthReportOverallStatus.Excellent.ToString(), StringComparison.OrdinalIgnoreCase)) return "Excellent / 优秀";
            if (string.Equals(value, HealthReportOverallStatus.Good.ToString(), StringComparison.OrdinalIgnoreCase)) return "Good / 良好";
            if (string.Equals(value, HealthReportOverallStatus.Attention.ToString(), StringComparison.OrdinalIgnoreCase)) return "Attention / 需要注意";
            if (string.Equals(value, HealthReportOverallStatus.Poor.ToString(), StringComparison.OrdinalIgnoreCase)) return "Poor / 状态较差";
            return "Incomplete / 检测未完成";
        }

        public static string Step(string value)
        {
            if (string.Equals(value, HealthCheckStepStatus.Passed.ToString(), StringComparison.OrdinalIgnoreCase)) return "Passed / 已通过";
            if (string.Equals(value, HealthCheckStepStatus.Attention.ToString(), StringComparison.OrdinalIgnoreCase)) return "Attention / 需要注意";
            if (string.Equals(value, HealthCheckStepStatus.Abnormal.ToString(), StringComparison.OrdinalIgnoreCase)) return "Critical / 严重异常";
            if (string.Equals(value, HealthCheckStepStatus.Skipped.ToString(), StringComparison.OrdinalIgnoreCase)) return "Skipped / 已跳过";
            if (string.Equals(value, HealthCheckStepStatus.Unsupported.ToString(), StringComparison.OrdinalIgnoreCase)) return "Unsupported / 不支持";
            if (string.Equals(value, HealthCheckStepStatus.Testing.ToString(), StringComparison.OrdinalIgnoreCase)) return "Testing / 检测中";
            return "Waiting / 等待";
        }

        public static string Category(HealthCategoryScore category)
        {
            if (category == null) return "Not Tested / 未检测";
            if (!category.Supported) return "Unsupported / 不支持";
            if (!category.Tested) return "Not Tested / 未检测";
            if (category.Status == "优秀") return "Excellent / 优秀";
            if (category.Status == "良好") return "Good / 良好";
            if (category.Status == "需要注意") return "Attention / 需要注意";
            return "Critical / 严重异常";
        }

        public static string ScoreContext(ControllerHealthReport report)
        {
            if (report == null || report.IsComplete) return string.Empty;
            return "评分基于已完成项目；跳过和不支持项目不参与评分。";
        }
    }
}
