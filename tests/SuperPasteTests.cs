using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
namespace Cccc {
    static class SuperPasteTests {
        static int checks;
        static void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
        static void Equal(string expected, string actual, string name) { Check(expected == actual, name + " expected=" + Escape(expected) + " actual=" + Escape(actual)); }
        static string Escape(string s) { return s.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t"); }
        static PasteRules Plain() { return new PasteRules { Markdown = false, Symbols = false, EndNewlines = false }; }
        static PasteRules All(bool markdown) { return new PasteRules { Markdown = markdown, Symbols = true, Tabs = true, Leading = true, Trailing = true, CjkSpaces = true, Spaces = true, Newlines = true, BlankLines = true, EndNewlines = true }; }
        static string Clean(string text, PasteRules r) { return SuperPasteText.Clean(text, r).Text; }
        [STAThread] static void Main(string[] args) {
            Settings.Key = @"Software\CCCC-superpaste-tests-" + Guid.NewGuid().ToString("N");
            try { TextTests(); TransactionTests(); HotkeyTests(); SettingsTests(); UiTests(args[0]); InputTests(); Console.WriteLine("PASS: " + checks + " Super Paste checks"); }
            catch(Exception e) { Console.WriteLine("FAIL: " + e); Environment.ExitCode=1; }
            finally { Registry.CurrentUser.DeleteSubKeyTree(Settings.Key,false); }
        }
        static void TextTests() {
            var r=Plain(); r.Symbols=true;
            Equal("\v1234567■8●9•①\uE000",Clean("\v1\uF06C2\uF06E3\uF0754\uF0FC5\uF0D86\uF0B27■8●9•①\uE000",r),"only explicit six symbols");
            r=Plain();r.Tabs=true; Equal("a   b",Clean("a\tb",r),"four-column tabs");
            r=Plain();r.Leading=true;Equal("a\n b".Replace(" b","b"),Clean("  a\n\u3000 b",r),"leading spaces");
            r=Plain();r.Trailing=true;Equal("a\nb",Clean("a  \nb\u3000",r),"trailing spaces");
            r=Plain();r.CjkSpaces=true;Equal("中文ABC  def中文  字",Clean("中文  ABC  def 中文  字",r),"CJK ASCII boundary only");
            r=Plain();r.Spaces=true;Equal("a b",Clean("a   b",r),"collapse spaces");
            r=Plain();r.Newlines=true;Equal("a\nb",Clean("a\n\n\nb",r),"collapse newlines");
            r.BlankLines=true;Equal("a\n\nb",Clean("a\n\n\nb",r),"collapse then insert blank");
            Equal("a\r\n\r\nb",Clean("a\r\nb",r),"retain CRLF");
            Equal("a\r\rb",Clean("a\rb",r),"retain CR");
            r=Plain();r.EndNewlines=true;Equal("a",Clean("a\r\n\r\n",r),"trim final newlines");
            var all=All(true);
            foreach(string sample in new[] {
                "```csharp\r\n  中文  ABC\t\uF06C\r\n```\r\n", "~~~\n  a\t b\n~~~\n",
                "    a  b\t\n", "`中文  ABC`  x\n", "- parent\n  - child  \n    continued\n",
                "> quote  text\n", "| a | b |\n| --- | --- |\n| x  y | z |\n",
                "[中文  ABC](https://example/a_b)\n", "<div>\n  中文  ABC\n</div>\n", "a\\  b\n",
                "a\tb\nc\td\n", "a\t\tb\nc\t\td\n" }) {
                // Protected complete blocks include original final line terminators.
                string expected=sample;
                if(sample.StartsWith("[中文") || sample.StartsWith("a\\")) expected=sample.TrimEnd('\n');
                Equal(expected,Clean(sample,all),"Markdown/table protected: "+Escape(sample));
            }
            Equal("a  \nb",Clean("a  \nb",all),"Markdown hard break preserved; no added blank");
            Equal("a\n\nb",Clean("a\n\n\n\nb",all),"Markdown compress blank runs");
            Equal("before `code\n  中文  ABC\nend`\n",Clean("before `code\n  中文  ABC\nend`\n",all),"multiline code spans stay byte-identical");
            Equal("\uF06C# heading",Clean("\uF06C# heading",all),"symbol removal must not create Markdown syntax");
            Equal("a   b\nc       d",Clean("a\tb\nc\t\td",new PasteRules { Markdown=false, Symbols=false, EndNewlines=false, Tabs=true }),"inconsistent TSV is not a table");
            foreach (bool markdown in new[]{false,true}) {
                var options=All(markdown);
                foreach(string sample in new[]{"  中文  ABC  \n\n\n def  \n", "a\tb\nc\td\n", "```\nx   y\n```\n", "a\nb", "\n\n", ""}) {
                    string once=Clean(sample,options);Equal(once,Clean(once,options),"idempotent");
                }
            }
        }
        sealed class Desktop : IPasteDesktop {
            public IntPtr Focus=new IntPtr(42); public uint Seq=1; public bool Released=true, Read=true, Write=true, Send=true;
            public string Input="\uF06Chello\n", Reason; public int Writes,Sends; public string Output;
            public IntPtr Foreground {get{return Focus;}} public uint Sequence {get{return Seq;}} public bool KeysReleased {get{return Released;}}
            public bool TryRead(out string text,out string reason){text=Read?Input:null;reason=Reason;return Read;}
            public bool TryWrite(string text,uint expected,out uint written){written=Seq;if(!Write||Seq!=expected)return false;Output=text;Writes++;written=++Seq;return true;}
            public bool SendPaste(){Sends++;return Send;}
        }
        static void TransactionTests() {
            double time=0;var d=new Desktop();var notices=new List<string>();uint own=0;
            using(var p=new SuperPaste(d,()=>time,s=>notices.Add(s),seq=>own=seq)) {
                p.Begin(new PasteRules());p.Begin(new PasteRules());p.Step();Equal("hello",d.Output,"clean result written");Check(d.Writes==1&&d.Sends==1&&own==2&&!p.Busy,"single paste and own revision");
                p.Begin(new PasteRules());d.Focus=new IntPtr(99);p.Step();Check(d.Writes==1&&!p.Busy,"focus changed cancels");
                p.Begin(new PasteRules());d.Seq++;p.Step();Check(d.Writes==1&&!p.Busy,"new clipboard cancels");
                d.Released=false;p.Begin(new PasteRules());p.Step();Check(d.Writes==1&&p.Busy,"wait for modifiers");time+=2;p.Step();Check(!p.Busy&&d.Writes==1,"modifier timeout");
                d.Released=true;d.Read=false;d.Reason=null;p.Begin(new PasteRules());p.Step();time+=1;p.Step();Check(!p.Busy&&d.Writes==1,"bounded clipboard retry");
                d.Reason="private";p.Begin(new PasteRules());p.Step();Check(!p.Busy&&d.Writes==1,"private content rejected");
                d.Read=true;d.Reason=null;d.Input="\uF06C\n";p.Begin(new PasteRules());p.Step();Check(!p.Busy&&d.Writes==1,"empty output never written");
                d.Input="new";d.Send=false;p.Begin(new PasteRules());p.Step();Check(d.Output=="new"&&d.Sends==2&&notices[notices.Count-1]==L.SPManualPaste,"send failure leaves cleaned text");
                d.Write=false;p.Begin(new PasteRules());p.Step();time+=1;p.Step();Check(!p.Busy&&d.Sends==2,"write contention never sends");
                p.Begin(new PasteRules());p.Cancel();p.Step();Check(d.Sends==2,"pause cancellation");
            }
        }
        static void HotkeyTests() {
            using(var first=new PasteHotkey()) using(var second=new PasteHotkey()) {
                Check(!second.Configure(true,2,0x56)&&!second.Active,"ordinary Ctrl+V cannot be overridden");
                // Use uncommon test shortcuts; never send input during registration checks.
                Check(first.Configure(true,7,0x7A),"register test hotkey");
                Check(!second.Configure(true,7,0x7A)&&!second.Active,"conflict detected");
                Check(second.Configure(true,7,0x79),"register fallback");
                Check(!second.Configure(true,7,0x7A)&&second.Active,"failed replacement keeps prior hotkey");
                Check(second.ActiveKey==0x79,"failed replacement keeps trigger key identity");
                Check(second.Configure(true,7,0x79),"fallback remains available");
                first.Configure(false,7,0x7A);Check(second.Configure(true,7,0x7A),"replacement after release");
                Check(second.Configure(true,7,0x78)&&second.Configure(true,7,0x79),"multiple replacements release correct IDs");
                second.Configure(false,7,0x79);Check(!second.Active,"pause releases binding");
            }
        }
        static void SettingsTests(){
            var s=Settings.Load();Check(!s.SuperPasteEnabled&&s.SuperPasteMarkdown&&s.SuperPasteOptions==257,"safe defaults");
            s.SuperPasteEnabled=true;s.SuperPasteMarkdown=false;s.SuperPasteOptions=511;s.SuperPasteKey=0x71;s.SuperPasteModifiers=6;s.Save();
            var loaded=Settings.Load();Check(loaded.SuperPasteEnabled&&!loaded.SuperPasteMarkdown&&loaded.SuperPasteOptions==511,"persist rules");
            var imported=Settings.Import(s.ToJson());Check(imported.SuperPasteKey==0x71&&imported.SuperPasteModifiers==6&&imported.SuperPasteOptions==511,"JSON settings round trip");
        }
        static void UiTests(string dir) {
            L.Apply("zh");var s=new Settings();
            var ui=new Customize(s,()=>{},b=>{},()=>{},()=>{},()=>{},b=>{},new WifiWatch());
            try {
                ui.SelectPage(8);var root=ui.Root;root.Measure(new System.Windows.Size(980,900));root.Arrange(new System.Windows.Rect(0,0,980,900));root.UpdateLayout();
                var image=new System.Windows.Media.Imaging.RenderTargetBitmap(980,900,96,96,System.Windows.Media.PixelFormats.Pbgra32);
                var bg=new System.Windows.Media.DrawingVisual();using(var g=bg.RenderOpen())g.DrawRectangle((System.Windows.Media.Brush)root.FindResource("WinBg"),null,new System.Windows.Rect(0,0,980,900));image.Render(bg);image.Render(root);
                var enc=new System.Windows.Media.Imaging.PngBitmapEncoder();enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));using(var f=System.IO.File.Create(System.IO.Path.Combine(dir,"super-paste-zh.png")))enc.Save(f);
                Check(ui.TitleText==L.SuperPasteTitle,"settings navigation");
                var binding=BindingFlags.Instance|BindingFlags.NonPublic;
                var checksUi=(System.Windows.Controls.CheckBox[])typeof(Customize).GetField("spRules",binding).GetValue(ui);
                var mode=(System.Windows.Controls.ComboBox)typeof(Customize).GetField("spMode",binding).GetValue(ui);
                Check(!checksUi[7].IsEnabled,"Markdown disables blank-line insertion");
                mode.SelectedIndex=1;Check(!s.SuperPasteMarkdown&&checksUi[7].IsEnabled,"mode switch enables independent rule");
                checksUi[7].IsChecked=true;mode.SelectedIndex=0;Check((s.SuperPasteOptions&128)!=0&&!checksUi[7].IsEnabled,"disabled rule retains plain mode preference");
                var input=(System.Windows.Controls.TextBox)typeof(Customize).GetField("spInput",binding).GetValue(ui);
                var output=(System.Windows.Controls.TextBox)typeof(Customize).GetField("spOutput",binding).GetValue(ui);
                input.Text="\uF06Cexample\n";Equal("example",output.Text,"live sample preview");
                s.Lang="en";L.Apply("en");ui.Reload();ui.SelectPage(8);
                root.Measure(new System.Windows.Size(980,900));root.Arrange(new System.Windows.Rect(0,0,980,900));root.UpdateLayout();
                image=new System.Windows.Media.Imaging.RenderTargetBitmap(980,900,96,96,System.Windows.Media.PixelFormats.Pbgra32);image.Render(bg);image.Render(root);
                enc=new System.Windows.Media.Imaging.PngBitmapEncoder();enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));using(var f=System.IO.File.Create(System.IO.Path.Combine(dir,"super-paste-en.png")))enc.Save(f);
                Check(ui.TitleText=="Super Paste","English page rebuild");
            }finally{ui.Close();}
        }
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a, uint b, bool attach);
        sealed class Receiver : TextBox {
            public int Pastes; public string Payload;
            protected override void WndProc(ref Message message) {
                if(message.Msg==0x302) { Pastes++; Text+=Payload; return; }
                base.WndProc(ref message);
            }
        }
        sealed class InputDesktop : IPasteDesktop {
            public uint Seq=1; public string Output; public readonly WindowsPasteDesktop NativeDesktop;
            public InputDesktop(IntPtr owner){NativeDesktop=new WindowsPasteDesktop(owner);}
            public IntPtr Foreground {get{return Native.GetForegroundWindow();}} public uint Sequence {get{return Seq;}}
            public bool KeysReleased {get{return NativeDesktop.KeysReleased;}}
            public bool TryRead(out string text,out string reason){text="\uF06Chello\n";reason=null;return true;}
            public bool TryWrite(string text,uint expected,out uint written){Output=text;written=++Seq;return true;}
            public bool SendPaste(){return NativeDesktop.SendPaste();}
        }
        static void InputTests() {
            // Real SendInput/WM_PASTE routing, with an in-memory test clipboard. The user's clipboard is never read or changed.
            IntPtr previous=Native.GetForegroundWindow();
            using(var form=new Form {Text="CCCC Super Paste input test",Width=420,Height=120})
            using(var box=new Receiver {Dock=DockStyle.Fill}) {
                form.Controls.Add(box); form.Show();
                uint pid; uint foregroundThread=Native.GetWindowThreadProcessId(previous,out pid);
                uint ownThread=Native.GetCurrentThreadId();
                bool attached=foregroundThread!=ownThread && AttachThreadInput(ownThread,foregroundThread,true);
                try { SetForegroundWindow(form.Handle);form.Activate();box.Focus(); }
                finally { if(attached)AttachThreadInput(ownThread,foregroundThread,false); }
                var watch=System.Diagnostics.Stopwatch.StartNew();
                while(watch.ElapsedMilliseconds<150){Application.DoEvents();System.Threading.Thread.Sleep(5);}
                Check(Native.GetForegroundWindow()==form.Handle,"test receiver has focus");
                var desktop=new InputDesktop(form.Handle);string failure=null;
                using(var paste=new SuperPaste(desktop,()=>watch.Elapsed.TotalSeconds,s=>failure=s,seq=>box.Payload=desktop.Output)) {
                    paste.Begin(new PasteRules());paste.Step();
                    long deadline=watch.ElapsedMilliseconds+500;
                    while(watch.ElapsedMilliseconds<deadline){Application.DoEvents();System.Threading.Thread.Sleep(5);}
                    Check(failure==null&&box.Pastes==1&&box.Text=="hello","SendInput delivers exactly one paste of cleaned text");
                }
                form.Close();
            }
            if(previous!=IntPtr.Zero)SetForegroundWindow(previous);
        }
    }
}
