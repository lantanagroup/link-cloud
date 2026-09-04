using System.Text.RegularExpressions;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Shared CQL text utilities: comment stripping, define parsing, and lightweight
/// token extraction. This is not a CQL compiler — it exists so runtime prediction
/// can read the measure bundle that MeasureEval will actually evaluate.
/// </summary>
internal static class CqlText
{
    private static readonly Regex DefinePattern = new(
        """(?im)^\s*define\s+(?:"([^"\r\n]+)"|([A-Za-z_][A-Za-z0-9_]*))\s*:""",
        RegexOptions.Compiled);

    private static readonly Regex CodeDeclarationPattern = new(
        """(?im)^\s*code\s+"([^"]+)"\s*:\s*'([^']+)'""",
        RegexOptions.Compiled);

    private static readonly Regex ValuesetDeclarationPattern = new(
        """(?im)^\s*valueset\s+"([^"]+)"\s*:\s*'([^']+)'""",
        RegexOptions.Compiled);

    public static string StripComments(string cql)
    {
        if (string.IsNullOrEmpty(cql))
            return string.Empty;

        // Character scanner so `http://` / `https://` inside valueset URLs and
        // quoted CQL strings are not treated as `//` line comments. A naive
        // `//.*?$` regex truncated every canonical URL and broke CQL-name →
        // ValueSet URL matching in CqlMeasureBundleModel.
        var sb = new System.Text.StringBuilder(cql.Length);
        for (var i = 0; i < cql.Length; i++)
        {
            var c = cql[i];
            if (c == '\'')
            {
                sb.Append(c);
                i++;
                while (i < cql.Length)
                {
                    sb.Append(cql[i]);
                    if (cql[i] == '\'')
                    {
                        if (i + 1 < cql.Length && cql[i + 1] == '\'')
                        {
                            sb.Append(cql[++i]);
                            i++;
                            continue;
                        }

                        break;
                    }

                    i++;
                }

                continue;
            }

            if (c == '"')
            {
                sb.Append(c);
                i++;
                while (i < cql.Length)
                {
                    sb.Append(cql[i]);
                    if (cql[i] == '"')
                        break;
                    i++;
                }

                continue;
            }

            if (c == '/' && i + 1 < cql.Length && cql[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < cql.Length && !(cql[i] == '*' && cql[i + 1] == '/'))
                    i++;
                if (i + 1 < cql.Length)
                    i++;
                sb.Append(' ');
                continue;
            }

            if (c == '/' && i + 1 < cql.Length && cql[i + 1] == '/'
                && (i == 0 || cql[i - 1] != ':'))
            {
                while (i < cql.Length && cql[i] != '\n' && cql[i] != '\r')
                    i++;
                if (i < cql.Length)
                    sb.Append(cql[i]);
                continue;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    public static Dictionary<string, string> ParseDefineBodies(string cql)
    {
        var defines = new Dictionary<string, string>(StringComparer.Ordinal);
        var matches = DefinePattern.Matches(cql);
        for (var i = 0; i < matches.Count; i++)
        {
            var current = matches[i];
            var name = current.Groups[1].Success ? current.Groups[1].Value : current.Groups[2].Value;
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var bodyStart = current.Index + current.Length;
            var bodyEnd = i + 1 < matches.Count ? matches[i + 1].Index : cql.Length;
            if (bodyEnd <= bodyStart)
                continue;

            defines[name] = cql[bodyStart..bodyEnd];
        }

        return defines;
    }

    public static Dictionary<string, string> ParseCodeDeclarations(string cql)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in CodeDeclarationPattern.Matches(cql))
        {
            var name = match.Groups[1].Value;
            var code = match.Groups[2].Value;
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(code))
                result[name] = code;
        }

        return result;
    }

    public static Dictionary<string, string> ParseValuesetDeclarations(string cql)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in ValuesetDeclarationPattern.Matches(cql))
        {
            var name = match.Groups[1].Value;
            var url = match.Groups[2].Value;
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(url))
                result[name] = url;
        }

        return result;
    }

    public static IEnumerable<string> SplitTopLevelUnion(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        var depth = 0;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is '(' or '{' or '[')
            {
                depth++;
                continue;
            }

            if (c is ')' or '}' or ']')
            {
                if (depth > 0) depth--;
                continue;
            }

            if (depth != 0 || i + 5 > text.Length)
                continue;

            if (!text.AsSpan(i).StartsWith("union", StringComparison.OrdinalIgnoreCase))
                continue;

            var beforeOk = i == 0 || char.IsWhiteSpace(text[i - 1]) || text[i - 1] is ')' or ']';
            var afterOk = i + 5 == text.Length || char.IsWhiteSpace(text[i + 5]) || text[i + 5] is '(' or '[';
            if (!beforeOk || !afterOk)
                continue;

            var part = text[start..i].Trim();
            if (part.Length > 0)
                yield return part;
            i += 4;
            start = i + 1;
        }

        var tail = text[start..].Trim();
        if (tail.Length > 0)
            yield return tail;
    }

    public static string? ExtractTopLevelWhere(string body)
    {
        var depth = 0;
        for (var i = 0; i < body.Length; i++)
        {
            var c = body[i];
            if (c is '(' or '{' or '[')
            {
                depth++;
                continue;
            }

            if (c is ')' or '}' or ']')
            {
                if (depth > 0) depth--;
                continue;
            }

            if (depth != 0)
                continue;

            if (IsKeywordAt(body, i, "where"))
            {
                var whereStart = i + 5;
                var whereEnd = FindTopLevelKeyword(body, whereStart, "return");
                return whereEnd >= 0
                    ? body[whereStart..whereEnd].Trim()
                    : body[whereStart..].Trim();
            }

            if (IsKeywordAt(body, i, "return"))
                return null;
        }

        return null;
    }

    public static string? ExtractTopLevelReturn(string body)
    {
        var idx = FindTopLevelKeyword(body, 0, "return");
        return idx < 0 ? null : body[(idx + 6)..].Trim();
    }

    public static bool IsKeywordAt(string text, int index, string keyword)
    {
        if (index < 0 || index + keyword.Length > text.Length)
            return false;
        if (!text.AsSpan(index).StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
            return false;
        if (index > 0 && char.IsLetterOrDigit(text[index - 1]))
            return false;
        var after = index + keyword.Length;
        return after == text.Length || !char.IsLetterOrDigit(text[after]);
    }

    public static int FindTopLevelKeyword(string text, int start, string keyword)
    {
        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (c is '(' or '{' or '[')
            {
                depth++;
                continue;
            }

            if (c is ')' or '}' or ']')
            {
                if (depth > 0) depth--;
                continue;
            }

            if (depth == 0 && IsKeywordAt(text, i, keyword))
                return i;
        }

        return -1;
    }

    public static string UnwrapOuterParens(string text)
    {
        var t = text.Trim();
        while (t.Length >= 2 && t[0] == '(' && t[^1] == ')' && ParensBalanced(t[1..^1]))
            t = t[1..^1].Trim();
        return t;
    }

    private static bool ParensBalanced(string text)
    {
        var depth = 0;
        foreach (var c in text)
        {
            if (c == '(') depth++;
            else if (c == ')')
            {
                depth--;
                if (depth < 0) return false;
            }
        }

        return depth == 0;
    }
}
