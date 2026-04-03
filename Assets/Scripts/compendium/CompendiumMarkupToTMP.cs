using System;
using System.Text;

namespace Compendium
{
    public static class CompendiumMarkupToTMP
    {
        // Converts:
        // [[Display|article:some_id]] -> <link="article:some_id"><mark=#2D6CDF33>Display</mark></link>
        // {{Display|term:some_term}}  -> <link="term:some_term"><mark=#F5C54233>Display</mark></link>
        //
        // Colors are RGBA hex (AA = alpha). Adjust to taste.
        private const string ArticleMarkColor = "#2D6CDF33";
        private const string TermMarkColor = "#F5C54233";

        public static string ToTmpRichText(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            var sb = new StringBuilder(input.Length + 32);
            int i = 0;

            while (i < input.Length)
            {
                // [[ ... ]]
                if (i + 1 < input.Length && input[i] == '[' && input[i + 1] == '[')
                {
                    if (TryParseToken(input, i + 2, "]]", out var token, out var endIndex) &&
                        TrySplitToken(token, out var display, out var kind, out var id) &&
                        string.Equals(kind, "article", StringComparison.OrdinalIgnoreCase))
                    {
                        sb.Append("<link=\"article:");
                        sb.Append(EscapeLinkId(id));
                        sb.Append("\"><mark=");
                        sb.Append(ArticleMarkColor);
                        sb.Append(">");
                        sb.Append(EscapeTmpText(display));
                        sb.Append("</mark></link>");
                        i = endIndex + 2;
                        continue;
                    }
                }

                // {{ ... }}
                if (i + 1 < input.Length && input[i] == '{' && input[i + 1] == '{')
                {
                    if (TryParseToken(input, i + 2, "}}", out var token, out var endIndex) &&
                        TrySplitToken(token, out var display, out var kind, out var id) &&
                        string.Equals(kind, "term", StringComparison.OrdinalIgnoreCase))
                    {
                        sb.Append("<link=\"term:");
                        sb.Append(EscapeLinkId(id));
                        sb.Append("\"><mark=");
                        sb.Append(TermMarkColor);
                        sb.Append(">");
                        sb.Append(EscapeTmpText(display));
                        sb.Append("</mark></link>");
                        i = endIndex + 2;
                        continue;
                    }
                }

                // default char
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
            var rhs = token.Substring(pipe + 1).Trim(); // e.g. term:status_barrier

            var colon = rhs.IndexOf(':');
            if (colon <= 0 || colon >= rhs.Length - 1) return false;

            kind = rhs.Substring(0, colon).Trim();
            id = rhs.Substring(colon + 1).Trim();

            return !string.IsNullOrEmpty(display) && !string.IsNullOrEmpty(kind) && !string.IsNullOrEmpty(id);
        }

        // TMP text escapes (minimal). If you insert raw '<' you can break tags.
        private static string EscapeTmpText(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("<", "&lt;").Replace(">", "&gt;");
        }

        // Link IDs should be safe. We just trim and avoid quotes.
        private static string EscapeLinkId(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Trim().Replace("\"", "");
        }
    }
}
