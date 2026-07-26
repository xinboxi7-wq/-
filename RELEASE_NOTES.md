# ControllerLab v1.0.2

## 最新运行截图更新

- README 的 Xbox 实时可视化图替换为当前运行版本截图，包含最新的 LT/RT 历史曲线和诊断面板。
- 保留 DualSense / DS5 实时可视化截图。

## 源码发布内容

ControllerLab v1.0.1 提供 Windows WPF 手柄检测与可视化的完整 Visual Studio 工程源码。

### 包含内容

- Xbox XInput 与 DualSense 原生 HID 设备统一管理。
- 多设备首页、实时可视化、按键测试、摇杆漂移/范围检测和扳机历史曲线。
- Xbox 与 DualSense 专属视觉层，以及区域配置与校准 override 支持。
- 所有 C#、项目文件、资源、离线区域工具和文档截图。

### 不包含内容

- `bin/`、`obj/`、`.vs/`、历史测试 EXE、临时审计截图和旧发布包。
- 个人本地校准 override、日志、设备数据或其他本机状态。

### 已知限制

- 电量、连接方式、触摸坐标和运动传感器能力受设备、驱动与 USB/蓝牙报告类型影响。
- DualSense 的高级能力仅在真实 HID 报告包含对应字段时启用；不会通过模拟数据伪造检测结论。
- 本 Release 是源码发布。请按 README 中的编译说明生成本机可执行文件。
