Let's Ctrl+CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC

## 鼠标附近时点击穿透 / Near-pointer click-through

在 **自定义 → 行为 → 鼠标附近时点击穿透** 中开启或关闭，默认开启。
仅在复制／大写锁定提醒的位置为“鼠标附近”时生效，点击和滚轮交给下方应用。
开启后提醒按设置时长自动关闭，悬停不延长，也不响应点击关闭或打开链接；
“直到点击才关闭”在此模式下暂不生效。关闭穿透可恢复原有交互，其他位置与托盘提醒不受影响。

**Customize → Behavior → Click through near-pointer cards** (on by default) lets mouse
input reach the app underneath copy/Caps Lock cards placed near the pointer. These cards
expire after the configured duration even if “Stay until clicked” is enabled; hovering
does not extend them and clicks do not dismiss them or open links. Switch this option off
to restore interactions. Other positions and tray-anchored notifications stay interactive.

Windows regression checks: `./test-click-through.ps1` (isolated preferences and OS window hit testing).

![alt text](image.png)

## 简易模式与透明度 / Simple mode and transparency

右键托盘图标（macOS 为菜单栏图标），打开 **自定义… → 外观**，可以找到两个卡片外观选项：

- **简易模式**：默认关闭。开启后使用 220×48 DIP 的单行紧凑卡片，只显示状态图标和标题，隐藏内容预览、缩略图、元信息、计数徽章和来源行。
- **透明度**：0–75%（默认 0%，完全不透明）。作用于整张卡片，包括背景、文字、图标和阴影。

改动立即生效并在屏幕上弹出真实卡片预览，配置自动保存，重启后仍然生效。

Right-click the tray icon (menu bar icon on macOS) and open **Customize… → Appearance** to find two card options:

- **Simple mode**: off by default. Shows a compact 220×48 DIP one-line card with only the status icon and the title — content preview, thumbnail, meta info, count badge and source line are hidden.
- **Transparency**: 0–75% (0%, fully opaque, by default). Applies to the whole card, including background, text, icons and shadow.

Changes take effect immediately and pop the real card on screen; settings persist across restarts.
