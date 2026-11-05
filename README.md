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
3. 右键图标进入 **设置…**，调整外观、位置、行为和其他提醒。
4. 需要临时停止提醒时使用 **暂停**；退出后重新启动可继续使用已有配置。

macOS 的复制失败检测需要输入监控权限，来源信息可能需要辅助功能权限；Wi-Fi 名称读取需要定位授权。请按应用提示授权。

## 鼠标附近时点击穿透

入口：**设置… → 行为 → 鼠标附近时点击穿透**，默认关闭，设置自动保存并支持 JSON 备份。

| 设置 | 鼠标附近的提醒行为 |
| --- | --- |
| 开启 | 鼠标点击和滚轮交给下方应用；提醒按配置时长自动消失 |
| 关闭 | 恢复点击关闭、点击链接等交互 |

仅影响位置设为 **鼠标附近** 的复制／剪切及 Caps Lock 卡片。其他位置和托盘定位的 Wi-Fi／声音输出提醒保持原有交互。

开启穿透时，悬停不会延长显示时间，点击不会关闭卡片或打开链接，**直到点击才关闭**不生效，以免卡片一直停留。需要这些交互时关闭穿透即可。

## 外观设置

在 **设置… → 外观** 中调整主题、配色、字体、尺寸及以下选项：

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

**Settings… → Behavior → Click through near-pointer cards** (off by default) lets mouse input pass through copy/cut and Caps Lock cards placed near the pointer. These cards expire after the configured duration; hovering does not extend them, clicks do not dismiss them or open links, and “Stay until clicked” is ignored. Turn the option off to restore interaction. Other positions and tray-anchored notifications are unchanged.

Appearance includes light/dark/system themes, Liquid Glass, custom colors, fonts, sizing, compact mode and 0–75% transparency. Settings persist and can be exported/imported as JSON.

Build Windows with `./build.ps1` and macOS with `bash mac/build.sh`.

## 许可证

[MIT](LICENSE)


## Windows 超级粘贴 / Super Paste

在 **设置… → 超级粘贴** 启用（默认关闭），设置快捷键、文本模式和九项独立清理规则。
默认快捷键 `Ctrl+Shift+Alt+V` 可修改；JetBrains 应用已使用它粘贴纯文本，启用前请留意应用内部冲突。
注册失败会显示错误，修改失败时保留已工作的旧快捷键。普通 `Ctrl+V` 不允许设置为超级粘贴快捷键。

超级粘贴会把完整 Unicode 文本按规则处理后，以纯文本粘贴，并将清理结果保留在剪贴板。
**不恢复原格式，不保留历史内容。** 非文本、图片／文件、带隐私标记的内容和空处理结果不写入。
文本输入上限约为 100 万 UTF-16 字符。等待组合键松开期间，若焦点或剪贴板变化则取消；
输入发送失败时，结果仍在剪贴板，可以手动粘贴。程序不会把发送按键等同于目标应用已成功插入。

- 默认 Markdown 模式，仅勾选“清理异常项目符号”和“删除文末换行”。普通文本模式可进一步清理空格和换行。
- 九项规则：六种异常私用区项目符号、Tab 展开、行首空格、行尾空格、中英文之间空格、重复空格、重复换行、补空行、文末换行。
- 仅删除 `U+F06C/F06E/F075/F0FC/F0D8/F0B2`，保留 `■ ● •`、带圈数字、其他私用区字符和 `U+000B`。
- Markdown 保守保护代码、列表、引用、表格、链接、HTML 和不确定语法；包含行内代码的整行也会保留。因此可能少清理，不会重新格式化全文。Markdown 模式禁用补空行，保留硬换行所需空格。
- 连续两行以上、每行 Tab 数量相同且至少一个 Tab 的区域按表格整块保留，两种模式均如此。
- 普通模式同时勾选换行去重与补空行时，先去重再补到一个空白行；Markdown 去重仅压缩多余空白行。
- Tab 按四字符制表位展开；已有换行风格保留。设置页的输入／结果预览不读取实际剪贴板。
- 设置自动保存并进入 JSON 导入／导出；暂停时释放快捷键，恢复后重新注册。编辑快捷键期间也暂时释放注册，避免误触粘贴。

运行 `./test-super-paste.ps1` 执行引擎、事务、注册冲突、设置与界面检查。
测试使用独立注册表路径；真实 SendInput 接收测试使用内存测试数据和拦截 WM_PASTE 的自建窗口，
不读取或覆盖用户剪贴板。构建仍使用 `./build.ps1`。

Windows only. Super Paste is off by default; configure it in **Settings → Super Paste**.
The customizable default `Ctrl+Shift+Alt+V` is also used by JetBrains apps. Plain `Ctrl+V` is reserved.
Processing pastes plain text and keeps the cleaned result on the clipboard, without restoring rich formats.
Markdown mode conservatively protects structural regions; plain mode supports additional whitespace cleanup.
Tab-separated tables are protected in both modes. Settings and examples are local; previews never read the clipboard.
This feature is not implemented on macOS. See [Super Paste notes](docs/SUPER_PASTE.md) for validation limits.
