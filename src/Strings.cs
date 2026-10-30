using System;
using System.Globalization;
using System.Text;

namespace Cccc
{
    static class L
    {
        public static bool Zh { get; private set; }

        static readonly string[] AgainEn = { "Copied", "Still copied", "Yes, it's copied", "Definitely copied", "Really. It's copied.", "It's there. Go paste." };
        static readonly string[] AgainZh = { "已复制", "还在呢", "真的复制好了", "放心，复制好了", "千真万确，已复制", "去粘贴吧" };

        static L() { Apply(null); }

        // pref is "en", "zh", or null to follow CCCC_LANG and then the Windows display language.
        public static void Apply(string pref)
        {
            if (pref == "en" || pref == "zh") { Zh = pref == "zh"; return; }
            string forced = Environment.GetEnvironmentVariable("CCCC_LANG");
            if (!string.IsNullOrEmpty(forced)) Zh = forced.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            else Zh = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh";
        }

        static string T(string en, string zh) { return Zh ? zh : en; }

        public static string[] Again { get { return Zh ? AgainZh : AgainEn; } }

        public static string Cut { get { return T("Cut", "已剪切"); } }
        public static string NothingCopied { get { return T("Nothing copied", "没有复制上"); } }
        public static string NothingCut { get { return T("Nothing cut", "没有剪切上"); } }
        public static string StillHolding { get { return T("Still holding: ", "剪贴板仍是："); } }
        public static string MaybeEmpty { get { return T("The selection may be empty", "可能没有选中内容"); } }

        public static string Link { get { return T("Link", "链接"); } }
        public static string Image { get { return T("Image", "图片"); } }
        public static string ImageHold { get { return T("an image ", "图片 "); } }
        public static string File { get { return T("File", "文件"); } }
        public static string Folder { get { return T("Folder", "文件夹"); } }
        public static string Private { get { return T("Private", "已隐藏"); } }
        public static string PrivateSub { get { return T("•••••••• hidden for privacy", "•••••••• 敏感内容不显示"); } }
        public static string Rich { get { return T("Rich content", "其他格式内容"); } }
        public static string AppData { get { return T("App data", "程序专用"); } }
        public static string KeptIn(string app) { return T("Kept inside " + app, "保存在 " + app + " 里"); }
        public static string AppContent(string app) { return T(app + " content", app + " 的内容"); }
        public static string Blank { get { return T("(whitespace)", "（空白字符）"); } }

        public static string Chars(int n) { return Zh ? N(n) + " 字" : N(n) + (n == 1 ? " char" : " chars"); }
        public static string Lines(int n) { return Zh ? N(n) + " 行" : N(n) + " lines"; }
        public static string Items(int n) { return Zh ? N(n) + " 项" : N(n) + " items"; }
        static string N(int n) { return n.ToString("N0", CultureInfo.InvariantCulture); }

        public static string ReadyTitle { get { return T("CCCC is on", "CCCC 已就绪"); } }
        public static string ReadySub { get { return T("Copy anything. I'll confirm it.", "随便复制点什么，我来确认。"); } }
        public static string RunningTitle { get { return T("Already running", "CCCC 已在运行"); } }
        public static string RunningSub { get { return T("CCCC lives in your tray.", "它就在系统托盘里。"); } }
        public static string OnClipboard { get { return T("On your clipboard", "剪贴板里现在是"); } }
        public static string EmptyTitle { get { return T("Clipboard is empty", "剪贴板是空的"); } }
        public static string EmptySub { get { return T("Copy something and it shows up here.", "复制点什么，它就会出现在这里。"); } }
        public static string Caps { get { return T("Caps Lock", "大写锁定"); } }
        public static string CapsWidth { get { return T("Card length", "卡片长度"); } }
        public static string HintCapsWidth { get { return T("How wide the Caps Lock card is", "大写锁定卡片的宽度"); } }
        public static string CapsHeight { get { return T("Card height", "卡片高度"); } }
        public static string HintCapsHeight { get { return T("How tall the Caps Lock card is", "大写锁定卡片的高度"); } }

        // Wi-Fi card. Details are joined with " · " and each shows only when known.
        public static string WifiSwitched(string name) { return name == null ? T("Wi-Fi switched", "Wi-Fi 已切换") : T("Switched to " + name, "已切换到 " + name); }
        public static string WifiFrom(string old) { return T("From " + old, "原为 " + old); }
        public static string WifiNowBand(string name, int band) { return name == null ? T("Wi-Fi now " + Band(band), "Wi-Fi 现在是 " + Band(band)) : T(name + ": now " + Band(band), name + "：已切换到 " + Band(band)); }
        public static string WifiWasBand(int band) { return T("Was " + Band(band), "原为 " + Band(band)); }
        public static string WifiReconnected(string name) { return name == null ? T("Wi-Fi reconnected", "Wi-Fi 已重新连接") : T("Reconnected to " + name, "已重新连接 " + name); }
        public static string WifiOffline(int sec) { return T("Offline for " + sec + " s", "断开了 " + sec + " 秒"); }
        public static string WifiDisconnected { get { return T("Wi-Fi disconnected", "Wi-Fi 已断开"); } }
        public static string WifiWasOn(string name) { return T("Was on " + name, "之前连接的是 " + name); }
        public static string WifiNeedLocation { get { return T("Allow Location to see network names", "允许定位权限以显示网络名称"); } }
        public static string Band(int band) { return band == 2 ? "2.4 GHz" : band == 5 ? "5 GHz" : band == 6 ? "6 GHz" : null; }
        public static string Signal(int pct) { return T("Signal " + pct + "%", "信号 " + pct + "%"); }
        public static string WifiConnected(string name) { return name == null ? T("Wi-Fi connected", "Wi-Fi 已连接") : T("Connected to " + name, "已连接 " + name); }
        public static string Detail(params string[] parts)
        {
            var sb = new StringBuilder();
            foreach (string p in parts)
                if (!string.IsNullOrEmpty(p)) { if (sb.Length > 0) sb.Append(" · "); sb.Append(p); }
            return sb.ToString();
        }
        public static string MenuWifi { get { return T("Show Wi-Fi changes", "Wi-Fi 变化时提醒我"); } }
        public static string HintWifi { get { return T("Show a card next to the Wi-Fi icon when the network changes", "网络变化时在 Wi-Fi 图标旁弹出卡片"); } }
        public static string TabWifi { get { return "Wi-Fi"; } }
        public static string WifiNames { get { return T("Network names", "网络名称"); } }
        public static string HintWifiNames { get { return T("Windows asks for Location to show them", "Windows 需要定位权限才能显示名称"); } }
        public static string WifiAllowed { get { return T("Allowed", "已允许"); } }
        public static string WifiNotAllowed { get { return T("Not allowed", "未允许"); } }
        public static string WifiNoAdapter { get { return T("No Wi-Fi adapter", "没有 Wi-Fi 适配器"); } }
        public static string WifiOpenLocation { get { return T("Open Location settings", "打开定位设置"); } }

        // Sound output card. Details are joined with " · " and each shows only when known.
        public static string AudioTitle(string name) { return T("Sound output: " + name, "声音输出：" + name); }
        public static string AudioNone { get { return T("No sound output", "没有声音输出设备"); } }
        public static string AudioVolume(int n) { return T("Volume " + n + "%", "音量 " + n + "%"); }
        public static string AudioMuted { get { return T("Muted", "已静音"); } }
        public static string AudioWas(string name) { return T("Was " + name, "原为 " + name); }
        public static string MenuAudio { get { return T("Show sound output changes", "声音输出切换时提醒我"); } }
        public static string HintAudio { get { return T("Show a card near the tray when sound switches to another device", "声音切换到其他设备时在托盘附近弹出卡片"); } }
        public static string TabAudio { get { return T("Sound output", "声音输出"); } }

        public static string SettingsFile { get { return T("Settings file", "设置文件"); } }
        public static string HintSettingsFile { get { return T("Export your settings to a file, or import a file from another computer. Pause stays as it is.", "把设置导出为文件，或导入另一台电脑导出的文件。暂停状态保持不变。"); } }
        public static string Export { get { return T("Export…", "导出…"); } }
        public static string Import { get { return T("Import…", "导入…"); } }
        public static string ImportFailed { get { return T("Couldn't import this file.", "无法导入此文件。"); } }
        public static string ExportFailed { get { return T("Couldn't export settings.", "无法导出设置。"); } }

        public static string MenuShow { get { return T("Show clipboard", "查看剪贴板"); } }
        public static string MenuPause { get { return T("Pause", "暂停"); } }
        public static string MenuSound { get { return T("Sound", "提示音"); } }
        public static string MenuStopSounds { get { return T("Stop all sounds", "停止所有声音"); } }
        public static string MenuHint { get { return T("Warn when nothing was copied", "没复制上时提醒我"); } }
        public static string MenuCaps { get { return T("Show Caps Lock changes", "切换大写锁定时提醒我"); } }
        public static string MenuStartup { get { return T("Start with Windows", "开机启动"); } }
        public static string MenuLanguage { get { return T("Language", "语言"); } }
        public static string MenuQuit { get { return T("Quit", "退出"); } }
        public static string MenuCheckUpdate { get { return T("Check for updates…", "检查更新…"); } }
        public static string MenuAutoUpdate { get { return T("Check for updates automatically", "自动检查更新"); } }
        public static string UpdateChecking { get { return T("Checking for updates", "正在检查更新"); } }
        public static string UpdateCheckingSub { get { return T("Asking GitHub…", "正在询问 GitHub…"); } }
        public static string UpToDate { get { return T("You're up to date", "已是最新版本"); } }
        public static string UpToDateSub(string ver) { return "CCCC " + ver; }
        public static string CheckFailed { get { return T("Couldn't check for updates", "检查更新失败"); } }
        public static string UpdatePrompt(string newVer, string curVer, string name)
        {
            string body = string.IsNullOrEmpty(name) ? "" : name + "\n\n";
            return T("CCCC " + newVer + " is available (you have " + curVer + ").\n\n" + body + "Update now? CCCC will restart.",
                     "CCCC " + newVer + " 已发布（当前 " + curVer + "）。\n\n" + body + "现在更新吗？CCCC 将自动重启。");
        }
        public static string Downloading(string ver) { return T("Updating to " + ver, "正在更新到 " + ver); }
        public static string DownloadingSub { get { return T("Downloading from GitHub…", "正在从 GitHub 下载…"); } }
        public static string UpdateFailed(string msg)
        {
            return T("Couldn't update automatically:\n" + msg + "\n\nOpen the download page instead?",
                     "无法自动更新：\n" + msg + "\n\n要打开下载页面吗？");
        }
        public static string UpdatedTitle(string ver) { return T("Updated to " + ver, "已更新到 " + ver); }
        public static string UpdatedSub { get { return T("Thanks for keeping CCCC fresh.", "CCCC 已是最新。"); } }

        public static string MenuSource { get { return T("Show source", "显示来源"); } }
        public static string MenuSourceOn { get { return T("On", "开启"); } }
        public static string SourceWhen { get { return T("When", "何时显示"); } }
        public static string SourceAlways { get { return T("Always", "总是显示"); } }
        public static string SourceAuto { get { return T("Only when not obvious", "仅在不明显时"); } }
        public static string SourceDetail { get { return T("Detail", "详细程度"); } }
        public static string DetailIcon { get { return T("App icon only", "仅应用图标"); } }
        public static string DetailApp { get { return T("App name", "应用名称"); } }
        public static string DetailSite { get { return T("App + website", "应用 + 网站"); } }
        public static string DetailTitle { get { return T("App + website + page/window title", "应用 + 网站 + 页面/窗口标题"); } }
        public static string RemoteDesktop { get { return T("Remote Desktop", "远程桌面"); } }

        public static string MenuCustomize { get { return T("Customize…", "自定义…"); } }
        public static string CustomizeTitle { get { return T("Customize CCCC", "自定义 CCCC"); } }
        public static string TabGeneral { get { return T("General", "常规"); } }
        public static string TabAppearance { get { return T("Appearance", "外观"); } }
        public static string TabBehavior { get { return T("Behavior", "行为"); } }
        public static string TabSound { get { return T("Sound", "声音"); } }
        public static string TabSource { get { return T("Source", "来源"); } }
        public static string TabCaps { get { return T("Caps Lock", "大写锁定"); } }
        public static string TabAbout { get { return T("About", "关于"); } }
        public static string AboutVersion { get { return T("Version", "版本"); } }
        public static string AboutBuild { get { return T("Build", "构建"); } }
        public static string AboutWebsite { get { return T("Website", "网站"); } }
        public static string AboutDeveloper { get { return T("Developer", "开发者"); } }
        public static string AboutContributors { get { return T("Contributors", "贡献者"); } }
        public static string AboutLicense { get { return T("License", "许可证"); } }

        public static string ResetDefaults { get { return T("Reset to defaults", "恢复默认"); } }
        public static string ThemeTitle { get { return T("Theme", "主题"); } }
        public static string ThemeSystem { get { return T("System", "跟随系统"); } }
        public static string ThemeLight { get { return T("Light", "浅色"); } }
        public static string ThemeDark { get { return T("Dark", "深色"); } }
        public static string PaletteTitle { get { return T("Palette", "配色"); } }
        public static string PaletteDefault { get { return T("Default", "默认"); } }
        public static string PaletteGraphite { get { return T("Graphite", "石墨"); } }
        public static string PaletteContrast { get { return T("High contrast", "高对比"); } }
        public static string PalettePastel { get { return T("Pastel", "柔和"); } }
        public static string PaletteGlass { get { return T("Liquid Glass", "液态玻璃"); } }
        public static string OkColor { get { return T("Success colour", "成功颜色"); } }
        public static string WarnColor { get { return T("Warning colour", "警告颜色"); } }
        public static string ResetColor { get { return T("Reset", "重置"); } }
        public static string FullColors { get { return T("Customize all colours", "自定义全部颜色"); } }
        public static string ColorBg { get { return T("Background", "背景"); } }
        public static string ColorBorder { get { return T("Border", "边框"); } }
        public static string ColorTitle { get { return T("Title text", "标题文字"); } }
        public static string ColorSub { get { return T("Body text", "正文文字"); } }
        public static string ColorMeta { get { return T("Details text", "细节文字"); } }
        public static string CardSize { get { return T("Card size", "卡片大小"); } }
        public static string FontTitle { get { return T("Font", "字体"); } }
        public static string FontDefault { get { return T("Default", "默认"); } }
        public static string TextSize { get { return T("Text size", "文字大小"); } }
        public static string SimpleMode { get { return T("Simple mode", "简易模式"); } }
        public static string HintSimple { get { return T("Only the status icon and title on a compact one-line card", "紧凑的单行卡片，只显示状态图标和标题"); } }
        public static string Transparency { get { return T("Transparency", "透明度"); } }
        public static string HintTransparency { get { return T("Card opacity; 0% is fully opaque", "卡片不透明度，0% 为完全不透明"); } }
        public static string PositionTitle { get { return T("Position", "位置"); } }
        public static string PosNear { get { return T("Near the mouse pointer", "鼠标附近"); } }
        public static string PosFree { get { return T("Freedom", "自由"); } }
        public static string PosSame { get { return T("Same as copied card", "与复制卡片相同"); } }
        public static string HintFree { get { return T("Drag the card in the preview, or drag the card itself on screen", "拖动预览中的卡片，或直接拖动屏幕上的卡片"); } }
        public static string DurationTitle { get { return T("Show for", "停留时间"); } }
        public static string StayUntilClicked { get { return T("Stay until clicked", "直到点击才关闭"); } }
        public static string PlaySounds { get { return T("Play sounds", "播放提示音"); } }
        public static string CopiedSound { get { return T("Copied sound", "复制成功音"); } }
        public static string FailSound { get { return T("Nothing copied sound", "未复制成功音"); } }
        public static string SoundBuiltIn { get { return T("Built-in", "内置"); } }
        public static string SoundCustom { get { return T("Custom file", "自定义文件"); } }
        public static string Browse { get { return T("Browse…", "浏览…"); } }
        public static string Test { get { return T("Test", "试听"); } }
        public static string LangSystem { get { return T("System", "跟随系统"); } }
        public static string AudioFilter { get { return "Audio (*.wav;*.mp3)|*.wav;*.mp3"; } }
        public static string ResetArmed { get { return T("Click again to reset", "再点一次以重置"); } }
        public static string ToggleOn { get { return T("On", "开"); } }
        public static string ToggleOff { get { return T("Off", "关"); } }
        public static string SoundBuiltInHint { get { return T("Built-in sound", "内置提示音"); } }
        public static string SoundNoFile { get { return T("No file chosen", "未选择文件"); } }
        public static string ColoursTitle { get { return T("Colours", "颜色"); } }
        public static string SizeTitle { get { return T("Size", "大小"); } }
        public static string HintPause { get { return T("Stop noticing copies for now", "暂时不监听复制"); } }
        public static string HintWarn { get { return T("Warn when Ctrl+C copies nothing", "Ctrl+C 没复制上时提醒"); } }
        public static string IgnoreApps { get { return T("Ignored apps", "忽略的应用"); } }
        public static string HintIgnore { get { return T("No card, sound or warning when copying in these apps", "在这些应用里复制时不弹卡片、不响、不提醒"); } }
        public static string AddApp { get { return T("Add app…", "添加应用…"); } }
        public static string Remove { get { return T("Remove", "移除"); } }
        public static string ProgramFilter { get { return T("Programs (*.exe)|*.exe", "程序 (*.exe)|*.exe"); } }
        public static string HintCaps { get { return T("Show a card when Caps Lock turns on or off", "大写锁定开启或关闭时弹出卡片"); } }
        public static string HintStartup { get { return T("Run CCCC when you sign in", "登录 Windows 时自动运行"); } }
        public static string HintAutoUpdate { get { return T("Check GitHub for new releases", "在 GitHub 上检查新版本"); } }
        public static string HintLanguage { get { return T("Follow Windows, or pick a language", "跟随系统，或选择语言"); } }
        public static string HintTheme { get { return T("Follow Windows, or pick light or dark", "跟随系统，或选择浅色或深色"); } }
        public static string HintOkColor { get { return T("The colour of a successful copy", "复制成功时的颜色"); } }
        public static string HintWarnColor { get { return T("The colour of a failed copy", "未复制成功时的颜色"); } }
        public static string HintFullColors { get { return T("Override the card's background, borders and text", "自定义卡片的背景、边框和文字"); } }
        public static string HintCardSize { get { return T("Scale the whole card", "整体缩放卡片"); } }
        public static string HintFont { get { return T("Font for the text on the card", "卡片文字使用的字体"); } }
        public static string HintTextSize { get { return T("Scale only the text", "只缩放文字"); } }
        public static string HintPosition { get { return T("Where the card appears on screen", "卡片在屏幕上出现的位置"); } }
        public static string HintDuration { get { return T("How long the card stays", "卡片停留的时间"); } }
        public static string HintStay { get { return T("Keep the card until you click it", "卡片一直显示，直到点击"); } }
        public static string HintSound { get { return T("A short sound for copies and misses", "复制成功或失败时播放短音"); } }
        public static string HintSource { get { return T("Show which app the copy came from", "显示复制内容来自哪个应用"); } }
        public static string HintWhen { get { return T("When to show the source", "何时显示来源"); } }
        public static string HintDetailIcon { get { return T("App icon only", "只显示应用图标"); } }
        public static string HintDetailApp { get { return T("e.g. Google Chrome", "例如 Google Chrome"); } }
        public static string HintDetailSite { get { return T("e.g. Google Chrome · github.com", "例如 Google Chrome · github.com"); } }
        public static string HintDetailTitle { get { return T("Also the page or window title", "还包括页面或窗口标题"); } }

        public static string Tip { get { return T("CCCC · copy with confidence", "CCCC · 放心复制"); } }
        public static string TipPaused { get { return T("CCCC · paused", "CCCC · 已暂停"); } }
    }
}
