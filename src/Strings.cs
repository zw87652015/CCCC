using System;
using System.Globalization;

namespace Cccc
{
    static class L
    {
        public static readonly bool Zh = Detect();

        static bool Detect()
        {
            string forced = Environment.GetEnvironmentVariable("CCCC_LANG");
            if (!string.IsNullOrEmpty(forced)) return forced.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh";
        }

        static string T(string en, string zh) { return Zh ? zh : en; }

        public static readonly string[] Again = Zh
            ? new[] { "已复制", "还在呢", "真的复制好了", "放心，复制好了", "千真万确，已复制", "去粘贴吧" }
            : new[] { "Copied", "Still copied", "Yes, it's copied", "Definitely copied", "Really. It's copied.", "It's there. Go paste." };

        public static readonly string Cut = T("Cut", "已剪切");
        public static readonly string NothingCopied = T("Nothing copied", "没有复制上");
        public static readonly string NothingCut = T("Nothing cut", "没有剪切上");
        public static readonly string StillHolding = T("Still holding: ", "剪贴板仍是：");
        public static readonly string MaybeEmpty = T("The selection may be empty", "可能没有选中内容");

        public static readonly string Link = T("Link", "链接");
        public static readonly string Image = T("Image", "图片");
        public static readonly string ImageHold = T("an image ", "图片 ");
        public static readonly string File = T("File", "文件");
        public static readonly string Folder = T("Folder", "文件夹");
        public static readonly string Private = T("Private", "已隐藏");
        public static readonly string PrivateSub = T("•••••••• hidden for privacy", "•••••••• 敏感内容不显示");
        public static readonly string Rich = T("Rich content", "其他格式内容");
        public static readonly string Blank = T("(whitespace)", "（空白字符）");

        public static string Chars(int n) { return Zh ? N(n) + " 字" : N(n) + (n == 1 ? " char" : " chars"); }
        public static string Lines(int n) { return Zh ? N(n) + " 行" : N(n) + " lines"; }
        public static string Items(int n) { return Zh ? N(n) + " 项" : N(n) + " items"; }
        static string N(int n) { return n.ToString("N0", CultureInfo.InvariantCulture); }

        public static readonly string ReadyTitle = T("CCCC is on", "CCCC 已就绪");
        public static readonly string ReadySub = T("Copy anything. I'll confirm it.", "随便复制点什么，我来确认。");
        public static readonly string RunningTitle = T("Already running", "CCCC 已在运行");
        public static readonly string RunningSub = T("CCCC lives in your tray.", "它就在系统托盘里。");
        public static readonly string OnClipboard = T("On your clipboard", "剪贴板里现在是");
        public static readonly string EmptyTitle = T("Clipboard is empty", "剪贴板是空的");
        public static readonly string EmptySub = T("Copy something and it shows up here.", "复制点什么，它就会出现在这里。");

        public static readonly string MenuShow = T("Show clipboard", "查看剪贴板");
        public static readonly string MenuPause = T("Pause", "暂停");
        public static readonly string MenuSound = T("Sound", "提示音");
        public static readonly string MenuHint = T("Warn when nothing was copied", "没复制上时提醒我");
        public static readonly string MenuStartup = T("Start with Windows", "开机启动");
        public static readonly string MenuQuit = T("Quit", "退出");
        public static readonly string Tip = T("CCCC · copy with confidence", "CCCC · 放心复制");
        public static readonly string TipPaused = T("CCCC · paused", "CCCC · 已暂停");
    }
}
