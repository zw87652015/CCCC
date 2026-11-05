using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Cccc
{
    sealed class PasteRules
    {
        public bool Markdown = true, Symbols = true, EndNewlines = true;
        public bool Tabs, Leading, Trailing, CjkSpaces, Spaces, Newlines, BlankLines;
    }
    sealed class PasteResult
    {
        public string Text;
        public int Changes, ProtectedRegions;
    }
    // Source-preserving scanner: ambiguous Markdown is left intact rather than reserialized.
    static class SuperPasteText
    {
        sealed class Line { public string Text, End; public bool Protected; }
        static readonly Regex Lines = new Regex(@"([^\r\n]*)(\r\n|\r|\n|$)");
        static readonly Regex Symbols = new Regex("[\uF06C\uF06E\uF075\uF0FC\uF0D8\uF0B2]");
        static readonly Regex Cjk = new Regex(@"(?<=[\p{IsCJKUnifiedIdeographs}])[ \u3000]+(?=[A-Za-z0-9])|(?<=[A-Za-z0-9])[ \u3000]+(?=[\p{IsCJKUnifiedIdeographs}])");
        static bool IsStructure(string s)
        {
            return Regex.IsMatch(s, @"^( {0,3}(>|#{1,6}(\s|$)|([-+*]|\d+[.)])\s|\[[^\]]+\]:)| {4}|\t|\s*[-*_][\s*_-]{2,}\s*$|\s*[=-]+\s*$)") || s.Contains("|");
        }
        public static PasteResult Clean(string text, PasteRules rules)
        {
            var result = new PasteResult();
            var lines = new List<Line>();
            foreach (Match m in Lines.Matches(text))
                if (m.Length > 0) lines.Add(new Line { Text = m.Groups[1].Value, End = m.Groups[2].Value });
            // Tabular runs preserve separators, cell whitespace and line endings in either mode.
            for (int i = 0; i < lines.Count; i++)
            {
                int tabs = CountTabs(lines[i].Text), j = i + 1;
                if (tabs == 0) continue;
                while (j < lines.Count && CountTabs(lines[j].Text) == tabs) j++;
                if (j - i >= 2) { for (int n = i; n < j; n++) lines[n].Protected = true; i = j - 1; }
            }
            if (rules.Markdown)
            {
                char fence = '\0'; int fenceSize = 0, inlineTicks = 0; bool html = false, list = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i]; string s = line.Text;
                    Match f = Regex.Match(s, @"^ {0,3}(`{3,}|~{3,})");
                    if (fence != '\0')
                    {
                        line.Protected = true;
                        if (f.Success && f.Groups[1].Value[0] == fence && f.Groups[1].Length >= fenceSize && s.Substring(f.Length).Trim().Length == 0) fence = '\0';
                        continue;
                    }
                    if (f.Success) { fence = f.Groups[1].Value[0]; fenceSize = f.Groups[1].Length; line.Protected = true; continue; }
                    bool inlineCode = inlineTicks > 0 || s.Contains("`");
                    foreach (Match run in Regex.Matches(s, "`+"))
                    {
                        if (inlineTicks == 0) inlineTicks = run.Length;
                        else if (inlineTicks == run.Length) inlineTicks = 0;
                    }
                    if (inlineCode) { line.Protected = true; continue; }
                    // Conservative HTML/list blocks include lazy continuation and their terminating blank line.
                    if (Regex.IsMatch(s, @"^\s*<[/!A-Za-z?]") || (i == 0 && s == "---")) html = true;
                    if (Regex.IsMatch(s, @"^ {0,3}([-+*]|\d+[.)])\s")) list = true;
                    if (html || list || IsStructure(s)) line.Protected = true;
                    if (s.Trim().Length == 0) { html = false; list = false; }
                }
                // A single pipe-bearing row may be a table: protect adjacent table rows above as well.
                for (int i = 1; i < lines.Count; i++)
                    if (lines[i].Text.Contains("|") && lines[i-1].Text.Contains("|")) lines[i-1].Protected = true;
            }
            foreach (var line in lines)
            {
                if (line.Protected) { result.ProtectedRegions++; continue; }
                string before = line.Text;
                line.Text = rules.Markdown ? CleanMarkdownLine(before, rules) : CleanPlain(before, rules, 0);
                if (before != line.Text) result.Changes++;
            }
            // Newline rules act on unprotected boundaries only.
            string dominant = "\r\n"; int best = 0;
            foreach (string ending in new[] { "\r\n", "\n", "\r" })
            {
                int n = 0; foreach (var line in lines) if (line.End == ending) n++;
                if (n > best) { best = n; dominant = ending; }
            }
            if (rules.Newlines || (!rules.Markdown && rules.BlankLines))
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].Protected || lines[i].End.Length == 0) continue;
                    int j = i + 1;
                    while (j < lines.Count && !lines[j].Protected && lines[j].Text.Length == 0 && lines[j].End.Length > 0) j++;
                    int keep = rules.Markdown || rules.BlankLines ? 1 : 0;
                    int blankCount = j - i - 1;
                    if (blankCount > keep)
                    { lines.RemoveRange(i + 1 + keep, blankCount - keep); result.Changes++; }
                    if (!rules.Markdown && rules.BlankLines && blankCount == 0 && i + 1 < lines.Count && !lines[i+1].Protected)
                    { lines.Insert(i + 1, new Line { Text = "", End = dominant }); result.Changes++; i++; }
                    else i += Math.Min(keep, blankCount);
                }
            }
            if (rules.EndNewlines)
            {
                for (int i = lines.Count - 1; i >= 0 && !lines[i].Protected; i--)
                {
                    if (lines[i].End.Length > 0) { lines[i].End = ""; result.Changes++; }
                    if (lines[i].Text.Length != 0) break;
                }
            }
            var output = new StringBuilder(); foreach (var line in lines) output.Append(line.Text).Append(line.End);
            result.Text = output.ToString(); return result;
        }
        static int CountTabs(string s) { int count = 0; foreach (char c in s) if (c == '\t') count++; return count; }
        static string CleanMarkdownLine(string s, PasteRules rules)
        {
            // Preserve a complete line when syntax is ambiguous or code spans cross lines.
            // This also protects delimiter spacing in emphasis, escapes, links and extensions.
            if (s.IndexOfAny(new[] { '`', '\\', '[', ']', '<', '>', '*', '_', '$' }) >= 0) return s;
            int hard = s.Length - s.TrimEnd(' ').Length;
            string suffix = hard >= 2 ? s.Substring(s.Length - hard) : "";
            string body = suffix.Length > 0 ? s.Substring(0, s.Length - suffix.Length) : s;
            string cleaned = CleanPlain(body, rules, 0) + suffix;
            return IsStructure(cleaned) ? s : cleaned;
        }
        static string CleanPlain(string s, PasteRules r, int column)
        {
            if (r.Symbols) s = Symbols.Replace(s, "");
            if (r.Tabs)
            {
                var b = new StringBuilder(); foreach (char c in s)
                    if (c == '\t') { int n = 4 - column % 4; b.Append(' ', n); column += n; }
                    else { b.Append(c); column++; }
                s = b.ToString();
            }
            if (r.Leading) s = s.TrimStart(' ', '\u3000');
            if (r.Trailing) s = s.TrimEnd(' ', '\u3000');
            if (r.CjkSpaces) s = Cjk.Replace(s, "");
            if (r.Spaces) s = Regex.Replace(s, " {2,}", " ");
            return s;
        }
    }
}
