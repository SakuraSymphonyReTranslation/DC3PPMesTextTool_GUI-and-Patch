using System.Text;
using System.Text.RegularExpressions;

namespace DC3PPMesTextTool;

/// <summary>Wrapping for normal DC3PP story text, in half-width columns.</summary>
public class TextFormatter
{
    // 46 * 11px = 506px, allowing room for the outline in the narrower backlog.
    // Vertical spacing belongs to DC3PPPatch.dll, not to the MES text stream.
    public const int DefaultWidth = 46;
    public int MinLineLength { get; set; } = 18; // Compatibility; wrapping uses MaxLineLength.
    public const int RecommendedLines = 4;
    public const int MinimumWidth = 10;
    public const int MaximumWidth = 100;

    private const string DisallowedAtStart = "。、，．？！：；）］｝」』】〉》’”!?.,:;)]}";
    private const string DisallowedAtEnd = "（［｛「『【〈《‘“([{“";
    private static readonly Regex Control = new(
        @"\G@(?:se[0-9]+|[btwd][0-9]+|c(?:[0-9]{1,2}|[rgbwk])|[a-z])",
        RegexOptions.Compiled);

    public int MaxLineLength { get; set; } = DefaultWidth;

    // CP932 has no em dash/Latin macrons. Use explicit readable equivalents,
    // rather than the encoder's silent '?' replacement.
    public static string NormalizeForEngine(string text) => NormalizeLineEndings(text)
        .Replace('—', '―').Replace('é', 'e').Replace('ā', 'a').Replace('·', '・');

    public string FormatBody(string text)
    {
        text = NormalizeForEngine(text);
        string wrapped = Format(text);
        if (!RequiresNativeLayout(text) && wrapped.Split('\n').Length > RecommendedLines)
        {
            // @s selects the engine's native 16px font (normal is 22px).
            // Keep the same physical width and all explicit paragraph breaks.
            var compact = new TextFormatter { MaxLineLength = Math.Min(MaximumWidth, MaxLineLength * 22 / 16) };
            return "@s" + compact.Format(text);
        }
        return wrapped;
    }

    public static string NormalizeLineEndings(string text) => text
        .Replace("\r\n", "\n").Replace('\r', '\n')
        .Replace("\\r\\n", "\n").Replace("\\n", "\n");

    public static void ValidateWidth(int width)
    {
        if (width < MinimumWidth || width > MaximumWidth)
            throw new ArgumentOutOfRangeException(nameof(width),
                $"Wrap width must be {MinimumWidth}–{MaximumWidth} half-width columns.");
    }

    /// <summary>Ruby/font effects must not be split or restarted by the wrapper.</summary>
    public static bool RequiresNativeLayout(string text)
    {
        if (text.Contains('｛')) return true;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '@') continue;
            var match = Control.Match(text, i);
            if (!match.Success) return true;
            string value = match.Value;
            bool timing = value.StartsWith("@se", StringComparison.Ordinal) ||
                (value.Length > 2 && "btwd".Contains(value[1]) && char.IsAsciiDigit(value[2]));
            if (!timing) return true;
            i += value.Length - 1;
        }
        return false;
    }

    private readonly record struct Unit(string Text, char Glyph, int Width)
    {
        public bool IsSpace => Glyph is ' ' or '　' or '\t';
    }

    private static List<Unit> Tokenize(string line)
    {
        var units = new List<Unit>();
        for (int i = 0; i < line.Length; i++)
        {
            var match = line[i] == '@' ? Control.Match(line, i) : null;
            if (match?.Success == true)
            {
                units.Add(new Unit(match.Value, '\0', 0));
                i += match.Length - 1;
            }
            else
            {
                char c = line[i];
                units.Add(new Unit(c.ToString(), c,
                    c <= 0x7f || c is >= '\uff61' and <= '\uff9f' ? 1 : 2));
            }
        }
        return units;
    }

    public static int MeasureLine(string line) => Tokenize(line).Sum(unit => unit.Width);

    public string Format(string text)
    {
        ValidateWidth(MaxLineLength);
        text = NormalizeLineEndings(text).Replace('_', ' ');
        if (RequiresNativeLayout(text)) return text;
        return string.Join("\n", text.Split('\n').Select(WrapLine));
    }

    private string WrapLine(string line)
    {
        var units = Tokenize(line);
        var result = new StringBuilder();
        int start = 0;
        while (start < units.Count)
        {
            int fit = start;
            int width = 0;
            while (fit < units.Count && width + units[fit].Width <= MaxLineLength)
                width += units[fit++].Width;

            if (fit == units.Count)
            {
                AppendRange(start, fit);
                break;
            }

            int split = FindBreak(fit, natural: true);
            if (split <= start) split = FindBreak(fit, natural: false);
            if (split <= start)
                throw new InvalidDataException("Cannot wrap this punctuation sequence; add a manual newline.");

            int end = split;
            while (end > start && units[end - 1].IsSpace) end--;
            AppendRange(start, end);
            result.Append('\n');
            start = split;
            while (start < units.Count && units[start].IsSpace) start++;
        }
        return result.ToString();

        void AppendRange(int begin, int end)
        {
            for (int i = begin; i < end; i++) result.Append(units[i].Text);
        }

        int FindBreak(int limit, bool natural)
        {
            for (int split = limit; split > start; split--)
            {
                // Keep zero-width commands with the following visible text.
                if (units[split - 1].Width == 0) continue;
                int left = split - 1, right = split;
                bool space = units[left].IsSpace;
                while (left >= start && (units[left].IsSpace || units[left].Width == 0)) left--;
                while (right < units.Count && (units[right].IsSpace || units[right].Width == 0))
                {
                    space |= units[right].IsSpace;
                    right++;
                }
                if (left < start || right == units.Count) continue;
                char previous = units[left].Glyph, next = units[right].Glyph;
                if (DisallowedAtEnd.Contains(previous) || DisallowedAtStart.Contains(next)) continue;
                if (!natural || space || units[left].Width == 2 || units[right].Width == 2)
                    return split;
            }
            return -1;
        }
    }
}

public sealed record LayoutWarning(int MesSlot, string Reason, string Message);
