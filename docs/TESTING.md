# 测试与回归指南

## 构建环境

- **目标框架：** .NET Framework 4.8。
- **架构：** x64。
- **UI：** 原生 WPF。
- **编译入口：** `build.ps1`，调用 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` 及 WPF 程序集。
- **运行环境：** Windows x64，需具备 .NET Framework 4.8。工程未在源码中声明更精确的最低 Windows 版本。

## Restore、Build 与本地测试

当前 `ControllerLab.csproj` 没有 `PackageReference`，仓库也没有 `packages.config`；因此当前没有需要执行的包还原步骤。不要为了形式运行不适用的 `dotnet restore`。若未来引入 NuGet 依赖，必须同时记录可执行的 restore 命令。

```powershell
# 在项目根目录构建一个隔离测试文件，避免覆盖正在运行的版本
.\build.ps1 -OutputName ControllerLab_Test.exe

# 基础构造、运行时和导航
.\ControllerLab_Test.exe --startup-selftest
.\ControllerLab_Test.exe --runtime-selftest
.\ControllerLab_Test.exe --controller-navigation-selftest

# 公共状态、设备和检测
.\ControllerLab_Test.exe --controller-core-selftest
.\ControllerLab_Test.exe --device-manager-selftest
.\ControllerLab_Test.exe --stick-drift-selftest
.\ControllerLab_Test.exe --trigger-chart-selftest

# DualSense 与可视化
.\ControllerLab_Test.exe --ds5-touch-parser-selftest
.\ControllerLab_Test.exe --ds5-motion-selftest
.\ControllerLab_Test.exe --ds5-overlay-selftest
.\ControllerLab_Test.exe --xbox-overlay-selftest

# 震动输出构造 / 安全逻辑
.\ControllerLab_Test.exe --rumble-selftest
```

当前没有 Publish Profile 或 `dotnet publish` 命令。用于本地测试的 EXE 由 `build.ps1` 创建；正式 Release 打包需另行记录步骤，且不得提交 `bin/`、`obj/` 或本地测试 EXE。

## 自动自检范围

| 命令 | 覆盖范围 | 不覆盖范围 |
| --- | --- | --- |
| `--startup-selftest` | WPF 窗口构造 | 真实设备输入 |
| `--runtime-selftest` | 窗口短时显示 / 关闭 | 长时间资源、真实设备 |
| `--controller-navigation-selftest` | 手柄导航逻辑 | 真实按键硬件 |
| `--controller-core-selftest` | 状态适配与报告模型 | 真实 HID / XInput |
| `--stick-drift-selftest` | 构造漂移、范围、阈值 | 真实摇杆噪声 |
| `--device-manager-selftest` | 多设备注册 / 移除逻辑 | Windows 热插拔 |
| `--ds5-touch-parser-selftest` | USB / BT 报告布局构造数据 | 真实 DualSense 报告 |
| `--ds5-motion-selftest` | 运动解析、CRC、融合边界 | 真实传感器精度 |
| `--ds5-overlay-selftest` / `--xbox-overlay-selftest` | 逻辑舞台与区域边界 | 实机照片的肉眼对齐 |
| `--trigger-chart-selftest` | 缓冲区与曲线逻辑 | 真实扳机噪声 |
| `--rumble-selftest` | 输出映射、模式取消、停止保护 | 真实震感 / HID 写入兼容性 |

## Xbox 实机回归

1. 使用有线、蓝牙或接收器（适用时）连接 Xbox / XInput 手柄，确认设备首页名称、设备 ID、输入来源和连接方式。
2. 在实时页逐项按 A/B/X/Y、D-pad 四方向与斜向、View、Menu、Guide、LB/RB、L3/R3；观察局部高亮无明显偏移。
3. 推动左右摇杆的中心、四向与四个斜向极限；确认摇杆帽位于前景、光环固定、回中重合。
4. LT / RT 从 0% 缓慢到 100%，确认历史曲线连续且 LT 左→右、RT 右→左的视觉反馈正确。
5. 完成按键测试和摇杆静止 / 范围测试；测试中故意触碰摇杆，确认结果被标记为无效而非严重漂移。
6. 震动测试先使用默认 40% / 5 秒：验证左低频、右高频、均衡、渐强、脉冲和交替；设备断开、切换页面和退出应用时必须停止。

## DualSense USB 实机回归

1. 以 USB 连接；记录设备名称、报告 ID、报告长度和连接方式。
2. 检查按键、摇杆、L2/R2、触摸板按压、麦克风按钮和电量（若报告可用）。
3. 在触摸板测试单指四角、滑动、双指、按压组合；确认只有报告提供真实坐标时才显示触点。
4. 在体感页静止校准、重新居中、轻微旋转；确认异常 CRC / 数据中断不会产生虚假姿态。
5. 使用默认安全震动设置，确认 USB 输出只使用 USB 报告，并验证页面离开 / 断开 / 退出停止。

## DualSense 蓝牙实机回归

1. 以蓝牙连接，记录完整 `0x31` 或紧凑兼容 `0x01` 报告布局。
2. 基础输入必须工作；紧凑报告应明确显示触摸坐标和运动不可用，不得伪造数据。
3. 对完整报告重复触点、体感和 CRC 异常检查。
4. 震动必须使用蓝牙输出 `0x31` 与 CRC，不得发送 USB 输出格式。

## Overlay 对齐与 DPI 检查

对 Xbox 和 DualSense 分别检查 100%、125%、150% DPI，及至少三种窗口尺寸：

- 底图完整显示且比例不变。
- 关闭 Glow、透明 Fill、1px 描边时，已校准区域与实体边缘贴合。
- 开启正式光效后，Glow 不越过无关外壳。
- 校准只写入相应 LocalAppData override；默认 JSON 保持不变。

## 回归测试清单

| 改动区域 | 必跑自动测试 | 必做人工 / 实机检查 |
| --- | --- | --- |
| 输入、设备管理 | 核心、设备、启动、运行时 | 断开重连、多设备切换、无设备状态 |
| Xbox Overlay | Xbox Overlay、启动、运行时 | 关键按键、D-pad、摇杆、DPI |
| DualSense HID / 触摸 | 触摸解析、核心、运行时 | USB、蓝牙完整 / 紧凑报告差异 |
| Motion | 运动自检、运行时 | 静止校准、断连、CRC / 可用性提示 |
| 漂移 / 范围 | 漂移自检、扳机曲线 | 静止、触碰无效、范围一圈、设备切换 |
| 震动 | 震动自检、运行时 | 两通道、预设、停止、断开、退出 |
| UI / 导航 | 启动、运行时、导航 | 页面切换 10 次、窗口 / DPI、资源趋势 |

每次实机验证应记录设备型号、连接模式、测试日期、输入来源、结果和已知异常；没有记录时，不得更新为 `Supported and verified`。
