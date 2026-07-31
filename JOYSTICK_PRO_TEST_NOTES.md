# ControllerLab 摇杆专业检测测试版

- 构建：`ControllerLab_JoystickPro_Test.exe`
- 运行环境：Windows x64，.NET Framework 4.8
- 新增：5 秒静止漂移、72 区间圆周、左右摇杆四方向回中、三档死区建议、左右健康评分。
- 安全：检测互斥；设备断开、设备切换、页面离开和应用退出会取消；检测期间禁止启动震动；震动停止后至少等待 1 秒。
- 数据：分析使用既有输入适配器提供的、未应用显示校准和游戏死区的 `-1.0～1.0` 值。
- 自检：新增 10 项算法自检，并保留启动、运行时、导航、核心、旧漂移、设备、DualSense、Overlay、扳机曲线和震动回归。
- 限制：当前构建未连接真实 Xbox 或 DualSense 做机械实测；所有硬件结论均应视为待验证。

运行新增自检：

```powershell
.\ControllerLab_JoystickPro_Test.exe --joystick-analyzer-selftest
```
