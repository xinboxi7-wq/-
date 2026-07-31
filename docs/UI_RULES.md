# UI 与可视化规则

## 总体视觉语言

- 以中央大尺寸手柄为实时可视化页的主体；右侧诊断与底部 LT / RT 历史曲线是既有稳定布局。
- 保持深色、青蓝科技风；绿色仅用于正常 / 通过等正向状态，黄色用于轻微异常，红色用于严重异常。
- 按键反馈采用低透明度局部填充、贴边描边和柔和外发光；避免工程后台式大面积矩形与强烈闪烁。
- 原始调试数据默认收起；默认优先显示结论与当前状态。
- 普通页面布局可使用响应式 WPF 容器；已校准 Overlay 仅能使用其专用逻辑坐标系统。

## Overlay 保护规则

- 不得为修复对齐而移动手柄主图、改动比例或在单个区域上叠加 Margin、Canvas 偏移、独立 Scale / Translate Transform。
- Xbox 与 DualSense 资源分别管理；每套底图与 Overlay 在同一个 **1536 × 1024** 逻辑舞台中整体缩放。
- 不得随意移动已校准的 Xbox / DualSense 区域；用户校准数据只能写入 override，不能覆盖默认资源。
- D-pad 的当前对齐结果视为稳定资产；普通 UI 改动不得重绘或自动替换其区域。
- 真实 PNG Alpha 遮罩优先于重新生成近似 PathGeometry。Xbox 顶部左右区域使用共享真实遮罩，不得换回矩形或手绘扳机轮廓。

## 输入视觉规则

- 摇杆帽必须位于手柄主体之上并随真实轴值移动；固定光环不随摇杆帽移动。
- D-pad 仅高亮真实按下的方向；组合方向显示对应局部区域，不能用整块十字覆盖。
- ABXY / PS 对应面键、肩键、扳机及触摸板高亮仅覆盖对应硬件区域，Glow 不得用来掩盖几何不准。
- LT 压力填充方向为 **左到右**；RT 压力填充方向为 **右到左**。扳机曲线继续保留最近 5 秒，不得用简单进度条替代。
- 动态演示必须明确标识；不能产生正式检测报告或伪装为真实输入。

## 资源与配置

| 设备 | 文件 | 用途 |
| --- | --- | --- |
| Xbox | `Assets/controller.png` | Xbox 底图 |
| Xbox | `Assets/xboxRegions.json` | 默认区域与锚点配置 |
| Xbox | `Assets/LeftTopTriggerMask.png` | 顶部左侧共享真实 Alpha Mask |
| Xbox | `Assets/RightTopTriggerMask.png` | 顶部右侧共享真实 Alpha Mask |
| Xbox | `%LocalAppData%\ControllerLab\xbox-regions.override.json` | 用户校准 override |
| DualSense | `Assets/dualsense.png` | DualSense 底图 |
| DualSense | `Assets/dualsense-left-stick-cap.png`、`Assets/dualsense-right-stick-cap.png` | 摇杆帽资源 |
| DualSense | `Assets/dualSenseRegions.json` | 默认区域和触摸映射配置 |
| DualSense | `Assets/dualSenseVisualStyles.json` | 视觉样式配置 |
| DualSense | `%LocalAppData%\XboxControllerLab\dualSense-regions.override.json` | 用户校准 override |

## 验收方式

- Overlay 对齐先在关闭 Glow、透明 Fill、1px 高对比描边的模式下判断；发光效果不能作为位置准确性的判断依据。
- 必须在至少三种窗口尺寸，以及 100%、125%、150% DPI 下检查底图和 Overlay 同步缩放。
- 对已校准区域的改动必须提供近景前后对比，并明确实机 / 演示 / 渲染审计的验证来源。
