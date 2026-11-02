# CCCC · 放心复制

给 Ctrl+C 一个看得见的反馈。复制后显示内容预览，重复复制会更新计数，未复制成功时给出提醒。
Windows 与 macOS 原生应用，启动后驻留托盘／菜单栏。

[下载 / Download](https://github.com/zw87652015/CCCC/releases)

![CCCC 提醒示例](image.png)

## 功能

- 复制、剪切与重复复制反馈，支持文本、链接、图片和文件预览。
- 提醒位置支持九宫格、鼠标附近及自由位置；卡片可调整显示时长。
- 跟随系统／浅色／深色主题，提供液态玻璃等配色、自定义颜色、字体和大小。
- 简易模式使用紧凑单行卡片；透明度可调为 0–75%。
- 可配置提示音、来源信息、忽略应用，以及 Caps Lock、Wi-Fi 和声音输出变化提醒。
- 支持 JSON 配置导入／导出，中英文界面及开机启动。

## 使用

1. Windows 便携版运行 `CCCC.exe`；macOS 打开 `CCCC.app`。
2. 复制内容后会出现提醒。左键点击托盘／菜单栏图标可查看当前剪贴板。
3. 右键图标进入 **自定义…**，调整外观、位置、行为和其他提醒。
4. 需要临时停止提醒时使用 **暂停**；退出后重新启动可继续使用已有配置。

macOS 的复制失败检测需要输入监控权限，来源信息可能需要辅助功能权限；Wi-Fi 名称读取需要定位授权。请按应用提示授权。

## 鼠标附近时点击穿透

入口：**自定义… → 行为 → 鼠标附近时点击穿透**，默认关闭，设置自动保存并支持 JSON 备份。

| 设置 | 鼠标附近的提醒行为 |
| --- | --- |
| 开启 | 鼠标点击和滚轮交给下方应用；提醒按配置时长自动消失 |
| 关闭 | 恢复点击关闭、点击链接等交互 |

仅影响位置设为 **鼠标附近** 的复制／剪切及 Caps Lock 卡片。其他位置和托盘定位的 Wi-Fi／声音输出提醒保持原有交互。

开启穿透时，悬停不会延长显示时间，点击不会关闭卡片或打开链接，**直到点击才关闭**不生效，以免卡片一直停留。需要这些交互时关闭穿透即可。

## 外观设置

在 **自定义… → 外观** 中调整主题、配色、字体、尺寸及以下选项：

- **简易模式**：默认关闭。开启后使用 220×48 DIP 的紧凑单行卡片，仅显示状态图标和标题，隐藏内容预览、缩略图、元信息、计数徽章和来源行；文字缩放可能增大实际尺寸。
- **透明度**：0–75%，默认 0%（完全不透明），作用于背景、文字、图标和阴影。

改动立即生效并在屏幕上弹出真实卡片预览，配置自动保存，重启后仍然生效。

## 构建

### Windows

需要 Windows、.NET Framework 编译工具，以及 Windows 10 SDK 的 `Windows.winmd`。

```powershell
./build.ps1
```

构建输出为 `dist/CCCC.exe` 和 `dist/CCCC-portable.zip`。构建前请退出该目录下正在运行的 CCCC，避免文件占用。

### macOS

需要 macOS 13 或更高版本，以及兼容项目的 Xcode／Swift 工具链。

```bash
bash mac/build.sh
```

输出为 `mac/build/CCCC.app`，默认构建 arm64 与 x86_64。可用 `CCCC_ARCHS` 指定架构，`CCCC_SIGN_ID=-` 使用临时签名。正式签名、公证及打包参见 `mac/release.sh`。

## English

CCCC gives visible feedback when you copy: content previews, repeated-copy counts and unsuccessful-copy hints. It is a native tray/menu bar utility for Windows and macOS.

**Customize… → Behavior → Click through near-pointer cards** (off by default) lets mouse input pass through copy/cut and Caps Lock cards placed near the pointer. These cards expire after the configured duration; hovering does not extend them, clicks do not dismiss them or open links, and “Stay until clicked” is ignored. Turn the option off to restore interaction. Other positions and tray-anchored notifications are unchanged.

Appearance includes light/dark/system themes, Liquid Glass, custom colors, fonts, sizing, compact mode and 0–75% transparency. Settings persist and can be exported/imported as JSON.

Build Windows with `./build.ps1` and macOS with `bash mac/build.sh`.

## 许可证

[MIT](LICENSE)
