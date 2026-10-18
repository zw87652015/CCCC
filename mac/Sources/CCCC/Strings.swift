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

    static var menuShow: String { t("Show clipboard", "查看剪贴板") }
    static var menuPause: String { t("Pause", "暂停") }
    static var menuSound: String { t("Sound", "提示音") }
    static var menuHint: String { t("Warn when nothing was copied", "没复制上时提醒我") }
    static var menuInput: String { t("Allow Input Monitoring…", "授予“输入监控”权限…") }
    static var menuStartup: String { t("Open at Login", "登录时打开") }
    static var menuLanguage: String { t("Language", "语言") }
    static var menuPosition: String { t("Notification position", "提醒位置") }
    static var positionLeft: String { t("Left", "左") }
    static var positionCenter: String { t("Center", "居中") }
    static var positionRight: String { t("Right", "右") }
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

    static var tip: String { t("CCCC · copy with confidence", "CCCC · 放心复制") }
    static var tipPaused: String { t("CCCC · paused", "CCCC · 已暂停") }
}
