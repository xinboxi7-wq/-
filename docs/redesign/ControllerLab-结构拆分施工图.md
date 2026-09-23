# ControllerLab · 结构拆分施工图

> **配套**：[[ControllerLab-总览]]（索引）｜[[ControllerLab-改版实现规格-状态矩阵]] §5 陷阱 B（拆分的**依据**）｜[[ControllerLab-视觉目标规格]] §7（拆分的**目的**）｜[[ControllerLab-组件库规格]]（拆分后的构件）
>
> **日期**：2026-09-22 ｜ **状态**：待执行
>
> **本文档补的缺口**：既有图纸只写下「**先做结构拆分，再做视觉改版**，单文件 ≤ 2,000 行」，**没有说怎么拆**。本文档把那一行变成可执行施工图。

---

## 0. 一句话

`ControllerLab.cs` = **601 KB / 11,036 行 / 45 个顶层类型**，但真正的病根**不是"类太多"，而是 `MainWindow` 一个类就占 5,042 行（46%）**。因此拆分的核心不是把小类挪走（那只是顺手），而是**用 `partial class` 把 MainWindow 切成 9 份**。

---

## 1. 实测基线（2026-09-22 16:07）

| 项 | 值 |
| --- | --- |
| 分支 | `agent/unify-ds5-visualizer-and-drift-test` |
| 工作树 | 干净 |
| 领先 origin | **11 个提交未推送**（开工前必须先推） |
| 基线编译 | ✅ `build.ps1 -OutputName ControllerLab_HermesBaseline.exe -Configuration Release` 通过，4.3 MB |
| 编译器 | `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`（build.ps1 内置调用） |
| MSBuild | `Program Files (x86)\Microsoft Visual Studio\18\BuildTools\...\MSBuild.exe` 可用 |
| 命名空间 | `ControllerLab`（单命名空间） |
| using 数 | 27 条（第 1–27 行） |

**结论：本地具备「改一次 → 编译一次 → 验证一次」的完整闭环能力，拆分不必依赖 Codex。**

---

## 2. 类型普查（45 个顶层类型 + 14 个嵌套类型）

### 2.1 按体量排序的头部

| 类型 | 起始行 | 约计行数 | 占全文 |
| --- | --- | --- | --- |
| **MainWindow** | 541 | **5,042** | **46%** |
| SonyInputManager | 7436 | 789 | 7% |
| DualSenseRegionManager | 9088 | 620 | 6% |
| InputManager | 6833 | 603 | 5% |
| DualSenseVisual | 8225 | 481 | 4% |
| DualSenseCalibrationWindow | 9974 | 234 | 2% |
| DualSenseTouchVisualizer | 8706 | 211 | 2% |
| TriggerChart | 10873 | 164 | 1% |
| DeviceHomeView | 426 | 69 | — |
| 其余 ~36 个 | — | 合计 ~2,700 | 24% |

### 2.2 完整分类映射

| 目标目录 | 类型 |
| --- | --- |
| `Models/` | ControllerReport、ControllerSettings、InputSnapshot、GuidedStage`(enum)`、ControllerFamily`(enum)`、StickPlotTraceMode`(enum)`、DualSenseTouchPoint、DualSenseTouchDebugInfo、DualSenseOverlayState、DualSenseRegionsDocument、DualSenseRegionDefinition、DualSensePathCommand、DualSenseEllipseDefinition、DualSenseMotionRangeDefinition、DualSenseTouchSensorDefinition、DualSenseLogicalPoint、DualSenseVisualStylesDocument、DualSenseVisualStyleDefinition、DualSenseRegionsOverride、DualSenseCalibrationHandle、DualSenseCalibrationSnapshot |
| `Theme/` | Palette |
| `Controls/` | ControllerVisual、StickPlot、DeadzoneSlider、DeadzoneSliderAutomationPeer、TriggerChart、DualSenseVisual、DualSenseTouchVisualizer、DualSenseCalibrationSurface |
| `Services/` | ReportExporter、SettingsStore、DiagnosticEngine、GuidedTestEngine、InputManager、SonyInputManager、DualSenseRegionManager |
| `Views/` | Program、App、ControllerVisualizerView、DeviceCard、DeviceHomeView、DualSenseTouchDebugWindow、DualSenseCalibrationWindow |
| `Views/MainWindow.*.cs` | MainWindow（partial 切 9 份，见 §4） |
| **不得移动** | 14 个嵌套类型（8+ 空格缩进，如 `MainWindow.RawInputRegistration`、`InputManager.XInputState`、`SonyInputManager.DualSenseReportLayout`）—— 它们不是顶层类型，移动会破坏封装 |

---

## 3. 拆分顺序（风险由零到高，每批一次提交）

### Batch 0 · 构建脚本改造（🟢 **已完成 2026-09-22**）

**原施工图漏掉的前置条件**：`build.ps1` 用的是**硬编码的 20 个根目录文件清单**（第 40–59 行），不递归、不扫目录。**不先改它，任何放进子目录的新文件都编译不到**，拆分第一步就会静默失败。

改动：清单 → 递归 glob + 排除 `bin/ obj/ release/ docs/ Assets/ audit*/`。
验证：改前改后编译的是**同一批 20 个文件**（根目录 `.cs` 恰好等于原清单，子目录里 0 个 `.cs`）；产物 4,302,336 B vs 基线 4,302,848 B（差异仅来自时间戳）；`--startup-selftest` 通过。
**踩坑（会挂掉所有构建）**：`Sort-Object -ExpandProperty` 在 Windows PowerShell 里**不存在**（PS 7 才有）→ 改用 `Sort-Object FullName | ForEach-Object { $_.FullName }`。

### Batch 1 · 数据与主题（🟢 **已完成 2026-09-22**）

提取 `Models/` 21 个纯数据类/枚举 + `Theme/Palette.cs`。

| 指标 | 结果 |
| --- | --- |
| `ControllerLab.cs` | 11,037 → **10,458 行**（−579） |
| 新建文件 | 22 个（21 Models + 1 Theme） |
| 编译 | ✅ 通过 |
| 自检 | ✅ `--startup-selftest` / `--runtime-selftest` / `--controller-core-selftest` / `--ds5-overlay-selftest` / `--xbox-overlay-selftest` 全过（退出码 0） |
| 提交 | `refactor(structure): extract models and theme palette from ControllerLab.cs` |

**工具**：`Tools/split_helper.py` —— 机械提取器（默认 dry-run，`--apply` 才写盘）。Batch 2–5 复用它，只需扩充批次映射表。


### Batch 2 · 自绘控件（🟢 低风险）
`Controls/` 8 个 `FrameworkElement` 子类。
独立渲染单元，只依赖 `Palette`/`Tokens` 与传入数据；**不得访问设备状态**。

### Batch 3 · 引擎与管理器（🟡 中风险）
`Services/` 7 个。含 `InputManager`（XInput/RawInput）、`SonyInputManager`（HID）、`DualSenseRegionManager`。
**风险点**：静态状态、跨类事件订阅、`IDisposable` 释放顺序。拆完必须逐一跑自检。

### Batch 4 · 窗口与视图（🟡 中风险）
`Views/` 7 个。`App`/`Program` 含启动与自检入口（`--startup-selftest` 等），**动它们会影响所有自检命令**，需重点回归。

### Batch 5 · MainWindow 切 partial（🔴 高风险 / 最大收益）
5,042 行 → 9 个 partial 文件，见 §4。

---

## 4. Batch 5：MainWindow 的 9 份切法

| 部分文件 | 职责 | 判据 |
| --- | --- | --- |
| `MainWindow.Shell.cs` | 构造函数、字段、窗口生命周期、`CompositionTarget.Rendering` 主循环、导航切换 | 只在 `MainWindow` 自身 |
| `MainWindow.DeviceHome.cs` | 设备首页、设备卡片、切换设备 | 页面 1 |
| `MainWindow.Visualizer.cs` | 实时监视（Xbox/DS5 可视化、扳机曲线） | 页面 2 |
| `MainWindow.HealthCheck.cs` | 完整检测流程、步骤条、进度清单 | 页面 3 |
| `MainWindow.StickTest.cs` | 摇杆专业检测（漂移/范围/死区/报告） | 页面 4 |
| `MainWindow.Rumble.cs` | 震动测试、时间线播放器、预设 | 页面 5 |
| `MainWindow.DualSense.cs` | DS 高级（触摸/运动/校准） | 页面 6 |
| `MainWindow.Reports.cs` | 历史报告列表与详情 | 页面 7 |
| `MainWindow.Settings.cs` | 设置、关于、隐私 | 页面 8 |

**切割红线**：
1. `RawInputRegistration` / `RawInputHeader` 两个私有嵌套 struct 只能留在**一个** partial 文件里（建议 `Shell`），其余部分作为同类的成员可直接引用。
2. 所有 partial 文件必须统一写 `namespace ControllerLab { public sealed partial class MainWindow : Window { ... } }`，且**只有一份**声明基类与接口。
3. 切割**只做移动**：不得顺手改名、改逻辑、改事件绑定，否则后续 `git diff` 无法审计。
4. 每切 2–3 个部分就编译一次，不要一次切完再编译。

---

## 5. 每批的标准作业流程（不可跳步）

```powershell
cd C:\Users\XinBai\Documents\Codex\2026-07-15\zu\outputs\xbox_controller_lab

# 1. 确认起点干净
git status -sb

# 2. 执行本批拆分（机械移动，不改逻辑）

# 3. 编译
.\build.ps1 -OutputName ControllerLab_Batch1.exe -Configuration Release

# 4. 运行相关自检（逐条，必须全部通过）
.\ControllerLab_Batch1.exe --startup-selftest
.\ControllerLab_Batch1.exe --runtime-selftest

# 5. 结构体检（本批必须让目标指标前进）
#    · ControllerLab.cs 行数应下降
#    · 新建文件数 = 本批类型数
#    · 单文件最大行数应下降

# 6. 提交（一次一批）
git add -A
git commit -m "refactor(structure): extract <批次名> from ControllerLab.cs"

# 7. 失败即回退，绝不带着红色工作树进入下一批
git checkout -- .   # 或 git reset --hard HEAD
```

### 防漂移检查（每批跑一次）

```bash
# 除 Theme/Tokens.cs 外不得出现颜色字面量
grep -rnE "#[0-9A-Fa-f]{6}" --include=*.cs . | grep -v "Theme/Tokens.cs"

# 不得存在驱动 UI 刷新的 DispatcherTimer（陷阱 A）
grep -rn "DispatcherTimer" --include=*.cs .
```

---

## 6. 完成判据与实际结果（2026-09-22 收工）

| 指标 | 拆分前 | 目标 | **实际** |
| --- | --- | --- | --- |
| `ControllerLab.cs` 行数 | 11,037 | 0（文件消失） | ✅ **已删除** |
| 拆分产出文件 | — | — | **53 个**（Models 21 / Theme 1 / Controls 8 / Services 7 / Views 7 + MainWindow 9 份） |
| MainWindow 单类行数 | 5,042 | ≤ 2,000 | ✅ 最大 **1,805**（`Views/MainWindow.Shell.cs`） |
| 编译 | ✅ | 全程保持 | ✅ 每批都过 |
| 全部自检 | — | 全程保持 | ✅ **11 项，每批全绿（含最后一批）** |
| 代码完整性 | — | 只搬不改 | ✅ 逐行多重集审计：**零行真实代码丢失**，唯一变更是 `class MainWindow` → `partial class MainWindow` |
| 工作树 | 干净 | 干净 | ✅ 干净 |

### ⚠️ 未尽事项：仍有 1 个文件超出 2,000 行目标

| 文件 | 行数 | 说明 |
| --- | --- | --- |
| `XboxOverlay.cs` | **3,193** | **不在本次拆分范围**（本次只拆 `ControllerLab.cs`）。需要 **Batch 6** 单独处理 |
| `ControllerCore.cs` | 1,840 | 达标 |
| `ControllerHealthCheckViewModel.cs` | 1,184 | 达标 |

### 执行方式（实际）

| 批次 | 执行者 | 结果 |
| --- | --- | --- |
| Batch 0 构建脚本 | 小马 | `build.ps1` 硬编码清单 → 递归 glob |
| Batch 1–4 | 小马（`Tools/split_helper.py`） | 44 个类型 → `Models/ Theme/ Controls/ Services/ Views/` |
| **Batch 5 MainWindow** | **Codex（gpt-5.6-luna, effort=medium）** | 5,119 行 → 9 个 partial 文件 |

**Codex 本次消耗**：周额度 **+1%**（63%→64%），1.82M token（其中 1.73M 命中缓存）。结论：**拆分本身极便宜，真正烧额度的是"在臃肿仓库里反复全量读代码"——而那正是本次拆分消灭掉的东西。**


---

## 7. 与既有图纸的关系（重要）

本文档**不改变任何视觉规格**，只调整执行顺序。开工前需拍板一处图纸内部矛盾：

| 出处 | 拆分时机 |
| --- | --- |
| 状态矩阵 §5-B | **先拆结构 → 再改视觉**（本文档采用） |
| 视觉目标规格 §7 | `Token → 组件库 → 页面（拆一页、改一页、验一页）` |

**采用"先拆"的理由**：Codex 任务 Step 1 要求「把四个骨架**接进现有窗口**做视觉验证」——只要接进现有窗口，就必须打开 `ControllerLab.cs`。**不先拆，Step 1 就开始付上下文税**；而拆分是机械活，可一次性完成且不消耗模型额度。

---

## 8. 拆分之后的流水线

```
① 结构拆分（本文档，机械活）
        ↓
② Codex Step 0  →  Theme/Tokens.cs        设计令牌层
③ Codex Step 1  →  4 个全局骨架组件
④ Codex Step 2  →  14 个基础控件（3 批）
⑤ Codex Step 3  →  8 个可视化控件（2 批）
        ↓
⑥ Step 4 前检查：22 组件巡览页 + 防漂移检查 + 结构体检
        ↓
⑦ 逐页组合「拆一页 → 改一页 → 验一页」
        ↓
⑧ 视觉验收（小马代跑）：1586 × 992 / 100% DPI 截图 → 与 9 张基线图逐区比对 → 出差异清单
```
