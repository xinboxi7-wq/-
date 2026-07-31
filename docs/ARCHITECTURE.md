# ControllerLab 架构

## 解决方案结构

```text
ControllerLab.sln
├─ ControllerLab.csproj                 # .NET Framework 4.8 x64 WinExe
├─ ControllerLab.cs                     # Program、App、MainWindow、页面组合、Raw Input、Sony HID、视觉组件
├─ ControllerCore.cs                    # 统一状态、设备管理、按键/摇杆测试与报告模型
├─ ControllerRumble.cs                  # 统一震动接口、XInput / DualSense 输出、预设与安全控制
├─ DualSenseMotion.cs                   # MotionSample、静止校准、姿态融合
├─ DualSenseMotionVisual.cs             # DualSense 体感姿态可视化
├─ XboxOverlay.cs                       # Xbox 区域配置、渲染和校准窗口
├─ ControllerLabTheme.cs                # 代码式 WPF 主题与通用样式
├─ Assets/                              # 图片、Alpha Mask、区域及视觉样式配置
├─ Tools/                               # 开发期离线轮廓 / 区域生成工具
└─ docs/                                # 项目维护文档与 README 截图
```

## WPF 界面

当前工程是**代码式 WPF**：`ControllerLab.cs` 的 `MainWindow` 负责页面宿主、导航和 UI 控件组合；没有独立的 `.xaml` View 或 `*ViewModel.cs` 层。

| 页面 | 入口 | 主要实现 |
| --- | --- | --- |
| 设备首页 | 页面 0 | `MainWindow`、`DeviceCard`、`ControllerDeviceManager` |
| 实时可视化 | 页面 1 | `ControllerVisualizerView`、`ControllerVisual`、`DualSenseVisual` |
| 按键检测 | 页面 2 | `InputTestSession`、`ControllerTestReport` |
| 摇杆检测 | 页面 3 | `StickDriftTestEngine`、`StickDriftAnalyzer`、范围跟踪器、`StickTestEvidenceStore` |
| 体感 | 页面 4 | `DualSenseMotionManager`、`MotionFusionService`、`DualSenseMotionPoseView` |
| 震动测试 | 页面 5 | `ControllerRumbleController`、`IControllerRumbleService` |

UI 页面不应直接读取 XInput、Raw Input 或 HID 字节。所有运行时输入先转换为 `ControllerState`，再在 WPF 渲染循环中显示。

## 输入数据流

```text
Xbox / XInput 兼容设备
  → InputManager（XInputGetState / Battery / Capabilities）
  → InputSnapshot
  → ControllerStateAdapter
  → ControllerState
  → ControllerDeviceManager（在线集合与选中设备）
  → MainWindow / 各页面 / 可视化组件

DualSense
  → Windows Raw Input（MainWindow.SourceInitialized 注册；WM_INPUT）
  → SonyInputManager.ObserveRawInput
  → USB / Bluetooth 报告布局和 CRC 校验
  → InputSnapshot
  → ControllerStateAdapter
  → ControllerState（DualSense 扩展状态）
  → ControllerDeviceManager / WPF 可视化
```

### 统一状态与设备生命周期

- `ControllerCore.cs:ControllerState` 是 UI 使用的统一公共状态，包含设备标识、类型、连接方式、电量、按键、D-pad、摇杆、扳机、时间戳、输入来源和能力。
- `ControllerStateAdapter` 将现有 `InputSnapshot` 适配为公共状态；`DualSenseControllerExtensions` 和 `XboxControllerExtensions` 保存专属能力。
- `IControllerDevice`、`XboxControllerDevice`、`DualSenseControllerDevice` 定义设备抽象；`ControllerDeviceManager` 维护可绑定在线集合并处理设备切换。
- `MainWindow.StartSampling()` 在输入侧采样；`CompositionTarget.Rendering` 以受控节奏刷新 UI。输入线程不得阻塞 WPF UI 线程。

## DualSense HID 路径

`SonyInputManager` 在 `ControllerLab.cs` 中维护 Sony 原生 HID 设备身份、报告解析和 Raw Input 观察：

- USB 完整输入：报告 `0x01`，完整布局包含触摸和运动字段。
- 蓝牙完整输入：报告 `0x31`，完整布局共享输入体并校验 CRC；CRC 失败时运动数据不可用。
- 蓝牙紧凑兼容输入：报告 `0x01`，仅基础按键 / 摇杆 / 扳机；不宣称触摸坐标或运动数据可用。

触点、运动和电量必须仅在当前实际报告提供相应数据时显示。`DualSenseMotion.cs` 将已验证的输入样本交给 `MotionFusionService`，内部用四元数跟踪姿态，并通过加速度计修正 Pitch / Roll 长期漂移。

## Overlay 渲染流

```text
Assets/controller.png + Assets/xboxRegions.json
  → XboxRegionManager（1536 × 1024 逻辑舞台）
  → 用户 override 合并（如有效）
  → ControllerVisual / Xbox 校准窗口

Assets/dualsense.png + Assets/dualSenseRegions.json + Assets/dualSenseVisualStyles.json
  → DualSenseRegionManager（1536 × 1024 逻辑舞台）
  → 用户 override 合并（如有效）
  → DualSenseVisual / Touch Visualizer
```

- Xbox 默认配置：`Assets/xboxRegions.json`；用户 override：`%LocalAppData%\ControllerLab\xbox-regions.override.json`。
- DualSense 默认配置：`Assets/dualSenseRegions.json`；视觉样式：`Assets/dualSenseVisualStyles.json`；用户 override：`%LocalAppData%\XboxControllerLab\dualSense-regions.override.json`。
- Xbox 顶部左右共享真实 Alpha Mask：`Assets/LeftTopTriggerMask.png`、`Assets/RightTopTriggerMask.png`。
- 所有已校准 Overlay 与底图都以 1536 × 1024 逻辑舞台整体缩放；不能通过单区域 Margin、Canvas 坐标或额外 Transform 进行临时修补。

## 震动输出流

```text
震动测试 UI
  → ControllerRumbleController（单任务、CancellationToken、安全停止）
  → ControllerRumbleServiceFactory
  ├─ XInputRumbleService → InputManager.TrySetVibration → XInputSetState
  └─ DualSenseRumbleService → DualSenseOutputReportBuilder
       → SonyInputManager.TryWriteOutputReport → HID 输出报告
       → USB 0x02 或 Bluetooth 0x31 + CRC
```

- Xbox 左通道映射低频大马达，右通道映射高频小马达。
- DualSense USB 与蓝牙输出报告分别构造和验证，不允许交叉发送。
- 页面离开、设备切换 / 断开、异常和应用退出均应调用停止输出；同一设备同一时间只允许一个震动任务。

## 配置、资源、日志与测试

| 类别 | 位置 | 说明 |
| --- | --- | --- |
| 主资源 | `Assets/` | 手柄底图、摇杆帽、Alpha Mask、区域 JSON 与样式 JSON |
| 离线工具 | `Tools/GenerateDualSenseRegions/`、`Tools/GenerateXboxTopRegions/`、`Tools/GenerateXboxDPadRegions/` | 开发期生成 / 审核工具，不是运行时依赖 |
| 崩溃日志 | `%LocalAppData%\ControllerLab\logs\crash.log` | `App.RecordUnhandledException` 写入 |
| 摇杆实测记录 | `%LocalAppData%\ControllerLab\stick-test-records\` | 用户在摇杆页保存的 JSON 结构记录和 UTF-8 中文 TXT 报告；仅接受完成的真实 XInput / DualSense HID 检测结果，同一 `EvidenceId` 的范围补充会更新同一对文件 |
| 构建 | `build.ps1` | 调用本机 .NET Framework 4.8 x64 WPF 编译器 |
| 可执行自检 | `ControllerLab.cs` 的命令行开关 | 启动、运行时、导航、核心、漂移、设备、触摸、运动、Overlay、扳机曲线和震动 |

详细命令与回归清单见 [TESTING.md](TESTING.md)。
