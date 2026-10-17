using System;
using System.Globalization;

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

        public static string MenuShow { get { return T("Show clipboard", "查看剪贴板"); } }
        public static string MenuPause { get { return T("Pause", "暂停"); } }
        public static string MenuSound { get { return T("Sound", "提示音"); } }
        public static string MenuHint { get { return T("Warn when nothing was copied", "没复制上时提醒我"); } }
        public static string MenuStartup { get { return T("Start with Windows", "开机启动"); } }
        public static string MenuLanguage { get { return T("Language", "语言"); } }
        public static string MenuQuit { get { return T("Quit", "退出"); } }
        public static string Tip { get { return T("CCCC · copy with confidence", "CCCC · 放心复制"); } }
        public static string TipPaused { get { return T("CCCC · paused", "CCCC · 已暂停"); } }
    }
}
