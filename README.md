Let's Ctrl+CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC

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

## 定时暂停 / Timed pause

右键托盘／菜单栏图标，可以暂停 **15 分钟、30 分钟或 1 小时**，也可立即恢复。
原有“暂停”开关保留为手动暂停；手动开关会取消定时恢复。
定时暂停会记住恢复时间，退出重启后仍然有效，电脑唤醒时若已到期则自动恢复。
暂停期间停止自动复制、失败与 Caps Lock 提醒及其声音；主动查看剪贴板仍然可用。

Right-click the tray/menu bar icon to pause for **15, 30 or 60 minutes**, or resume immediately.
The existing Pause toggle is indefinite and cancels any timed resume. Deadlines survive
restarts and are reconciled after sleep. Automatic copy/failure/Caps Lock feedback and
sounds stop while paused; explicitly inspecting the clipboard remains available.

## 配置导入／导出 / Settings backup

打开 **自定义… → 常规 → 导出配置…／导入配置…**，使用 JSON 文件完整备份和恢复：

- 外观、颜色、字体、大小、位置、透明度与简易模式；
- 提醒行为、来源显示、Caps Lock 独立配置、语言与自动检查更新；
- 提示音开关、自定义声音文件路径和忽略应用列表。

文件不包含剪贴板内容、暂停状态、自启动或系统授权。首版只支持同平台恢复
（Windows → Windows，macOS → macOS）。字体和声音文件本身不打包；导入的资源不存在时恢复默认并提示。
缺失字段保留当前值，未知字段忽略；错误类型、越界值、不支持的版本或平台会拒绝整次导入。

导入先校验并确认，再自动备份原配置，保存失败会尝试回滚。最近一次导入前备份位于：

- Windows：`%LOCALAPPDATA%\CCCC\settings-before-import.json`
- macOS：`~/Library/Application Support/CCCC/settings-before-import.json`

Open **Customize… → General → Export settings… / Import settings…** for a versioned JSON
backup of appearance, reminder behavior, sources, Caps Lock preferences, language, update
checks, sounds and ignored apps. Clipboard contents, pause state, login startup and system
permissions are excluded. Restore files on the same platform. Font/audio files are not
embedded; unavailable imported resources fall back to defaults with a notice. Missing keys
retain current preferences; invalid files are rejected before changes. Import asks for
confirmation, saves the previous preferences to the locations above and rolls back on save failure.

Windows 构建：`./build.ps1`；回归检查：`./test.ps1`。测试使用独立注册表路径。
macOS 构建：`mac/build.sh`；核心回归检查：`bash mac/test.sh`，使用独立 UserDefaults 域。
