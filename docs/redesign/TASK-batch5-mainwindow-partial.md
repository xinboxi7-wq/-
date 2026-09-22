# TASK: Batch 5 — 把 MainWindow 拆成 partial class（结构拆分收尾）

> 派工单 · 由小马（包工头）出具 · 2026-09-22
> 交给 Codex 执行。执行者请**先读本文件全文再动手**。

---

## 背景（30 秒版）

`ControllerLab.cs` 原本 11,037 行 / 601 KB，是本项目所有额度浪费的根源。前 4 批已经把 44 个类型搬进 `Models/ Theme/ Controls/ Services/ Views/`，文件降到 **5,119 行**。

**现在这个文件里只剩一个类：`MainWindow`（约 5,000 行，占 98%）。** 本批把它用 `partial class` 切成 9 份，让这个文件彻底消失。

---

## 开工前必读

1. **本文件全文**
2. `AGENTS.md`（项目铁律）
3. `docs/redesign/ControllerLab-结构拆分施工图.md` 的 **§4**（9 份切法与切割红线）
4. `git status -sb`（必须干净；不干净就停下报告）

---

## 交付物

在 `Views/` 下新建 **9 个 partial 文件**：

| 文件 | 职责 | 判据 |
| --- | --- | --- |
| `Views/MainWindow.Shell.cs` | 构造函数、字段、窗口生命周期、`CompositionTarget.Rendering` 主循环、导航切换 | 只在 `MainWindow` 自身 |
| `Views/MainWindow.DeviceHome.cs` | 设备首页、设备卡片、切换设备 | 页面 1 |
| `Views/MainWindow.Visualizer.cs` | 实时监视（Xbox/DS5 可视化、扳机曲线） | 页面 2 |
| `Views/MainWindow.HealthCheck.cs` | 完整检测流程、步骤条、进度清单 | 页面 3 |
| `Views/MainWindow.StickTest.cs` | 摇杆专业检测（漂移/范围/死区/报告） | 页面 4 |
| `Views/MainWindow.Rumble.cs` | 震动测试、时间线播放器、预设 | 页面 5 |
| `Views/MainWindow.DualSense.cs` | DS 高级（触摸/运动/校准） | 页面 6 |
| `Views/MainWindow.Reports.cs` | 历史报告列表与详情 | 页面 7 |
| `Views/MainWindow.Settings.cs` | 设置、关于、隐私 | 页面 8 |

**文件划分依据是你读代码后的实际判断**，表格是意图说明。若某个成员归属不明，放进 `Shell` 并在报告里列出疑问，**不要自创第 10 个文件**。

完成后 `ControllerLab.cs` 必须**不再存在**（若剩空壳，删除该文件）。

---

## 硬性要求（违反即视为不通过）

1. **只做移动，不做改动。** 不得改名、不得改逻辑、不得调整语句顺序、不得顺手修 bug、不得格式化。目的是让 `git diff` 可审计 —— 除了"位置变了"，内容应当逐字一致。
2. **`public sealed partial class MainWindow : Window` 只能出现一次**（建议在 `Shell`）。其余 8 个文件写 `public sealed partial class MainWindow`，**不重复写基类**。
3. **字段与构造函数只能有一份**，放在 `Shell`。
4. **两个私有嵌套 struct `RawInputRegistration` / `RawInputHeader` 只能留在一个文件里**（建议 `Shell`）—— 它们是 `MainWindow` 的私有实现，拆开后其余部分作为同类成员仍可引用。
5. 每个新文件顶部保留完整 using 列表 + `namespace ControllerLab`，与原文件一致。
6. **不得触碰其他文件**（`Models/ Theme/ Controls/ Services/ Views/` 已有的 44 个文件、`build.ps1`、其他 `.cs` 一律不动）。
7. 不得新增依赖、不得引入 XAML。

---

## 收工流程（逐步执行，全部要做）

```powershell
cd C:\Users\XinBai\Documents\Codex\2026-07-15\zu\outputs\xbox_controller_lab

# 1. 编译
.\build.ps1 -OutputName ControllerLab_Batch5.exe -Configuration Release

# 2. 全量自检（11 项，必须全部退出码 0）
.\ControllerLab_Batch5.exe --startup-selftest
.\ControllerLab_Batch5.exe --runtime-selftest
.\ControllerLab_Batch5.exe --controller-core-selftest
.\ControllerLab_Batch5.exe --device-manager-selftest
.\ControllerLab_Batch5.exe --rumble-selftest
.\ControllerLab_Batch5.exe --stick-drift-selftest
.\ControllerLab_Batch5.exe --ds5-touch-parser-selftest
.\ControllerLab_Batch5.exe --ds5-motion-selftest
.\ControllerLab_Batch5.exe --ds5-overlay-selftest
.\ControllerLab_Batch5.exe --xbox-overlay-selftest
.\ControllerLab_Batch5.exe --trigger-chart-selftest

# 3. 结构体检
#    · ControllerLab.cs 应已消失
#    · Views/MainWindow.*.cs 应为 9 个
#    · 单文件最大行数应 ≤ 2,000

# 4. 清理构建产物（.exe 已被 .gitignore 忽略，但不要提交）
Remove-Item ControllerLab_Batch5.exe -ErrorAction SilentlyContinue

# 5. 提交
git add -A
git commit -m "refactor(structure): split MainWindow into partial classes

Batch 5 of 5 of the structural split. ControllerLab.cs is gone."
```

**任何一步失败：回退（`git checkout -- .`），不要带着红色工作树继续，并在报告里说明失败点。**

---

## 报告要求（用中文，必须包含）

1. 实际创建的 9 个文件 + 各自行数
2. `ControllerLab.cs` 是否已删除
3. 编译命令与结果
4. 11 项自检逐项结果（退出码）
5. 提交哈希
6. **你判断不了归属、勉强放进 `Shell` 的成员清单**
7. **任何不确定或未验证的部分** —— 明确区分"我验证过"和"我认为"

---

## 上下文说明（供参考，不影响交付）

- 本任务**不要求**做任何视觉改动。视觉改版是后续 Step 0–3 的事，任务书在 `docs/redesign/ControllerLab-Codex提示词-Step0-3.md`。
- 项目无 NuGet 依赖，无 XAML，编译器是 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`（由 `build.ps1` 调用）。
- `build.ps1` 已于本日改为**递归收集所有 `.cs`**（排除 `bin/ obj/ release/ docs/ Assets/ audit*/`），所以放进 `Views/` 的文件会被自动编译。
