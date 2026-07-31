# ControllerLab 当前状态

> 2026-07-31：专业震动系统软件实现完成。现有 `IControllerRumbleService` 保留，新增统一能力模型、14 个数据化预设、独立 25 Hz 时间线播放器、原生 WPF 曲线编辑、设备哈希配置和感知校准。新增震动构造自检覆盖 12 类安全路径；本环境未连接真实 Xbox 或 DualSense，左右震感及 DualSense USB/蓝牙 HID 写入仍保持“已实现，待实机验证”。

> 2026-07-31：摇杆专业检测完整软件模块已接入第 3 页，包含独立 Analyzer/ViewModel/Page、5 秒静止采样、72 区间圆周、左右四方向回中、三档死区与完整健康评分。新增算法自检及原有回归均通过；真实 Xbox/DualSense 机械表现仍待实机验证。详见 `JOYSTICK_PRO_TEST.md`。

> 新任务开始时先读本文件，再读对应专题文档。状态以当前源码和可复现自检为准，不将动态演示或构造数据记为实机验证。

## 当前稳定基线

- **版本：** `v1.2.0-test`（当前代码的检测报告标识；正式语义版本尚未集中管理，需要人工确认）。
- **分支：** `agent/unify-ds5-visualizer-and-drift-test`。
- **最近提交：** 以 `git log -1 --oneline` 为准；提交哈希不在本文重复维护，避免文档与 Git 历史漂移。
- **构建：** 2026-07-31 已用 `build.ps1 -OutputName ControllerLab_RumblePro_Test.exe` 成功构建；启动、运行时、导航、核心、两类摇杆、完整检测、设备、触摸、运动、双 Overlay、扳机曲线和专业震动共 14 项可执行自检通过。
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
| 摇杆专业检测第一阶段：漂移、范围、稳定性、建议死区与实测记录 | 已实现但待实机验证 | `StickDriftAnalyzer`、`StickDriftTestEngine`、`StickTestEvidenceStore`；支持触碰无效判定、连续三轮、范围检查、JSON 结构记录和中文 TXT 报告。构造自检覆盖算法和临时目录写入，真实 Xbox / DualSense 记录仍待补。 |
| LT / RT 历史曲线 | 已实现但待实机验证 | `TriggerTelemetryBuffer`；有自检。 |
| DualSense 触摸板按压 | 已实现但待实机验证 | 由完整 HID 报告的按钮位驱动。 |
| DualSense 最多两点触摸坐标 | 已实现但待实机验证 | USB 完整 `0x01` 与蓝牙完整 `0x31` 报告支持；紧凑兼容报告明确不可用。 |
| DualSense 陀螺仪、加速度计与姿态显示 | 已实现但待实机验证 | `DualSenseMotionManager` / `MotionFusionService`；USB、蓝牙字段和 CRC 有构造自检。 |
| Xbox 双电机震动 | 已实现但待实机验证 | `XInputRumbleService` 调用 `XInputSetState`；尚未记录本基线的实机结果。 |
| DualSense 基础双通道震动 | 已实现但待实机验证 | USB `0x02` / 蓝牙 `0x31` 输出及 CRC 已实现；尚未实机确认。 |
| 专业震动时间线、预设与校准 | 已实现但待实机验证 | `RumblePatternPlayer` 以 25 Hz 播放线性时间线；自定义预设、哈希设备配置、损坏恢复和停止路径有构造自检。 |

## 当前正在开发

- 摇杆、完整健康检测与专业震动的软件实现均已完成；下一项工作是分别用真实 Xbox / XInput 与 DualSense USB / 蓝牙完成可追溯实机记录。在记录补齐前，相关硬件能力保持“已实现但待实机验证”。

## 已知问题与限制

- 正式产品版本号未在单一位置集中定义：检测报告使用 `v1.2.0-test`，历史发布说明仍包含较旧版本号。
- 当前 UI 是大型代码式 WPF 组合，尚无独立 ViewModel 层；修改时必须谨慎处理 Dispatcher、事件订阅和页面离开清理。
- Xbox / 第三方 XInput 手柄的电量、连接方式、震动能力取决于驱动和设备实现。
- DualSense 完整 USB / 蓝牙 HID 报告才提供触点和运动数据；蓝牙紧凑兼容报告只提供基础输入。
- DualSense USB / 蓝牙震动输出仅通过逻辑自检，必须在真实手柄上分别验证。
- 震动与漂移、陀螺仪静止校准及完整检测静止采样均已在运行时互斥；仍需真实设备验证物理震动停止时序。
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
| 2026-07-31 | 完成摇杆专业检测第一阶段的软件闭环：真实输入门控、连续静止检测、触碰无效判定、范围检查、中文报告与本地 JSON/TXT 实测记录。 | `ControllerLab_StickPhase1.exe` 构建成功；12 项自动自检通过，包含临时目录中 JSON/TXT 记录写入。未接入真实 Xbox / DualSense，不能宣称实机验证完成。 |
| 2026-07-31 | 完成专业震动系统：能力模型、数据化预设、25 Hz 播放器、时间线编辑、设备校准与配置。 | 构建与震动专业自检通过；真实 Xbox 左右通道、DualSense USB/蓝牙仍待实机。 |
| 2026-07-31 | 建立项目上下文、架构、设备、UI、测试、决策和路线图文档基线。 | 构建成功；启动、运行时、导航、核心、设备、漂移、触摸、运动、双 Overlay、扳机曲线和震动自检均通过。 |
