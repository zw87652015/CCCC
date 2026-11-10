using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Cccc
{
    sealed class PasteRules
    {
        public bool Symbols = true, Trailing, BlankLines, EndNewlines = true;
    }
    // Source-preserving cleaner: fenced code blocks and tab-separated runs stay byte-identical.
    static class SuperPasteText
    {
        sealed class Line { public string Text, End; public bool Protected; }
        static readonly Regex Lines = new Regex(@"([^\r\n]*)(\r\n|\r|\n|$)");
        static readonly Regex Symbols = new Regex("[\uF06C\uF06E\uF075\uF0FC\uF0D8\uF0B2]");
        static readonly Regex Fence = new Regex(@"^ {0,3}(`{3,}|~{3,})");
        public static string Clean(string text, PasteRules rules)
        {
            var lines = new List<Line>();
            foreach (Match m in Lines.Matches(text))
                if (m.Length > 0) lines.Add(new Line { Text = m.Groups[1].Value, End = m.Groups[2].Value });
            // Tabular runs preserve separators, cell whitespace and line endings.
            for (int i = 0; i < lines.Count; i++)
            {
                int tabs = CountTabs(lines[i].Text), j = i + 1;
                if (tabs == 0) continue;
                while (j < lines.Count && CountTabs(lines[j].Text) == tabs) j++;
                if (j - i >= 2) { for (int n = i; n < j; n++) lines[n].Protected = true; i = j - 1; }
            }
            // Fenced code blocks: a ``` or ~~~ opener closes on a matching fence with a blank rest.
            char fence = '\0'; int fenceSize = 0;
            foreach (var line in lines)
            {
                Match f = Fence.Match(line.Text);
                if (fence != '\0')
                {
                    line.Protected = true;
                    if (f.Success && f.Groups[1].Value[0] == fence && f.Groups[1].Length >= fenceSize && line.Text.Substring(f.Length).Trim().Length == 0) fence = '\0';
                    continue;
                }
                if (f.Success) { fence = f.Groups[1].Value[0]; fenceSize = f.Groups[1].Length; line.Protected = true; }
            }
            foreach (var line in lines)
            {
                if (line.Protected) continue;
                if (rules.Symbols) line.Text = Symbols.Replace(line.Text, "");
                if (rules.Trailing) line.Text = line.Text.TrimEnd(' ', '\u3000');
            }
            if (rules.BlankLines)
            {
                // Collapse each run of blank unprotected lines to its first line.
                var kept = new List<Line>(lines.Count);
                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i]; kept.Add(line);
                    if (line.Protected || line.Text.Length != 0 || line.End.Length == 0) continue;
                    int j = i + 1;
                    while (j < lines.Count && !lines[j].Protected && lines[j].Text.Length == 0 && lines[j].End.Length > 0) j++;
                    i = j - 1;
                }
                lines = kept;
            }
            if (rules.EndNewlines)
            {
                for (int i = lines.Count - 1; i >= 0 && !lines[i].Protected; i--)
                {
                    if (lines[i].End.Length > 0) lines[i].End = "";
                    if (lines[i].Text.Length != 0) break;
                }
            }
            var output = new StringBuilder(); foreach (var line in lines) output.Append(line.Text).Append(line.End);
            return output.ToString();
        }
        static int CountTabs(string s) { int count = 0; foreach (char c in s) if (c == '\t') count++; return count; }
    }
}
