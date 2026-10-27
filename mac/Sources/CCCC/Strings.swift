import Foundation

enum L {
    static var zh = false

    static let againEn = ["Copied", "Still copied", "Yes, it's copied", "Definitely copied", "Really. It's copied.", "It's there. Go paste."]
    static let againZh = ["已复制", "还在呢", "真的复制好了", "放心，复制好了", "千真万确，已复制", "去粘贴吧"]

    // pref is "en", "zh", or nil to follow CCCC_LANG and then the system language.
    static func apply(_ pref: String?) {
        if pref == "en" || pref == "zh" { zh = pref == "zh"; return }
        if let forced = ProcessInfo.processInfo.environment["CCCC_LANG"], !forced.isEmpty {
            zh = forced.lowercased().hasPrefix("zh")
        } else {
            zh = Locale.preferredLanguages.first?.hasPrefix("zh") == true
        }
    }

    static func t(_ en: String, _ zhText: String) -> String { zh ? zhText : en }

    static var again: [String] { zh ? againZh : againEn }

    static var cut: String { t("Cut", "已剪切") }
    static var nothingCopied: String { t("Nothing copied", "没有复制上") }
    static var nothingCut: String { t("Nothing cut", "没有剪切上") }
    static var stillHolding: String { t("Still holding: ", "剪贴板仍是：") }
    static var maybeEmpty: String { t("The selection may be empty", "可能没有选中内容") }

    static var link: String { t("Link", "链接") }
    static var image: String { t("Image", "图片") }
    static var imageHold: String { t("an image ", "图片 ") }
    static var file: String { t("File", "文件") }
    static var folder: String { t("Folder", "文件夹") }
    static var text: String { t("Text", "文本") }
    static var previewNeedsPaste: String { t("Preview needs “Paste from Other Apps” permission", "预览需要“从其他 App 粘贴”权限") }
    static var private_: String { t("Private", "已隐藏") }
    static var privateSub: String { t("•••••••• hidden for privacy", "•••••••• 敏感内容不显示") }
    static var rich: String { t("Rich content", "其他格式内容") }
    static var appData: String { t("App data", "程序专用") }
    static func keptIn(_ app: String) -> String { t("Kept inside " + app, "保存在 " + app + " 里") }
    static func appContent(_ app: String) -> String { t(app + " content", app + " 的内容") }
    static var blank: String { t("(whitespace)", "（空白字符）") }

    static func chars(_ n: Int) -> String { zh ? N(n) + " 字" : N(n) + (n == 1 ? " char" : " chars") }
    static func lines(_ n: Int) -> String { zh ? N(n) + " 行" : N(n) + " lines" }
    static func items(_ n: Int) -> String { zh ? N(n) + " 项" : N(n) + " items" }
    private static func N(_ n: Int) -> String {
        let f = NumberFormatter()
        f.numberStyle = .decimal
        f.locale = Locale(identifier: "en_US")
        return f.string(from: NSNumber(value: n)) ?? String(n)
    }

    static var readyTitle: String { t("CCCC is on", "CCCC 已就绪") }
    static var readySub: String { t("Copy anything. I'll confirm it.", "随便复制点什么，我来确认。") }
    static var runningTitle: String { t("Already running", "CCCC 已在运行") }
    static var runningSub: String { t("CCCC lives in your menu bar.", "它就在菜单栏里。") }
    static var onClipboard: String { t("On your clipboard", "剪贴板里现在是") }
    static var emptyTitle: String { t("Clipboard is empty", "剪贴板是空的") }
    static var emptySub: String { t("Copy something and it shows up here.", "复制点什么，它就会出现在这里。") }
    static var caps: String { t("Caps Lock", "大写锁定") }
    static var capsWidth: String { t("Card length", "卡片长度") }
    static var hintCapsWidth: String { t("How wide the Caps Lock card is", "大写锁定卡片的宽度") }
    static var capsHeight: String { t("Card height", "卡片高度") }
    static var hintCapsHeight: String { t("How tall the Caps Lock card is", "大写锁定卡片的高度") }

    // Wi-Fi card. Details are joined with " · " and each shows only when known.
    static func wifiSwitched(_ name: String?) -> String { name.map { t("Switched to " + $0, "已切换到 " + $0) } ?? t("Wi-Fi switched", "Wi-Fi 已切换") }
    static func wifiFrom(_ old: String) -> String { t("From " + old, "原为 " + old) }
    static func wifiNowBand(_ name: String?, _ b: Int) -> String {
        let g = band(b) ?? ""
        return name.map { t($0 + ": now " + g, $0 + "：已切换到 " + g) } ?? t("Wi-Fi now " + g, "Wi-Fi 现在是 " + g)
    }
    static func wifiWasBand(_ b: Int) -> String { t("Was " + (band(b) ?? ""), "原为 " + (band(b) ?? "")) }
    static func wifiReconnected(_ name: String?) -> String { name.map { t("Reconnected to " + $0, "已重新连接 " + $0) } ?? t("Wi-Fi reconnected", "Wi-Fi 已重新连接") }
    static func wifiOffline(_ sec: Int) -> String { t("Offline for \(sec) s", "断开了 \(sec) 秒") }
    static var wifiDisconnected: String { t("Wi-Fi disconnected", "Wi-Fi 已断开") }
    static func wifiWasOn(_ name: String) -> String { t("Was on " + name, "之前连接的是 " + name) }
    static var wifiNeedLocation: String { t("Allow Location to see network names", "允许定位权限以显示网络名称") }
    static func band(_ b: Int) -> String? { b == 2 ? "2.4 GHz" : b == 5 ? "5 GHz" : b == 6 ? "6 GHz" : nil }
    static func signal(_ pct: Int) -> String { t("Signal \(pct)%", "信号 \(pct)%") }
    static func wifiConnected(_ name: String?) -> String { name.map { t("Connected to " + $0, "已连接 " + $0) } ?? t("Wi-Fi connected", "Wi-Fi 已连接") }
    static func detail(_ parts: String?...) -> String {
        parts.compactMap { $0 }.filter { !$0.isEmpty }.joined(separator: " · ")
    }
    static var menuWifi: String { t("Show Wi-Fi changes", "Wi-Fi 变化时提醒我") }
    static var hintWifi: String { t("Show a card under Control Center when the network changes", "网络变化时在控制中心下方弹出卡片") }
    static var tabWifi: String { "Wi-Fi" }
    static var wifiNames: String { t("Network names", "网络名称") }
    static var hintWifiNames: String { t("macOS asks for Location to show them", "macOS 需要定位权限才能显示名称") }
    static var wifiAllowed: String { t("Allowed", "已允许") }
    static var wifiNotAllowed: String { t("Not allowed", "未允许") }
    static var wifiNoAdapter: String { t("No Wi-Fi adapter", "没有 Wi-Fi 适配器") }
    static var wifiOpenLocation: String { t("Open Location settings", "打开定位设置") }

    // Sound output card. Details are joined with " · " and each shows only when known.
    static func audioTitle(_ name: String) -> String { t("Sound output: " + name, "声音输出：" + name) }
    static var audioNone: String { t("No sound output", "没有声音输出设备") }
    static func audioVolume(_ n: Int) -> String { t("Volume \(n)%", "音量 \(n)%") }
    static var audioMuted: String { t("Muted", "已静音") }
    static func audioWas(_ name: String) -> String { t("Was " + name, "原为 " + name) }
    static var menuAudio: String { t("Show sound output changes", "声音输出切换时提醒我") }
    static var tabAudio: String { t("Sound output", "声音输出") }

    static var settingsFile: String { t("Settings file", "设置文件") }
    static var hintSettingsFile: String { t("Export your settings to a file, or import a file from another computer. Pause stays as it is.", "把设置导出为文件，或导入另一台电脑导出的文件。暂停状态保持不变。") }
    static var exportSettings: String { t("Export…", "导出…") }
    static var importSettings: String { t("Import…", "导入…") }
    static var importFailed: String { t("Couldn't import this file.", "无法导入此文件。") }
    static var exportFailed: String { t("Couldn't export settings.", "无法导出设置。") }

    static var menuShow: String { t("Show clipboard", "查看剪贴板") }
    static var menuPause: String { t("Pause", "暂停") }
    static var menuSound: String { t("Sound", "提示音") }
    static var menuStopSounds: String { t("Stop all sounds", "停止所有声音") }
    static var menuHint: String { t("Warn when nothing was copied", "没复制上时提醒我") }
    static var menuCaps: String { t("Show Caps Lock changes", "切换大写锁定时提醒我") }
    static var menuInput: String { t("Allow Input Monitoring…", "授予“输入监控”权限…") }
    static var menuStartup: String { t("Open at Login", "登录时打开") }
    static var menuLanguage: String { t("Language", "语言") }
    static var menuQuit: String { t("Quit", "退出") }
    static var menuCheckUpdate: String { t("Check for updates…", "检查更新…") }
    static var menuAutoUpdate: String { t("Check for updates automatically", "自动检查更新") }
    static var updateChecking: String { t("Checking for updates", "正在检查更新") }
    static var updateCheckingSub: String { t("Asking GitHub…", "正在询问 GitHub…") }
    static var upToDate: String { t("You're up to date", "已是最新版本") }
    static func upToDateSub(_ ver: String) -> String { "CCCC " + ver }
    static var checkFailed: String { t("Couldn't check for updates", "检查更新失败") }
    static func updatePrompt(_ newVer: String, _ curVer: String, _ name: String?) -> String {
        let body = (name?.isEmpty ?? true) ? "" : name! + "\n\n"
        return t("CCCC " + newVer + " is available (you have " + curVer + ").\n\n" + body + "Update now? CCCC will restart.",
                 "CCCC " + newVer + " 已发布（当前 " + curVer + "）。\n\n" + body + "现在更新吗？CCCC 将自动重启。")
    }
    static func downloading(_ ver: String) -> String { t("Updating to " + ver, "正在更新到 " + ver) }
    static var downloadingSub: String { t("Downloading from GitHub…", "正在从 GitHub 下载…") }
    static func updateFailed(_ msg: String) -> String {
        t("Couldn't update automatically:\n" + msg + "\n\nOpen the download page instead?",
          "无法自动更新：\n" + msg + "\n\n要打开下载页面吗？")
    }
    static func updatedTitle(_ ver: String) -> String { t("Updated to " + ver, "已更新到 " + ver) }
    static var updatedSub: String { t("Thanks for keeping CCCC fresh.", "CCCC 已是最新。") }
    static var loginItemFailed: String { t("Couldn't change Open at Login", "无法修改登录时打开") }
    static var yes: String { t("Yes", "是") }
    static var no: String { t("No", "否") }

    static var menuSource: String { t("Show source", "显示来源") }
    static var menuSourceOn: String { t("On", "开启") }
    static var sourceWhen: String { t("When", "何时显示") }
    static var sourceAlways: String { t("Always", "总是显示") }
    static var sourceAuto: String { t("Only when not obvious", "仅在不明显时") }
    static var sourceDetail: String { t("Detail", "详细程度") }
    static var detailIcon: String { t("App icon only", "仅应用图标") }
    static var detailApp: String { t("App name", "应用名称") }
    static var detailSite: String { t("App + website", "应用 + 网站") }
    static var detailTitle: String { t("App + website + page/window title", "应用 + 网站 + 页面/窗口标题") }
    static var universalClipboard: String { t("Universal Clipboard", "通用剪贴板") }

    static var menuCustomize: String { t("Customize…", "自定义…") }
    static var customizeTitle: String { t("Customize CCCC", "自定义 CCCC") }
    static var tabGeneral: String { t("General", "常规") }
    static var tabAppearance: String { t("Appearance", "外观") }
    static var tabBehavior: String { t("Behavior", "行为") }
    static var tabSound: String { t("Sound", "声音") }
    static var tabSource: String { t("Source", "来源") }
    static var tabCaps: String { t("Caps Lock", "大写锁定") }
    static var tabAbout: String { t("About", "关于") }
    static var aboutVersion: String { t("Version", "版本") }
    static var aboutBuild: String { t("Build", "构建") }
    static var aboutWebsite: String { t("Website", "网站") }
    static var aboutDeveloper: String { t("Developer", "开发者") }
    static var aboutContributors: String { t("Contributors", "贡献者") }
    static var aboutLicense: String { t("License", "许可证") }
    static var resetDefaults: String { t("Reset to defaults", "恢复默认") }
    static var resetConfirm: String { t("Reset the card, position, sound and source settings to defaults?", "要将卡片、位置、声音和来源设置恢复为默认吗？") }
    static var closeWindow: String { t("Close", "关闭") }
    static var themeTitle: String { t("Theme", "主题") }
    static var themeSystem: String { t("System", "跟随系统") }
    static var themeLight: String { t("Light", "浅色") }
    static var themeDark: String { t("Dark", "深色") }
    static var paletteTitle: String { t("Palette", "配色") }
    static var paletteDefault: String { t("Default", "默认") }
    static var paletteGraphite: String { t("Graphite", "石墨") }
    static var paletteContrast: String { t("High contrast", "高对比") }
    static var palettePastel: String { t("Pastel", "柔和") }
    static var paletteGlass: String { t("Liquid Glass", "液态玻璃") }
    static var okColorTitle: String { t("Success colour", "成功颜色") }
    static var warnColorTitle: String { t("Warning colour", "警告颜色") }
    static var resetColor: String { t("Reset", "重置") }
    static var fullColors: String { t("Customize all colours", "自定义全部颜色") }
    static var colorBg: String { t("Background", "背景") }
    static var colorBorder: String { t("Border", "边框") }
    static var colorTitle: String { t("Title text", "标题文字") }
    static var colorSub: String { t("Body text", "正文文字") }
    static var colorMeta: String { t("Details text", "细节文字") }
    static var cardSize: String { t("Card size", "卡片大小") }
    static var fontTitle: String { t("Font", "字体") }
    static var fontDefault: String { t("Default", "默认") }
    static var textSize: String { t("Text size", "文字大小") }
    static var simpleMode: String { t("Simple mode", "简易模式") }
    static var transparency: String { t("Transparency", "透明度") }
    static var positionTitle: String { t("Position", "位置") }
    static var posNear: String { t("Near the mouse pointer", "鼠标附近") }
    static var posRandom: String { t("Random", "随机") }
    static var posSame: String { t("Same as copied card", "与复制卡片相同") }
    static var durationTitle: String { t("Show for", "停留时间") }
    static var stayUntilClicked: String { t("Stay until clicked", "直到点击才关闭") }
    static var playSounds: String { t("Play sounds", "播放提示音") }
    static var copiedSound: String { t("Copied sound", "复制成功音") }
    static var failSound: String { t("Nothing copied sound", "未复制成功音") }
    static var soundBuiltIn: String { t("Built-in", "内置") }
    static var soundCustom: String { t("Custom file", "自定义文件") }
    static var browse: String { t("Choose…", "选择…") }
    static var testSound: String { t("Test", "试听") }
    static var langSystem: String { t("System", "跟随系统") }
    static var noFile: String { t("No file", "无文件") }
    static var ignoreApps: String { t("Ignored apps", "忽略的应用") }
    static var hintIgnore: String { t("No card, sound or warning when copying in these apps", "在这些应用里复制时不弹卡片、不响、不提醒") }
    static var addApp: String { t("Add app…", "添加应用…") }
    static var remove: String { t("Remove", "移除") }

    static var tip: String { t("CCCC · copy with confidence", "CCCC · 放心复制") }
    static var tipPaused: String { t("CCCC · paused", "CCCC · 已暂停") }
}
