using System.Text;

namespace OmsiLaunch.Modules.Prefetch;

// OMSI's text files: UTF-16 LE with a BOM (global.cfg), UTF-8 with a BOM, or
// ANSI (Windows-1252). Lines are trimmed; CRLF, LF and CR all end a line.
internal static class Text
{
    private static readonly Encoding Ansi;

    static Text()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Ansi = Encoding.GetEncoding(1252);
    }

    public const long ConfigLimit = 8L << 20;
    public const long TileLimit = 64L << 20;

    public static byte[]? ReadBytes(string path, long limit)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.SequentialScan);
            if (stream.Length <= 0 || stream.Length > limit) return null;
            var bytes = new byte[stream.Length];
            var read = 0;
            while (read < bytes.Length)
            {
                var n = stream.Read(bytes, read, bytes.Length - read);
                if (n <= 0) break;
                read += n;
            }
            return read == bytes.Length ? bytes : bytes[..read];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return null; }
    }

    public static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        return Ansi.GetString(bytes);
    }

    public static List<string> ReadLines(string path, long limit = ConfigLimit)
    {
        var bytes = ReadBytes(path, limit);
        return bytes is null ? new List<string>() : Lines(Decode(bytes));
    }

    public static List<string> Lines(string text)
    {
        var lines = new List<string>();
        var start = 0;
        for (var i = 0; i <= text.Length; i++)
        {
            if (i < text.Length && text[i] != '\n' && text[i] != '\r') continue;
            lines.Add(text[start..i].Trim(' ', '\t', '﻿'));
            if (i < text.Length && text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
            start = i + 1;
        }
        if (lines.Count > 0 && lines[^1].Length == 0 && (text.Length == 0 || text[^1] == '\n' || text[^1] == '\r')) lines.RemoveAt(lines.Count - 1);
        return lines;
    }

    // Candidate references of one line: its first tab-separated field and every
    // quoted string. Section lines ("[...]") have none.
    public static IEnumerable<string> LineTokens(string line)
    {
        if (line.Length == 0 || line[0] == '[') yield break;
        var tab = line.IndexOf('\t');
        yield return (tab < 0 ? line : line[..tab]).Trim();
        for (var open = line.IndexOf('"'); open >= 0;)
        {
            var close = line.IndexOf('"', open + 1);
            if (close < 0) break;
            yield return line.Substring(open + 1, close - open - 1);
            open = line.IndexOf('"', close + 1);
        }
    }

    public static IEnumerable<string> Tokens(IEnumerable<string> lines) => lines.SelectMany(LineTokens);

    public static bool EndsWith(string value, string suffix) => value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);

    public static bool EndsWithAny(string value, IReadOnlyList<string> suffixes)
    {
        foreach (var suffix in suffixes) if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static readonly string[] ImageExtensions = { ".bmp", ".dds", ".tga", ".png", ".jpg", ".jpeg" };

    // Texture names stored in a .o3d: printable strings preceded by their
    // length byte that end in an image extension.
    public static List<string> O3dTextures(string path)
    {
        var names = new List<string>();
        var data = ReadBytes(path, 256L << 20);
        if (data is null) return names;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i + 4 <= data.Length; i++)
        {
            if (data[i] != (byte)'.') continue;
            foreach (var extension in ImageExtensions)
            {
                var length = extension.Length;
                if (i + length > data.Length || !MatchesIgnoreCase(data, i, extension)) continue;
                var end = i + length;
                for (var size = length + 1; size < 256 && size <= end; size++)
                {
                    var start = end - size;
                    if (start < 1) break;
                    if (data[start - 1] != size) continue;
                    var printable = true;
                    for (var k = start; k < end; k++) { var c = data[k]; if (c < 32 || (c >= 127 && c < 160)) { printable = false; break; } }
                    if (printable)
                    {
                        var name = Ansi.GetString(data, start, size);
                        if (seen.Add(name)) names.Add(name);
                    }
                    break;
                }
                break;
            }
        }
        return names;
    }

    private static bool MatchesIgnoreCase(byte[] data, int at, string ascii)
    {
        for (var k = 0; k < ascii.Length; k++)
        {
            var c = data[at + k];
            if (c >= 'A' && c <= 'Z') c = (byte)(c + 32);
            if (c != ascii[k]) return false;
        }
        return true;
    }
}
