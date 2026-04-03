using System;
using System.Collections.Generic;
using System.Text;

namespace Compendium
{
    public enum MarkupSpanType
    {
        ArticleLink, // [[Text|article:some_id]]
        TermTooltip  // {{Text|term:some_term}}
    }

    public readonly struct MarkupSpan
    {
        public readonly int start;
        public readonly int length;
        public readonly MarkupSpanType type;
        public readonly string targetId;
        public readonly string displayText;

        public MarkupSpan(int start, int length, MarkupSpanType type, string targetId, string displayText)
        {
            this.start = start;
            this.length = length;
            this.type = type;
            this.targetId = targetId;
            this.displayText = displayText;
        }

        public bool ContainsIndex(int charIndex) => charIndex >= start && charIndex < (start + length);
    }

    public static class CompendiumMarkup
    {
        // [[Display|article:article_id]]
        // {{Display|term:term_id}}
        public static string Parse(string input, List<MarkupSpan> spansOut)
        {
            spansOut.Clear();
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            var sb = new StringBuilder(input.Length);
            int i = 0;

            while (i < input.Length)
            {
                if (i + 1 < input.Length && input[i] == '[' && input[i + 1] == '[')
                {
                    if (TryParseToken(input, i + 2, "]]", out var token, out var endIndex) &&
                        TrySplitToken(token, out var display, out var kind, out var id) &&
                        string.Equals(kind, "article", StringComparison.OrdinalIgnoreCase))
                    {
                        int startIndex = sb.Length;
                        sb.Append(display);
                        spansOut.Add(new MarkupSpan(startIndex, display.Length, MarkupSpanType.ArticleLink, id, display));
                        i = endIndex + 2;
                        continue;
                    }
                }

                if (i + 1 < input.Length && input[i] == '{' && input[i + 1] == '{')
                {
                    if (TryParseToken(input, i + 2, "}}", out var token, out var endIndex) &&
                        TrySplitToken(token, out var display, out var kind, out var id) &&
                        string.Equals(kind, "term", StringComparison.OrdinalIgnoreCase))
                    {
                        int startIndex = sb.Length;
                        sb.Append(display);
                        spansOut.Add(new MarkupSpan(startIndex, display.Length, MarkupSpanType.TermTooltip, id, display));
                        i = endIndex + 2;
                        continue;
                    }
                }

                sb.Append(input[i]);
                i++;
            }

            return sb.ToString();
        }

        private static bool TryParseToken(string s, int start, string endDelim, out string token, out int endIndex)
        {
            token = null;
            endIndex = -1;

            var idx = s.IndexOf(endDelim, start, StringComparison.Ordinal);
            if (idx < 0) return false;

            token = s.Substring(start, idx - start);
            endIndex = idx;
            return true;
        }

        private static bool TrySplitToken(string token, out string display, out string kind, out string id)
        {
            display = null;
            kind = null;
            id = null;

            if (string.IsNullOrWhiteSpace(token)) return false;

            var pipe = token.IndexOf('|');
            if (pipe <= 0 || pipe >= token.Length - 1) return false;

            display = token.Substring(0, pipe).Trim();
            var rhs = token.Substring(pipe + 1).Trim(); // e.g. article:mech_unit_shield

            var colon = rhs.IndexOf(':');
            if (colon <= 0 || colon >= rhs.Length - 1) return false;

            kind = rhs.Substring(0, colon).Trim();
            id = rhs.Substring(colon + 1).Trim();

            return !string.IsNullOrEmpty(display) && !string.IsNullOrEmpty(kind) && !string.IsNullOrEmpty(id);
        }
    }
}
