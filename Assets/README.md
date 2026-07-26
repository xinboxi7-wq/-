# Assets 目录说明

本目录只保留运行时和开发期实际需要的资源。

## Xbox 运行时资源

- `controller.png`：Xbox 手柄主图，所有 Overlay 使用同一 1536×1024 逻辑坐标系。
- `stick-cap.png`：可移动摇杆帽。
- `LeftTopTriggerMask.png` / `RightTopTriggerMask.png`：顶部左右统一透明 Mask，分别由 LB/LT 与 RB/RT 共享。
- `xboxRegions.json`：Xbox 区域、锚点、填充方向与视觉参数。

## DualSense 运行时资源

- `dualsense.png`：DualSense 主图。
- `dualsense-left-stick-cap.png` / `dualsense-right-stick-cap.png`：摇杆帽图层。
- `dualSenseRegions.json`：DualSense 区域数据。
- `dualSenseVisualStyles.json`：DualSense 光效样式。

## Reference

`Reference/` 存放未参与运行时编译的历史抠图、色键和旧 Mask，便于追溯素材来源；它们不会被程序加载。自动审计截图和构建输出不属于 Assets，也不提交到 Git。

离线生成逻辑位于项目根目录的 `Tools/`，而不是运行时 Assets 中。
