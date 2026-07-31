# ControllerLab Agent Guide

## 项目身份

- **项目名称：** ControllerLab
- **类型：** Windows 原生 WPF 桌面应用
- **主要语言：** C#；当前 UI 为代码式 WPF（工程目前没有独立 XAML 视图）
- **目标：** Xbox 与 DualSense 手柄实时可视化、输入测试和硬件健康诊断

## 技术边界

- 保持原生 WPF；不引入 WebView、HTML/CSS、Unity 或浏览器渲染方案。
- 保留既有 HID、RawInput、XInput、ViGEm 及设备识别架构。
- 不在没有明确收益和回归验证时进行全项目重构。

## 关键保护规则

- 不得随意修改已校准的 Xbox / DualSense Overlay 坐标。
- 不得重新绘制已确认正确的 D-pad 与按键区域，或替换已验证的真实 PNG 遮罩资源。
- 不得破坏既有输入链路；USB 与蓝牙 HID 输出报告不得混用。
- 未经实机验证的硬件能力必须标记为待验证；不得伪造设备支持、输入或测试成功状态。
- 漂移静止检测期间禁止震动；陀螺仪静止校准期间也禁止震动。
- 设备断开、页面退出和应用退出时必须停止震动。

## 工作流程

每次开始任务前：

1. 阅读本文件和 [docs/PROJECT_STATUS.md](docs/PROJECT_STATUS.md)。
2. 根据任务阅读相关专题文档。
3. 运行 `git status -sb`。
4. 先理解已有实现，再修改代码。

每次完成任务后：

1. 构建整个解决方案或等价的当前构建脚本。
2. 运行与改动相关的自检。
3. 回归 Xbox 与 DualSense 原有功能。
4. 更新 `PROJECT_STATUS.md` 和相关专题文档。
5. 列出改动文件并说明实机验证状态。
6. 创建清晰 Git 提交，并保持工作树干净。

## 常用命令

以下命令已由当前工程文件确认：

```powershell
# 当前项目没有 PackageReference 或 packages.config；Restore 目前不适用。
# 如以后增加 NuGet 依赖，再为 ControllerLab.sln 配置并执行 restore。

# 使用项目内置的 .NET Framework 4.8 x64 WPF 编译脚本
.\build.ps1 -OutputName ControllerLab.exe

# 常用逻辑 / 运行时自检（先完成上一步构建）
.\ControllerLab.exe --startup-selftest
.\ControllerLab.exe --runtime-selftest
.\ControllerLab.exe --controller-core-selftest
.\ControllerLab.exe --stick-drift-selftest
.\ControllerLab.exe --device-manager-selftest
.\ControllerLab.exe --ds5-touch-parser-selftest
.\ControllerLab.exe --ds5-motion-selftest
.\ControllerLab.exe --ds5-overlay-selftest
.\ControllerLab.exe --xbox-overlay-selftest
.\ControllerLab.exe --trigger-chart-selftest
.\ControllerLab.exe --rumble-selftest
```

当前没有 Publish Profile 或 `dotnet publish` 流程。用于本地测试的可执行文件由 `build.ps1` 生成；发布包应在单独、可复现的发布流程中创建。

## 文档索引

- [当前状态](docs/PROJECT_STATUS.md)
- [架构](docs/ARCHITECTURE.md)
- [设备支持](docs/DEVICE_SUPPORT.md)
- [UI 规则](docs/UI_RULES.md)
- [设计决策](docs/DECISIONS.md)
- [测试](docs/TESTING.md)
- [路线图](docs/ROADMAP.md)
