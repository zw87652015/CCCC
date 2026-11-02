# 更新日志 / Changelog

## v1.10 — 2026-11-02

基于上游 v1.9 源码，当前分支新增以下改动。此条目表示源码版本，不表示已发布 GitHub Release 或商店更新。

### 新增

- “自定义 → 行为”增加“鼠标附近时点击穿透”开关，默认开启。
- 开关自动保存，支持配置导入／导出及恢复默认设置。
- Windows 使用透明分层窗口输入样式；macOS 使用 `ignoresMouseEvents`。

### 行为调整

- 开启穿透且卡片位于鼠标附近时，点击和滚轮交给下方应用，不再挡住实际操作。
- 适用于复制／剪切及 Caps Lock 提醒，包括 Caps Lock 跟随复制卡片位置或单独设为鼠标附近的情况。
- 穿透卡片按设置时长自动关闭，忽略悬停延长和“直到点击才关闭”，不响应点击关闭或打开链接，也不显示可点击链接图标。
- 关闭穿透后恢复原有点击交互。其他位置、自由拖动以及托盘定位的 Wi-Fi／声音输出提醒不受影响。

### 文档与验证

- 完善 README：功能、使用、外观配置、穿透规则、构建及验证说明。
- Windows 程序集／文件版本改为 `1.10.0.0`，macOS 应用版本与构建版本改为 `1.10.0`。
- 增加 25 项 Windows 回归检查，覆盖配置保存、JSON 往返、恢复默认、运行时开关和位置变化、不同卡片角色、系统命中测试与自动消失。
- Windows 构建通过；macOS 尚未编译运行验证。

### English

- Add an optional near-pointer click-through mode, enabled by default, with persistence and JSON backup support.
- Pass mouse input through copy/cut and Caps Lock cards near the pointer. Use timed dismissal instead of hover extensions, click actions or indefinite display.
- Preserve interaction at other positions and for tray-anchored notifications.
- Update documentation and both platform versions. Add 25 Windows regression checks; macOS compilation/runtime verification remains outstanding.

## 早期版本 / Earlier versions

此前版本未在此仓库维护独立更新日志。历史改动请查看 [上游提交记录](https://github.com/zw87652015/CCCC/commits/main/) 和 [发布记录](https://github.com/zw87652015/CCCC/releases)。
