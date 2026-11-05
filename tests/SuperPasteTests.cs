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
            try { TextTests(); TransactionTests(); HotkeyTests(); SettingsTests(); InputTests(); UiTests(args[0]); Console.WriteLine("PASS: " + checks + " Super Paste checks"); }
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
            foreach(string sample in new[] {
                "<pre>\n\n  中文  ABC\t\uF06C\n\n</pre>\n", "<!-- comment\n\n  中文  ABC\n-->\n",
                "<script>`code`\n\n  var  a = 1;\n</script>\n", "<?processing\n\n  text  here\n?>\n",
                "<![CDATA[\n\n  中文  ABC\n]]>\n", "---\ntitle: example\n\n  value  with spaces\n---\n",
                "- parent\n\n  paragraph  two\n\n  - nested\n", "> quote\nlazy  continuation\n",
                "    code\n\n\n    more  code\n",
                "[label](\n  https://example.test/a\n  \"title  here\"\n)\n",
                "[multi\n  line  label](target)\n", "[ref]:\n  https://example.test\n  \"title  here\"\n" }) {
                Equal(sample,Clean(sample,all),"additional protected Markdown block");
                Equal(sample,Clean(Clean(sample,all),all),"protected Markdown stable on repeated runs");
            }
            Equal("- item\n\nnormal text",Clean("- item\n\nnormal  text",all),"plain paragraph resumes after list");
            var performance=System.Diagnostics.Stopwatch.StartNew();
            string nearDivider="---"+new string(' ',20000)+"x";
            Equal("--- x",Clean(nearDivider,new PasteRules {Markdown=true,Symbols=false,EndNewlines=false,Spaces=true}),"near divider bounded cleanup");
            var manyLines=new System.Text.StringBuilder();for(int i=0;i<30000;i++)manyLines.Append("line\n");
            var blankRule=Plain();blankRule.BlankLines=true;
            string spaced=Clean(manyLines.ToString(),blankRule);
            Check(spaced.Length==manyLines.Length+29999,"large input blank-line insertion");
            Equal(spaced,Clean(spaced,blankRule),"large input newline idempotence");
            Check(performance.ElapsedMilliseconds<10000,"large input completes within budget");
            Check(WindowsPasteDesktop.SupportsText(f=>f==13||f==8),"Unicode text accepted alongside bitmap preview");
            Check(!WindowsPasteDesktop.SupportsText(f=>f==8),"textless image rejected");
            Check(!WindowsPasteDesktop.SupportsText(f=>f==13||f==15),"file list rejected even with text representation");
            foreach (bool markdown in new[]{false,true}) {
                var options=All(markdown);
                foreach(string sample in new[]{"  中文  ABC  \n\n\n def  \n", "a\tb\nc\td\n", "```\nx   y\n```\n", "a\nb", "\n\n", ""}) {
                    string once=Clean(sample,options);Equal(once,Clean(once,options),"idempotent");
                }
            }
        }
        sealed class Desktop : IPasteDesktop {
            public IntPtr Focus=new IntPtr(42); public uint Seq=1; public bool Released=true, Read=true, Write=true, Send=true;
            public bool ThrowSend, ReplaceAfterWrite;
            public string Input="\uF06Chello\n", Reason; public int Writes,Sends; public string Output;
            public IntPtr Foreground {get{return Focus;}} public uint Sequence {get{return Seq;}} public bool KeysReleased {get{return Released;}}
            public bool TryRead(out string text,out string reason){text=Read?Input:null;reason=Reason;return Read;}
            public bool TryWrite(string text,uint expected,out uint written){written=Seq;if(!Write||Seq!=expected)return false;Output=text;Writes++;written=++Seq;if(ReplaceAfterWrite)Seq++;return true;}
            public bool SendPaste(){Sends++;if(ThrowSend)throw new InvalidOperationException();return Send;}
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
                d.Write=true;d.ThrowSend=true;p.Begin(new PasteRules());p.Step();Check(notices[notices.Count-1]==L.SPManualPaste&&d.Output=="new","send exception preserves manual-paste guidance");
                d.ThrowSend=false;d.ReplaceAfterWrite=true;int beforeSend=d.Sends;p.Begin(new PasteRules());p.Step();Check(d.Sends==beforeSend&&notices[notices.Count-1]==L.SPChanged,"clipboard changed after write never claims cleaned result remains");
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
                Check(second.Configure(true,new[]{"7:120","7:121","7:120"}) && second.RegisteredShortcuts.Length==2,"multiple hotkeys deduplicated");
                Check(!first.Configure(true,7,120)&&!first.Configure(true,7,121),"both shortcuts really registered");
                var registrations=(Dictionary<string,int>)typeof(PasteHotkey).GetField("registrations",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(second);
                var received=new List<int>();second.Pressed+=key=>received.Add(key);
                var proc=typeof(PasteHotkey).GetMethod("WndProc",BindingFlags.Instance|BindingFlags.NonPublic);
                foreach(var pair in registrations) {
                    int mods,key;PasteHotkey.Parse(pair.Key,out mods,out key);
                    var msg=Message.Create(second.Handle,0x312,new IntPtr(pair.Value),new IntPtr((key<<16)|mods));
                    proc.Invoke(second,new object[]{msg});
                }
                Check(received.Count==2&&received.Contains(120)&&received.Contains(121),"each hotkey reports its actual trigger key");
                Check(first.Configure(true,7,122),"occupy candidate shortcut");
                Check(!second.Configure(true,new[]{"7:119","7:122"}),"multi-key change fails atomically");
                Check(second.RegisteredShortcuts.Length==2&&!first.Configure(true,7,120)&&!first.Configure(true,7,121),"failed set preserves both old shortcuts");
                Check(first.Configure(true,7,119),"staged shortcut released after conflict");
                Check(second.Configure(true,new[]{"7:121"})&&first.Configure(true,7,120),"removing shortcut releases registration");
                Check(!second.Configure(true,new[]{"2:86"})&&second.Active,"invalid set keeps working registration");
                first.Configure(false,0,0);second.Configure(false,0,0);
                Check(second.Configure(true,12,45),"register Shift Windows Insert");
                Check(!first.Configure(true,12,45),"Windows shortcut conflict detected");
                int winEvents=0;second.Pressed+=key=>{if(key==45)winEvents++;};
                var winMsg=Message.Create(second.Handle,0x312,new IntPtr(registrations["12:45"]),new IntPtr((45<<16)|12));
                proc.Invoke(second,new object[]{winMsg});
                Check(winEvents==1,"Windows modifier message dispatched");
                int parsedMods,parsedKey;
                Check(!PasteHotkey.Parse("16:45",out parsedMods,out parsedKey),"unknown modifier bits rejected");
                Check(PasteHotkey.ValidKey(45)&&PasteHotkey.Display("12:45")=="Shift+Windows+Insert","Insert supported and displayed");
                second.Configure(false,new[]{"7:121"});Check(first.Configure(true,7,121),"disable releases entire set");
            }
        }
        static void SettingsTests(){
            var s=Settings.Load();Check(s.SuperPasteShortcuts.Count==2&&s.SuperPasteShortcuts[0]=="7:86"&&s.SuperPasteShortcuts[1]=="12:45","two default shortcuts");Check(!s.SuperPasteEnabled&&s.SuperPasteMarkdown&&s.SuperPasteOptions==257,"safe defaults");
            s.SuperPasteEnabled=true;s.SuperPasteMarkdown=false;s.SuperPasteOptions=511;s.SuperPasteKey=0x71;s.SuperPasteModifiers=6;s.SuperPasteShortcuts=new List<string>{"6:113","12:45","7:119"};s.Save();
            var loaded=Settings.Load();Check(loaded.SuperPasteEnabled&&!loaded.SuperPasteMarkdown&&loaded.SuperPasteOptions==511,"persist rules");
            var imported=Settings.Import(s.ToJson());Check(imported.SuperPasteKey==0x71&&imported.SuperPasteModifiers==6&&imported.SuperPasteOptions==511,"JSON settings round trip");
            Check(imported.SuperPasteShortcuts.Count==3&&imported.SuperPasteShortcuts[2]=="7:119","multi-shortcut JSON round trip");
            var old=Settings.Import("{\"format\":\"cccc-settings\",\"version\":1,\"settings\":{\"SuperPasteKey\":113,\"SuperPasteModifiers\":6}}");
            Check(old.SuperPasteShortcuts.Count==2&&old.SuperPasteShortcuts[0]=="6:113"&&old.SuperPasteShortcuts[1]=="12:45","old JSON migrates custom shortcut and adds Insert");
            using(var k=Registry.CurrentUser.OpenSubKey(Settings.Key,true)) {
                k.SetValue("SuperPasteShortcuts",new[]{"12:45","12:45","invalid","2:86"},RegistryValueKind.MultiString);
            }
            Check(Settings.Load().SuperPasteShortcuts.Count==1,"invalid and duplicate imported shortcuts removed");
            old.SuperPasteShortcuts=new List<string>{"7:86"};old.Save();
            Check(Settings.Load().SuperPasteShortcuts.Count==1,"explicitly removed default stays removed");
        }
        static void UiTests(string dir) {
            Check(Customize.PasteModifierMask(System.Windows.Input.ModifierKeys.Shift,true)==12,"capture supplements missing Windows modifier");
            Check(Customize.PasteModifierMask(System.Windows.Input.ModifierKeys.Shift|System.Windows.Input.ModifierKeys.Windows,false)==12,"capture retains WPF Windows modifier");
            Check(Customize.PasteModifierMask(System.Windows.Input.ModifierKeys.Shift,false)==4,"capture does not invent Windows modifier");
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
                var shortcutsUi=(System.Windows.Controls.ListBox)typeof(Customize).GetField("spShortcuts",binding).GetValue(ui);
                var keyUi=(System.Windows.Controls.TextBox)typeof(Customize).GetField("spKey",binding).GetValue(ui);
                var removeUi=(System.Windows.Controls.Button)typeof(Customize).GetField("spRemove",binding).GetValue(ui);
                Check(shortcutsUi.Items.Count==2,"UI lists both defaults");
                shortcutsUi.SelectedIndex=1;Check(keyUi.Text=="Shift+Windows+Insert","UI selects Insert binding for editing");
                removeUi.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                Check(s.SuperPasteShortcuts.Count==1&&shortcutsUi.Items.Count==1&&!removeUi.IsEnabled,"UI removes shortcut and keeps at least one");
                var addUi=(System.Windows.Controls.Button)typeof(Customize).GetField("spAdd",binding).GetValue(ui);
                var saveKeyUi=(System.Windows.Controls.Button)typeof(Customize).GetField("spSaveKey",binding).GetValue(ui);
                var modsUi=(System.Windows.Controls.CheckBox[])typeof(Customize).GetField("spModifiers",binding).GetValue(ui);
                var mainKeyUi=(System.Windows.Controls.ComboBox)typeof(Customize).GetField("spMainKey",binding).GetValue(ui);
                var statusUi=(System.Windows.Controls.TextBlock)typeof(Customize).GetField("spStatus",binding).GetValue(ui);
                addUi.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                for(int i=0;i<4;i++)modsUi[i].IsChecked=(i==1||i==3);
                mainKeyUi.SelectedValue=45;
                saveKeyUi.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                Check(s.SuperPasteShortcuts.Count==2&&s.SuperPasteShortcuts[1]=="12:45"&&keyUi.Text=="Shift+Windows+Insert","manual editor adds Windows Insert without physical keys");
                addUi.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                saveKeyUi.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                Check(s.SuperPasteShortcuts.Count==2&&statusUi.Text.Contains(L.SPShortcutExists),"duplicate shortcut clearly reported");
                mainKeyUi.SelectedValue=0x7A;
                saveKeyUi.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                Check(s.SuperPasteShortcuts.Count==2&&s.SuperPasteShortcuts[1]=="12:122","manual editor updates selected shortcut");
                for(int i=0;i<4;i++)modsUi[i].IsChecked=false;
                saveKeyUi.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                Check(s.SuperPasteShortcuts[1]=="12:122"&&statusUi.Text==L.SPInvalidShortcut,"no-modifier input rejected visibly");
                modsUi[0].IsChecked=true;mainKeyUi.SelectedValue=86;
                saveKeyUi.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                Check(s.SuperPasteShortcuts[1]=="12:122"&&statusUi.Text==L.SPInvalidShortcut,"manual editor preserves normal Ctrl V");
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
            using(var box=new Receiver {Dock=DockStyle.Fill,ImeMode=ImeMode.Disable}) {
                form.Controls.Add(box); form.Show();
                Application.DoEvents();
                uint pid; uint foregroundThread=Native.GetWindowThreadProcessId(previous,out pid);
                uint ownThread=Native.GetCurrentThreadId();
                bool attached=foregroundThread!=ownThread && AttachThreadInput(ownThread,foregroundThread,true);
                try { SetForegroundWindow(form.Handle);form.Activate();box.Focus(); }
                finally { if(attached)AttachThreadInput(ownThread,foregroundThread,false); }
                var watch=System.Diagnostics.Stopwatch.StartNew();
                while(watch.ElapsedMilliseconds<150){Application.DoEvents();System.Threading.Thread.Sleep(5);}
                Check(Native.GetForegroundWindow()==form.Handle,"test receiver has focus");
                box.Focus();
                Check(box.Focused,"receiving textbox has keyboard focus");
                box.Clear();
                var desktop=new InputDesktop(form.Handle);string failure=null;
                using(var paste=new SuperPaste(desktop,()=>watch.Elapsed.TotalSeconds,s=>failure=s,seq=>box.Payload=desktop.Output)) {
                    paste.Begin(new PasteRules());paste.Step();
                    long deadline=watch.ElapsedMilliseconds+500;
                    while(watch.ElapsedMilliseconds<deadline){Application.DoEvents();System.Threading.Thread.Sleep(5);}
                    Check(failure==null&&box.Pastes==1&&box.Text=="hello","SendInput delivers exactly one paste of cleaned text; failure="+failure+"; pastes="+box.Pastes+"; length="+box.TextLength+"; suffix-crlf="+box.Text.EndsWith("\r\n")+"; prefix-crlf="+box.Text.StartsWith("\r\n")+"; payload="+(box.Payload=="hello")+"; busy="+paste.Busy+"; focus="+(Native.GetForegroundWindow()==form.Handle));
                }
                form.Close();
            }
            if(previous!=IntPtr.Zero)SetForegroundWindow(previous);
        }
    }
}
