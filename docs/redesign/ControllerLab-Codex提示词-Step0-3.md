# ControllerLab 改版 · Codex/Luna 提示词（Step 0–3）

> **配合文档**：[[ControllerLab-组件库规格]]（施工图）｜[[ControllerLab-视觉目标规格]]（色值/布局）｜[[ControllerLab-改版实现规格-状态矩阵]]（行为）
>
> **用法**：**一次只贴一个任务**。Step 0→1→2→3 顺序执行，每个任务一次提交。

---

## 0. 前置准备（做一次）

### 0.1 把规格录进项目（进版本控制，Codex 读得更稳）

```powershell
$dst = "C:\Users\XinBai\Documents\Codex\2026-07-15\zu\outputs\xbox_controller_lab\docs\redesign"
New-Item -ItemType Directory -Force $dst | Out-Null
Copy-Item "D:\Obsidian Vault\仓库\ControllerLab\*.md" $dst -Force
Get-ChildItem $dst
```

然后 `git add docs/redesign && git commit -m "docs: add redesign specs"`。

### 0.2 通用规则（每个任务都适用，已内嵌进下面的提示词）

| 项 | 规则 |
| --- | --- |
| 读文档 | 每个任务开头读 `AGENTS.md` + 本任务指定的章节，**不要通读全部规格**（浪费上下文） |
| 一次一任务 | 不要把多个 Step 合并；不要一次做十几个组件 |
| 边界 | 不改 Overlay 坐标/遮罩；不改现有输入链路；不加 NuGet 依赖；不新增规格外功能 |
| 禁止 | 页面代码出现十六进制色值 / `new SolidColorBrush` / 字号字面量；用 `DispatcherTimer` 驱动 UI 刷新 |
| 收工 | 构建 → 跑相关自检 → 更新 `docs/PROJECT_STATUS.md` → 一次规范提交 → 说明实机验证状态 |

---

## 1. 通用提示词模板（先看模板，再看下面的具体任务）

```
【任务】<一句话>

【开工前必读】
- AGENTS.md
- docs/redesign/<具体文档> 的 §<具体章节>
- git status -sb

【交付】
<明确到文件与公开成员>

【硬性要求】
- 颜色/尺寸字面量只允许出现在 Tokens.cs
- 不改动现有页面行为与输入链路
- 不引入新依赖
- 组件文件 ≤300 行，页头注释写明用途与出现页面

【收工】
- 构建（.\build.ps1 -OutputName <名>.exe）
- 运行 <相关自检命令>
- 更新 docs/PROJECT_STATUS.md
- 提交：<commit message>
```

---

## 2. Step 0 — 设计令牌层

```
【任务】ControllerLab 视觉改版 Step 0：建立设计令牌层（唯一改色入口）

【开工前必读】
- AGENTS.md
- docs/redesign/ControllerLab-组件库规格.md 的 §0 全节（通用实现约定 + 尺寸令牌）
- docs/redesign/ControllerLab-视觉目标规格.md 的 §1（设计令牌）
- git status -sb

【交付】
新建 Tokens.cs（建议放 Theme/Tokens.cs），公开以下 static readonly 成员：
- 颜色（Brush）：BgBase #07131C / BgPage #0B1722 / BgPanel #12202C / BgPanelRaised #192D3C
  BorderSubtle #24343F / BorderStrong #465865
  TextPrimary #AECEDF（淡蓝白，不是纯白，不得改成 #FFFFFF）/ TextSecondary #6B818C / TextDisabled #4A5F6B
  Accent #2FD6F8 / AccentDim #1E7080
  StateSuccess #68F0A8 / StateWarning #E8A33D / StateDanger #E0484F / StateNeutral #5B6E7A
- 字色对应的 Pen（1px/2px 描边用）
- 字号与字重常量：页标题 30/700、面板标题 18/600、卡片标题 20/600、正文 14、辅助 12、大数值 34/700、导航 15/500
- 间距常量：页面边距 32、卡间距 16、卡内边距 20
- 圆角常量：卡片 12、控件 8、胶囊 999
- 控件高度：主按钮 46、次按钮 44、输入/下拉 40、分段 40、顶栏 72、设备条 56、页脚条 40、行高 44/56
- 私有工厂 Frozen(string hex) → Brush，所有返回的 Brush/Pen 必须 Freeze()

【硬性要求】
- 这些数值是本任务唯一允许出现颜色字面量的位置
- 不改动任何页面行为；不新增依赖

【收工】
- 构建、运行 --startup-selftest
- 更新 docs/PROJECT_STATUS.md（新增「设计令牌层」章节）
- 提交：chore(theme): add design tokens layer
```

---

## 3. Step 1 — 全局骨架（4 个组件）

```
【任务】ControllerLab 视觉改版 Step 1：实现全局骨架四件套

【开工前必读】
- AGENTS.md
- docs/redesign/ControllerLab-组件库规格.md 的 §0 与 §2（全局骨架）
- docs/redesign/ControllerLab-视觉目标规格.md 的 §2（顶栏/设备条/页脚/页头规格）
- docs/redesign/ControllerLab-改版实现规格-状态矩阵.md 的 §2.5（导航规则）
- Theme/Tokens.cs（上一步产物）
- git status -sb

【交付】四个独立文件，每个 ≤300 行：
1. TopBar.cs —— CL logo + 「手柄实验室/ControllerLab」+ 8 项导航（设备/实时监视/完整检测/摇杆检测/震动测试/DS 高级/历史报告/设置）+ 窗口按钮
   · 选中项：Accent 描边胶囊 + Accent 文字；未选中 TextSecondary
   · **导航按当前选中设备能力动态过滤（决策 D1/D6）**：连 Xbox 时不出现「DS 高级」；隐藏而非灰显；隐藏后其余项顺延不留空位
   · 未接入真实设备时，按「无过滤」渲染并留 TODO
2. PageHeader.cs —— 接受标题/副标题/右侧动作区（ContentPresenter）
3. DeviceBar.cs —— ● 连接点 + 设备名 + | + 连接方式 + | + 电量
   · **电量能力驱动（D5）**：无电量能力时整段（含分隔符与图标）不出现，不显示「未知/—」
   · 右侧「切换设备」次按钮
4. FooterBar.cs —— 左 `● 设备已就绪`（StatusDot）+ 右上下文文字

【硬性要求】
- 全部使用 Tokens；不得出现颜色字面量
- 组件不得直接访问 ControllerDeviceManager 等状态类，数据由页面注入
- 无设备/断开状态下必须能安全渲染
- 不改动现有页面行为

【收工】
- 构建、运行 --startup-selftest
- 把四个骨架接进一个页面做视觉验证（可临时挂在现有窗口顶部/底部，不影响功能）
- 更新 docs/PROJECT_STATUS.md
- 提交：feat(ui): add global shell components (topbar/header/devicebar/footerbar)
```

---

## 4. Step 2 — 基础控件（14 个，分 3 批）

### Batch A（5 个）
```
【任务】ControllerLab 视觉改版 Step 2A：基础控件（Button / Card / Segmented / Badge / StatusDot）

【开工前必读】
- AGENTS.md
- docs/redesign/ControllerLab-组件库规格.md 的 §0 与 §1–8（这五个组件的规格表）
- Theme/Tokens.cs
- git status -sb

【交付】5 个独立文件，每个 ≤300 行，按规格表实现全部变体与状态：
- Button：主/次/危险/幽灵 + 悬停/按下/禁用/聚焦 + 可选左图标/下箭头
- Card：普通/抬升/选中（2px Accent 描边 + 外发光，仅描边不改背景）+ 标题行
- Segmented：胶囊导航 / 分段选择 / 大 tab 三种形态
- Badge：成功/警告/中性/危险
- StatusDot：绿/琥珀/灰

【硬性要求】
- 禁止任何 Windows 原生白色控件
- 颜色全部走 Tokens；组件不访问设备状态
- 禁用态必须清晰可读（不得仅靠降透明度）

【收工】
- 构建、运行 --startup-selftest
- 提供一份"组件展示"临时页面或自检入口，可逐个查看四种状态（便于截图验收）
- 更新 docs/PROJECT_STATUS.md
- 提交：feat(ui): add base controls batch A
```

### Batch B（5 个）
```
【任务】ControllerLab 视觉改版 Step 2B：基础控件（ProgressBar / Notice / EmptyState / ListRow / Table）
【开工前必读】同上；读组件库规格 §0 与 §10–13、§22、§12
【交付】5 个文件，每个 ≤300 行
- ProgressBar：Accent 填充 + Neutral 轨道，右侧可带百分比
- Notice：信息/警告/危险（左竖条 3px + 图标；背景仅 10–15% 透明度）
- EmptyState：图标 + 标题 + 说明 + 可选按钮
- ListRow：图标 + 主文本 + 副文本 + 右侧状态/箭头；选中态 Accent 描边
- Table：暗色表头（无背景色差）、1px 行分隔、结果列支持彩色字/StatusDot；禁止原生 DataGrid 皮肤
【收工】同 Batch A，提交：feat(ui): add base controls batch B
```

### Batch C（4 个）
```
【任务】ControllerLab 视觉改版 Step 2C：基础控件（Toggle / Slider / Select / Checkbox）
【开工前必读】同上；读组件库规格 §0 与 §4–7
【交付】4 个文件，每个 ≤300 行
- Toggle：44×24，on=Accent 底+白滑块，off=Neutral 底
- Slider：行高 44 + 数值框（宽 72）；青色/琥珀色两变体；禁用态
- Select：暗色下拉，展开面板 BgPanelRaised + 8px 阴影；禁用态
- Checkbox：18×18，勾选 = Accent 实心
【关键】这四个最容易误用系统原生控件，必须完全自绘/重模板
【收工】同 Batch A，提交：feat(ui): add base controls batch C
```

---

## 5. Step 3 — 数据可视化控件（8 个，分 2 批）

### Batch A（4 个）
```
【任务】ControllerLab 视觉改版 Step 3A：可视化控件（CrosshairPlot / DriftPlot / MetricTile / ScoreBar）

【开工前必读】
- AGENTS.md
- docs/redesign/ControllerLab-组件库规格.md 的 §0 与 §15、§16、§19、§11
- docs/redesign/ControllerLab-改版实现规格-状态矩阵.md 的 §3-04、§3-08（状态与"上次结果/本轮结果"区分要求）
- Theme/Tokens.cs
- git status -sb

【交付】4 个文件，每个 ≤300 行
- CrosshairPlot：同心圆 + 十字 + 中心点；未开始/采样中/完成 三态视觉必须可区分
- DriftPlot：±1.0 双轴 + 同心圆 + 采样点簇 + 右上"中心放大(5x)"内嵌图；支持"上次结果"与"本轮结果"两套描边风格
- MetricTile：标签 + 大数值(34/700) + 对比行；**无数据时数值显示 `—`，不得填示例数字**
- ScoreBar：横条 + 分数；**未检测变体必须无填充、无分数，右侧灰字「未检测」**

【硬性要求】
- 图表不得使用第三方图表库（不新增依赖），用原生 WPF 绘制
- 静态渲染路径不得使用 DispatcherTimer
- 无数据/无设备时必须安全渲染

【收工】
- 构建、运行 --startup-selftest
- 提供可逐个查看各状态的临时展示入口
- 更新 docs/PROJECT_STATUS.md
- 提交：feat(ui): add visualization controls batch A
```

### Batch B（4 个）
```
【任务】ControllerLab 视觉改版 Step 3B：可视化控件（TimelineChart / TouchGrid / PresetTile / StepBar）
【开工前必读】同上；读组件库规格 §17、§18、§20、§21 与 §3-06、§3-07
【交付】4 个文件，每个 ≤300 行
- TimelineChart：双通道（左 Accent / 右 Warning）+ 0/20/40% 刻度 + 0–5s 轴 + 阶梯曲线 + 15% 透明填充 + 播放游标
  · 停止/播放/暂停三态视觉必须可区分；峰值不得超 40%
- TouchGrid：24×12 网格 + 覆盖单元 + 触点编号 + 轨迹折线（≤1600 点上限）
- PresetTile：图标 + 标题 + 两行说明；选中 Accent 描边；圆角 10（非胶囊）
- StepBar：完成(Success 勾)/当前(Accent 描边+数字)/未开始(Neutral)/跳过(斜线) 四态；隐藏步骤不留空位
【收工】同 Batch A，提交：feat(ui): add visualization controls batch B
```

---

## 6. Step 4 之前的检查清单

Step 0–3 全部完成后，**先别急着改页面**，做这三件事：

```
1. 组件巡览：用一个临时页面把 22 个组件的所有状态全部铺出来，截图给我看
        → 我做视觉验收，对照 9 张目标图逐项核对色值/尺寸
2. 防漂移检查（grep 全项目）：
        · 除 Tokens.cs 外不得出现 #十六进制 / new SolidColorBrush
        · 不得存在用于 UI 刷新的 DispatcherTimer
3. 结构体检：ControllerLab.cs 行数变化；新建文件数；单文件最大行数
        → 若 ControllerLab.cs 反而变大，说明组合方式没走通，及时纠正
```

**这三项通过后，才进入 Step 4（逐页组合）**，并且按「拆一页 → 改一页 → 验一页」的节奏推进。

---

## 7. 遇到卡壳时

| 情况 | 处理 |
| --- | --- |
| 模型自行脑补规格外行为 | 拒绝，标记 `NEEDS-OWNER`，把问题抛回来 |
| 组件在无设备状态下崩溃 | 视为不通过，必须修（规格 §0.1 第 6 条） |
| 出现原生白色控件 | 视为不通过 |
| 同一批组件改了好几轮还不对 | **停手**，把失败点告诉我 —— 多半是规格歧义，改规格比继续试错便宜 |
