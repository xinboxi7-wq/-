# ControllerLab 组件库规格（22 个）

> **用途**：`ControllerLab-视觉目标规格` 的施工图最后一环。把 22 个组件写死到"尺寸/令牌/状态/落地方式"级别，使 Luna 这类模型能**一次做对**，且 9 个页面只做"组合 + 绑定"，不再各自手写样式。
>
> **前置**：[[ControllerLab-视觉目标规格]]（实测色板、逐页布局比例）｜[[ControllerLab-改版实现规格-状态矩阵]]（行为与状态）
>
> **目标终态**：单文件 ≤ 2,000 行；单次任务上下文 ≤ 5 万 token。

---

## 0. 通用实现约定（先读这一节，它决定成败）

### 0.1 代码式 WPF 的复用方式（关键）

本项目**没有 XAML 资源字典**，所有 UI 由 C# 构造。因此**必须**用下面三层结构，否则会退化成"每页各写一套样式"：

```csharp
// 第 1 层：令牌（唯一改色入口）
static class Tokens {
    public static readonly Brush BgPage    = Frozen("#0B1722");
    public static readonly Brush BgPanel   = Frozen("#12202C");
    public static readonly Brush BorderSubtle = Frozen("#24343F");
    public static readonly Brush TextPrimary  = Frozen("#AECEDF");  // 淡蓝白，非纯白
    public static readonly Brush TextSecondary= Frozen("#6B818C");
    public static readonly Brush Accent       = Frozen("#2FD6F8");
    public static readonly Brush AccentDim    = Frozen("#1E7080");
    public static readonly Brush Success      = Frozen("#68F0A8");
    public static readonly Brush Warning      = Frozen("#E8A33D");
    public static readonly Brush Danger       = Frozen("#E0484F");
    public static readonly Brush Neutral      = Frozen("#5B6E7A");
    // 字号 / 间距 / 圆角 同处定义
}

// 第 2 层：组件工厂（唯一造控件入口）
static class Ui {
    public static Button PrimaryButton(string text)   { ... }
    public static Border Card(string title = null)    { ... }
    // ...
}

// 第 3 层：页面 = 组合 + 数据绑定，不出现任何颜色/尺寸字面量
```

**硬性规则**
1. 所有 `Brush` / `Pen` 必须 `Freeze()`（性能 + 防止跨线程问题）。
2. **页面代码里禁止出现任何十六进制颜色、`new SolidColorBrush`、字号字面量。** 一律走 `Tokens` / `Ui`。
3. 每个组件独立文件，**单文件 ≤ 300 行**，并在文件头用注释写明用途与出现页面。
4. 组件**不得直接访问设备状态**（不引用 `ControllerDeviceManager` 等）；数据由页面注入，组件只渲染。
5. **禁止任何 Windows 原生白色控件**（CheckBox / Slider / ComboBox / 表格头 / 滚动条）—— 必须样式化。
6. 组件必须能在"无设备 / 断开 / 禁用"状态下安全渲染（不抛异常、不显示假数据）。

### 0.2 通用尺寸令牌

| 令牌 | 值 | 令牌 | 值 |
| --- | --- | --- | --- |
| 页面边距 | 32 | 卡片圆角 | 12 |
| 卡间距 | 16 | 控件圆角 | 8 |
| 卡内边距 | 20 | 胶囊圆角 | 999 |
| 主按钮高 | 46 | 顶栏高 | 72 |
| 次按钮高 | 44 | 设备条高 | 56 |
| 输入/下拉高 | 40 | 页脚条高 | 40 |
| 分段控件高 | 40 | 拖拽/滑块行高 | 44 |

---

## 1–22 组件规格

> 记号：`尺寸` = 高 × 建议宽；`令牌` 引用 §0.1；`H` = Harness 可自动自检，`V` = 需视觉比对。

### 1. `Button`
| 项 | 规格 |
| --- | --- |
| 尺寸 | 主 46 高 / 次 44 高；最小宽 96；内边距 20×0 |
| 变体 | **主**：`Accent` 实心 + `#06131D` 文字 ｜ **次**：透明底 + `BorderStrong` 1px + `TextPrimary` ｜ **危险**：`Danger` 实心 ｜ **幽灵**：无边框 + `TextSecondary` |
| 状态 | 悬停（主色提亮 8%）/ 按下（降 8%）/ 禁用（`BgPanel` 底 + `TextDisabled` + 无描边）/ 聚焦（1px `Accent` 外框，保留给手柄导航） |
| 附加 | 可带左图标（16×16）；可带下箭头（用于"查看详细数据"） |
| 禁止 | 圆角内出现白底；禁用态不得只是降低透明度导致看不清 |

**出现页面**：全部 9 页

### 2. `Card`
| 项 | 规格 |
| --- | --- |
| 尺寸 | 圆角 12；内边距 20；可选标题行高 40 |
| 变体 | 普通（`BgPanel`）/ 抬升（`BgPanelRaised`）/ 选中（2px `Accent` 描边 + 外发光 `AccentDim` 30%） |
| 标题行 | 标题 18/600 `TextPrimary` + 右侧可选动作区（次按钮或说明文字 12px `TextSecondary`） |
| 禁止 | 选中态不得改背景色（只加描边，避免视觉跳动） |

**出现页面**：全部 9 页

### 3. `Segmented`
| 项 | 规格 |
| --- | --- |
| 尺寸 | 高 40；胶囊型圆角；项内边距 18×0 |
| 变体 | **胶囊导航**（顶栏 8 项，高 34）/ **分段选择**（04 页模式切换、09 页刷新率）/ **大 tab**（05、06 页，高 56，等宽撑满） |
| 状态 | 选中：`Accent` 实心 + `#06131D` 文字；未选：透明 + `TextSecondary` + `BorderSubtle` 1px；悬停：`AccentDim` 20% 底 |
| 禁止 | 选中项宽度变化导致其余项跳动（等宽或预留空间） |

**出现页面**：02(部分) 03 04 05 06 09

### 4. `Slider`
| 项 | 规格 |
| --- | --- |
| 尺寸 | 行高 44；轨道高 4；滑块 16×16 圆 |
| 变体 | **青色**（默认）/ **琥珀色**（06 页右侧马达）/ 禁用 |
| 组成 | 左标签（14px `TextPrimary`）+ 轨道 + 右侧数值框（宽 72，输入框样式） |
| 状态 | 拖动中：数值框实时更新；禁用：`Neutral` 轨道 + `TextDisabled` 标签 |
| 禁止 | 使用原生白色轨道；数值超过安全上限时不得静默截断（需夹紧并提示） |

**出现页面**：06 09

### 5. `Toggle`
| 项 | 规格 |
| --- | --- |
| 尺寸 | 44×24；滑块 20 圆 |
| 状态 | on：`Accent` 底 + 白滑块靠右；off：`Neutral` 底 + `TextSecondary` 滑块靠左；禁用：整体降饱和 |
| 交互 | 点击切换即生效（无确认），但设置页需标注"自动保存" |
| 禁止 | 使用原生 CheckBox 代替 |

**出现页面**：09

### 6. `Select`
| 项 | 规格 |
| --- | --- |
| 尺寸 | 高 40；宽按最宽项自适应或固定 140；圆角 8 |
| 状态 | 常态：`BgPanelRaised` + `BorderSubtle` 1px + 右侧下箭头；展开：面板 `BgPanelRaised` + 8px 阴影偏移；禁用：`TextDisabled` + 箭头降饱和 |
| 禁止 | 使用系统默认下拉（白底白箭头） |

**出现页面**：09（数值小数位，当前"3 位"）

### 7. `Checkbox`
| 项 | 规格 |
| --- | --- |
| 尺寸 | 方框 18×18 + 文字间距 10 |
| 状态 | 勾选：`Accent` 实心 + 深色勾；未选：透明 + `BorderStrong` 1px；禁用：`Neutral` |
| 禁止 | 原生白框 |

**出现页面**：09（设置自动保存）

### 8. `Badge`
| 项 | 规格 |
| --- | --- |
| 尺寸 | 高 22；内边距 10×0；圆角 6；字号 12/600 |
| 变体 | 成功（`Success` 20% 底 + `Success` 字）/ 警告（`Warning` 20% 底 + `Warning` 字）/ 中性（`Neutral` 20% 底 + `TextSecondary`）/ 危险 |
| 文案 | 「检测未完成」「检测完成」「需关注」 |

**出现页面**：01 08（+07 报告）

### 9. `StatusDot`
| 项 | 规格 |
| --- | --- |
| 尺寸 | 8×8 圆 + 文字间距 8 |
| 变体 | 绿=`Success`（已连接/已就绪）/ 琥珀=`Warning`（需关注）/ 灰=`Neutral`（未检测/未开始） |
| 禁止 | 用纯色文字代替圆点 |

**出现页面**：01 02 03 04 05 06 07 08（页脚与状态行）

### 10. `ProgressBar`
| 项 | 规格 |
| --- | --- |
| 尺寸 | 高 6；圆角 3 |
| 变体 | 常规（`Accent` 填充 + `Neutral` 轨道）/ 分段完成态 |
| 附加 | 右侧可带百分比文字（13px `TextSecondary`） |
| 禁止 | 使用原生 ProgressBar |

**出现页面**：03（71%）07（步骤进度）

### 11. `ScoreBar`
| 项 | 规格 |
| --- | --- |
| 尺寸 | 行高 44；轨道高 8；标签列宽固定 |
| 组成 | 项目名（14px `TextPrimary`）+ 轨道 + 右侧 `分数 / 满分`（13px） |
| 变体 | 绿（≥90）/ 青（70–89）/ 琥珀（<70）/ **未检测：无填充、无分数、右侧灰字「未检测」** |
| 禁止 | **未检测项不得出现任何填充条或数字**（概念图原稿的错误，见 E-表） |

**出现页面**：08

### 12. `Table`
| 项 | 规格 |
| --- | --- |
| 尺寸 | 行高 40；表头高 36 |
| 表头 | `TextSecondary` 13/600；无背景色差，仅底部分隔线 1px |
| 单元格 | 14px `TextPrimary`；结果列带 `StatusDot` 或彩色字 |
| 行分隔 | 1px `BorderSubtle` 10% 透明度 |
| 禁止 | 原生 `DataGrid` 默认皮肤（白色表头 + 网格线） |

**出现页面**：08

### 13. `Notice`
| 项 | 规格 |
| --- | --- |
| 尺寸 | 内边距 14；圆角 8；行高自适应 |
| 变体 | 信息（`Accent` 左竖条 3px + 图标）/ 警告（`Warning`）/ 危险（`Danger`） |
| 组成 | 左图标（18）+ 标题（可选，14/600）+ 文案（13px `TextSecondary`） |
| 用途 | 「静止检测期间震动已禁用」「仅展示已完成项目…」「离开页面或断开设备时自动停止」 |
| 禁止 | 用大色块背景（只允许 10–15% 透明度） |

**出现页面**：04 05 06 07 08

### 14. `EmptyState`
| 项 | 规格 |
| --- | --- |
| 尺寸 | 居中，垂直居中于容器 |
| 组成 | 图标 48（`TextSecondary`）+ 标题 18/600 + 说明 13px + 可选主按钮 |
| 用途 | 「未发现手柄」「暂无检测记录」 |
| 禁止 | 显示任何虚构的设备/记录占位卡 |

**出现页面**：01 08

### 15. `CrosshairPlot`
| 项 | 规格 |
| --- | --- |
| 尺寸 | 正方形，边长按容器自适应（建议 ≥240） |
| 组成 | 外圆 2 圈（`BorderSubtle`）+ 十字线 + 中心点（`Accent` 4px）+ 中心区高亮环 |
| 状态 | 未开始：仅坐标框；采样中：中心点抖动 + 进度环；完成：中心点固定 + 结论色 |
| 禁止 | 采样中与完成态使用相同视觉 |

**出现页面**：04（静止检测）07（向导步骤）

### 16. `DriftPlot`
| 项 | 规格 |
| --- | --- |
| 尺寸 | 正方形，≥280；含右上角"中心放大 (5x)"内嵌小图（≈90×90） |
| 组成 | ±1.0 双轴 + 同心圆（0.25/0.5/0.75）+ 采样点簇 + 中心十字 |
| 状态 | 左右摇杆各一实例；左用 `Success` 系、右用 `Accent` 系（色系区分） |
| 附加 | 曲线/点簇需有"上次结果"与"本轮结果"两套视觉，**严格区分且标注清楚** |
| 禁止 | 共用一个容器显示两轮结果 |

**出现页面**：04

### 17. `TimelineChart`
| 项 | 规格 |
| --- | --- |
| 尺寸 | 全宽 × 高 ≈300；左右通道各占一半高度 |
| 组成 | 两独立通道（左 `Accent` / 右 `Warning`）+ 各自 0/20/40% 刻度 + 横轴 0–5 s + 阶梯曲线（2px）+ 曲线下 15% 透明度填充 + 播放游标 |
| 状态 | 停止：曲线静态 + 游标隐藏；播放：游标推进；暂停：游标停住 + 输出归零 |
| 禁止 | 播放态与停止态视觉混淆；曲线超过 40% |

**出现页面**：06

### 18. `TouchGrid`
| 项 | 规格 |
| --- | --- |
| 尺寸 | 宽 ≥420；24×12 单元，单元间距 2；圆角 8 容器 |
| 组成 | 网格底（`BgPanelRaised` 单元 + `BorderSubtle` 1px）+ 覆盖单元（`Accent` 40% 底）+ 触点编号 + 轨迹折线（≤1600 点） |
| 状态 | 空闲：空网格；采集中：实时覆盖 + 轨迹；完成：保留轨迹 + 覆盖率数字 |
| 禁止 | 覆盖率 0 时显示为通过 |

**出现页面**：05

### 19. `MetricTile`
| 项 | 规格 |
| --- | --- |
| 尺寸 | 高 ≈92；内边距 16 |
| 组成 | 标签（12px `TextSecondary`）+ 大数值（34/700 `TextPrimary`）+ 对比行（12px，「上次结果 x」） |
| 状态 | 无数据：数值显示 `—` + 标签保留（**不显示示例数字**） |
| 禁止 | 用示例数值填充空数据 |

**出现页面**：02 04（08 摘要卡为其变体：大数值 + 名称）

### 20. `PresetTile`
| 项 | 规格 |
| --- | --- |
| 尺寸 | 高 ≈110；宽自适应（4 个横排）；内边距 16；圆角 10 |
| 组成 | 图标（24）+ 标题（14/600）+ 两行说明（12px `TextSecondary`） |
| 状态 | 选中：`Accent` 描边 2px；未选：`BorderSubtle` 1px；悬停：`AccentDim` 20% 底 |
| 圆角 | 10（非胶囊） |
| 禁止 | 用纯图标无文字 |

**出现页面**：06（14 个预设，默认展示 4 个 + 「14 个预设」标注）

### 21. `StepBar`
| 项 | 规格 |
| --- | --- |
| 尺寸 | 全宽 × 高 ≈80；步骤间距自适应 |
| 组成 | 项 = 圆形标（28）+ 步骤名（14px）+ 连接线（2px） |
| 状态 | 完成：`Success` 圆 + 勾；当前：`Accent` 描边圆 + 数字 + `Accent` 文字；未开始：`Neutral` 圆 + 数字 + `TextSecondary` 文字；**跳过**：`Neutral` 圆 + 斜线图标 |
| 动态 | 步骤按设备能力生成（DualSense 才出现触摸/陀螺仪相关），与导航过滤规则一致 |
| 禁止 | 隐藏步骤时留空位 |

**出现页面**：07

### 22. `ListRow`
| 项 | 规格 |
| --- | --- |
| 尺寸 | 行高 56；内边距 14；圆角 8 |
| 组成 | 左图标（20，可选）+ 主文本（14/600）+ 副文本（12px `TextSecondary`）+ 右侧状态（`StatusDot` 或 `Badge`）或箭头 |
| 状态 | 选中：`Accent` 描边 + `BgPanelRaised` 底；悬停：`BgPanelRaised` |
| 用途 | 08 记录列表｜07 进度清单｜01 连接帮助卡（变体：无箭头时用 `>`） |
| 禁止 | 主文本与副文本同字号同色 |

**出现页面**：01 07 08

---

## 23. 页面 → 组件组合清单

| 页面 | 用到组件 |
| --- | --- |
| 01 设备首页 | Card, Button, ListRow(变体), EmptyState, StatusDot, PageHeader, TopBar, FooterBar |
| 02 实时监视 | Card, MetricTile, CrosshairPlot(小), Segmented, Button, DeviceBar, FooterBar |
| 03 按键检测 | Card, Button, ProgressBar, Badge, StatusDot, DeviceBar, FooterBar |
| 04 摇杆检测 | Card, Segmented, DriftPlot, MetricTile, Notice, Button, DeviceBar, FooterBar |
| 05 DualSense 高级 | Segmented(大tab), TouchGrid, CrosshairPlot, Card, Button, Notice, Badge, DeviceBar |
| 06 震动测试 | Segmented, TimelineChart, PresetTile, Slider, Button, Notice, Card, DeviceBar |
| 07 完整检测 | StepBar, Card, CrosshairPlot, ListRow, Notice, Button, DeviceBar, FooterBar |
| 08 历史报告 | Card, ListRow, MetricTile(变体), ScoreBar, Notice, Table, StatusDot, Badge, Button |
| 09 设置 | Segmented(分段), Select, Toggle, Slider, Checkbox, Button, Card, ListRow, FooterBar |

**全局骨架**（9 页共有，必须先做）：`TopBar` / `PageHeader` / `DeviceBar`（01 页不显示）/ `FooterBar`

---

## 24. 施工顺序与验收

### 顺序（每步一个提交）
```
Step 0  Tokens.cs                              1 文件
Step 1  全局骨架：TopBar / PageHeader / DeviceBar / FooterBar          4 文件
Step 2  基础控件：Button Card Segmented Badge StatusDot ProgressBar
                  Notice EmptyState ListRow Table Toggle Slider Select Checkbox   14 文件
Step 3  数据可视化：CrosshairPlot DriftPlot TimelineChart TouchGrid
                  MetricTile ScoreBar PresetTile StepBar              8 文件
Step 4  逐页组合（拆一页、改一页、验一页）                              9 页
```

**Step 0–3 是机械活**：有明确对错、单文件小 → **用低成本模型（Luna）做** ✅
**Step 4 需要判断**：布局取舍、状态取舍、数据绑定逻辑 → 用更强的模型或分页细做。

### 验收
- **每个组件交付时**：能在无设备状态下安全渲染 + 四种状态（常态/悬停/禁用/选中）均可切换查看 + 无原生白色控件
- **每页交付时**：截图（1586×992）与目标图同页并排比对，区域比例 / 组件数量 / 色值三类逐项核对
- **色值核对**：主色 `#2FD6F8`、主文本 `#AECEDF`、页面底 `#0B1722` 为三个必查点
- **禁止项自动检查**：全项目 grep 不得出现 `SolidColorBrush` 字面量、`#` 十六进制色值（除 `Tokens.cs`）、`DispatcherTimer` 用于 UI 刷新

> 小马可代跑"启动应用 → 截图 → 与目标图逐区比对 → 出差异清单"的视觉验收环节。
