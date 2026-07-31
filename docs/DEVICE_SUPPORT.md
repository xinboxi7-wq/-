# 设备支持矩阵

状态定义：

- **Supported and verified**：有代码且已记录真实设备验证。
- **Implemented, unverified**：已有实现和 / 或构造自检，但本基线没有可追溯的实机记录。
- **Partially supported**：仅某些报告、模式或基础子集可用。
- **Unsupported**：当前没有对应实现。
- **Unknown**：取决于设备或驱动，尚不能从代码确认。

> 当前基线不把动态演示、构造自检或 UI 截图视为真实硬件验证。

## Xbox / XInput 兼容设备

| 能力 | 状态 | 当前实现与限制 |
| --- | --- | --- |
| 设备发现与连接 | Implemented, unverified | `InputManager` 轮询 XInput 槽位；兼容设备取决于其 XInput 模式。 |
| 按键、D-pad、左右摇杆 | Implemented, unverified | 统一映射到 `ControllerState`。 |
| LT / RT | Implemented, unverified | 归一化后用于可视化、曲线和测试。 |
| 电量 | Partially supported | 使用 XInput 电池 API；USB、第三方驱动和接收器可能返回未知。 |
| 左右震动 | Implemented, unverified | 左=低频大马达，右=高频小马达；调用 `XInputSetState`。 |
| Guide / Share / Elite Paddles | Partially supported | 公共模型包含能力字段；是否可读取取决于 XInput 版本及设备。 |
| 触摸、陀螺仪、加速度计 | Unsupported | 当前 Xbox / XInput 路径不提供这些能力。 |

### 第三方飞智及其他兼容手柄

- **Unknown / Partially supported：** 只有处于 XInput 兼容模式并被 Windows 暴露为 XInput 设备时，基础输入和震动才会走 Xbox 路径。
- USB、蓝牙、2.4G 接收器的连接名称、电量和震动能力由设备固件 / 驱动决定，不能仅凭显示名称推断。
- 实机验证时必须记录型号、固件、连接模式与 `ControllerState.InputSource`。

## Sony DualSense / DualSense Edge

| 能力 | USB | 蓝牙 | 说明 |
| --- | --- | --- | --- |
| 原生 HID 基础输入 | Implemented, unverified | Implemented, unverified | 由 `SonyInputManager` 解析并合并到统一状态。 |
| 按键、D-pad、摇杆、L2/R2 | Implemented, unverified | Implemented, unverified | 完整或兼容输入布局均保留基础输入。 |
| 触摸板按压 | Implemented, unverified | Implemented, unverified | 由完整报告中的按压位驱动。 |
| 单指 / 双指触点 | Implemented, unverified | Partially supported | USB 完整 `0x01` 和蓝牙完整 `0x31` 最多解析两点；蓝牙紧凑兼容 `0x01` 明确不可用。 |
| 陀螺仪 / 加速度计 | Implemented, unverified | Partially supported | 完整 USB / BT 报告支持；BT 需 CRC 通过；紧凑兼容报告不支持。 |
| 电量 | Partially supported | Partially supported | 依赖当前 HID 报告提供的状态字段。 |
| 基础双通道震动 | Implemented, unverified | Implemented, unverified | USB `0x02` 长度 63；BT `0x31` 长度 78 且含 CRC；尚待实机输出确认。 |
| 高级触觉反馈 | Unsupported | Unsupported | 当前震动模块只实现基础双通道输出。 |
| 自适应扳机 | Unsupported | Unsupported | 当前没有输出控制实现。 |
| 灯带控制 | Unsupported | Unsupported | 状态模型保留 Lightbar 字段；没有控制 UI / 输出实现。 |
| 麦克风按钮 | Implemented, unverified | Implemented, unverified | 当前解析并进入 DualSense 扩展状态。 |

## DUALSHOCK 4

| 能力 | 状态 | 说明 |
| --- | --- | --- |
| 基础识别与输入 | Partially supported | 存在兼容解析路径，完整功能没有作为本基线承诺。 |
| 专属可视化与高级能力 | Unknown | 需要按实际 HID 报告和界面映射单独验证。 |

## 验证记录要求

新增真实设备结论时，必须在 [PROJECT_STATUS.md](PROJECT_STATUS.md) 和 [TESTING.md](TESTING.md) 记录：设备型号、连接方式、输入来源、软件版本、测试日期、成功 / 失败现象。未记录的能力保持 `Implemented, unverified` 或更保守的状态。
