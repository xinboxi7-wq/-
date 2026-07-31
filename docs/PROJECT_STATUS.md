# ControllerLab 当前状态

> 新任务开始时先读本文件，再读对应专题文档。状态以当前源码和可复现自检为准，不将动态演示或构造数据记为实机验证。

## 当前稳定基线

- **版本：** `v1.2.0-test`（当前代码的检测报告标识；正式语义版本尚未集中管理，需要人工确认）。
- **分支：** `agent/unify-ds5-visualizer-and-drift-test`。
- **最近提交：** 以 `git log -1 --oneline` 为准；提交哈希不在本文重复维护，避免文档与 Git 历史漂移。
- **构建：** 2026-07-31 已用 `build.ps1 -OutputName ControllerLab_DocsBaseline.exe` 成功构建；12 项可执行自检全部通过。
- **UI 实现：** 原生、代码式 WPF；没有独立 XAML View / ViewModel 文件。

## 已完成功能

| 能力 | 当前状态 | 依据与边界 |
| --- | --- | --- |
| Xbox / XInput 设备发现与统一状态 | 已实现但待实机验证 | `InputManager`、`ControllerStateAdapter`、`ControllerDeviceManager`；有核心自检。 |
| DualSense 原生 HID 发现与统一状态 | 已实现但待实机验证 | `SonyInputManager` 经 Raw Input 接收报告；有解析自检。 |
| 多设备首页与切换 | 已实现但待实机验证 | `ControllerDeviceManager` 维护在线设备；切页不应重复启动输入线程。 |
| Xbox 实时按键、十字键、摇杆、肩键与扳机可视化 | 已实现但待实机验证 | `ControllerVisual`、`XboxRegionManager` 和 `Assets/xboxRegions.json`；有 Overlay 自检。 |
| DualSense 实时可视化 | 已实现但待实机验证 | `DualSenseVisual`、`DualSenseRegionManager`；有 Overlay 自检。 |
| 按键测试 | 已实现但待实机验证 | 只应接受真实 `ControllerState.HasRealInput`。 |
| 摇杆漂移、范围与建议死区 | 已实现但待实机验证 | `StickDriftAnalyzer`、`StickDriftTestEngine`；有构造数据自检。 |
| LT / RT 历史曲线 | 已实现但待实机验证 | `TriggerTelemetryBuffer`；有自检。 |
| DualSense 触摸板按压 | 已实现但待实机验证 | 由完整 HID 报告的按钮位驱动。 |
| DualSense 最多两点触摸坐标 | 已实现但待实机验证 | USB 完整 `0x01` 与蓝牙完整 `0x31` 报告支持；紧凑兼容报告明确不可用。 |
| DualSense 陀螺仪、加速度计与姿态显示 | 已实现但待实机验证 | `DualSenseMotionManager` / `MotionFusionService`；USB、蓝牙字段和 CRC 有构造自检。 |
| Xbox 双电机震动 | 已实现但待实机验证 | `XInputRumbleService` 调用 `XInputSetState`；尚未记录本基线的实机结果。 |
| DualSense 基础双通道震动 | 已实现但待实机验证 | USB `0x02` / 蓝牙 `0x31` 输出及 CRC 已实现；尚未实机确认。 |

## 当前正在开发

- 当前任务仅建立项目上下文与文档基线；没有进行功能开发。

## 已知问题与限制

- 正式产品版本号未在单一位置集中定义：检测报告使用 `v1.2.0-test`，历史发布说明仍包含较旧版本号。
- 当前 UI 是大型代码式 WPF 组合，尚无独立 ViewModel 层；修改时必须谨慎处理 Dispatcher、事件订阅和页面离开清理。
- Xbox / 第三方 XInput 手柄的电量、连接方式、震动能力取决于驱动和设备实现。
- DualSense 完整 USB / 蓝牙 HID 报告才提供触点和运动数据；蓝牙紧凑兼容报告只提供基础输入。
- DualSense USB / 蓝牙震动输出仅通过逻辑自检，必须在真实手柄上分别验证。
- 震动与漂移检测已互斥；陀螺仪静止校准与震动的互斥规则列为硬性保护要求，需在涉及任一模块的下次改动中确认运行时也已强制执行。
- Overlay 默认资源和用户校准 override 必须保持兼容；不可通过普通布局偏移修补已校准区域。

## 下一步（近期）

1. 对 Xbox 与 DualSense 分别完成 USB / 蓝牙（适用时）的实机输入、断开重连与震动安全停止验证。
2. 将版本号、构建产物命名与 Release 说明收敛为单一来源。
3. 以真实手柄数据复核摇杆漂移、范围与扳机曲线的报告阈值。
4. 为触摸、运动和震动的连接模式能力建立持续回归记录。
5. 在不破坏已校准 Overlay 的前提下完成 UI / 产品化收尾。

## 最近更新

| 日期 | 改动 | 验证 |
| --- | --- | --- |
| 2026-07-31 | 建立项目上下文、架构、设备、UI、测试、决策和路线图文档基线。 | 构建成功；启动、运行时、导航、核心、设备、漂移、触摸、运动、双 Overlay、扳机曲线和震动自检均通过。 |
