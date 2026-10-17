using System.Text;

namespace OmsiLaunch.Modules.Prefetch;

// The preloader's own log, in its data directory (logs\preloader-*.log). It is
// made for use, not for debugging: a session writes a few dozen lines (what was
// held, how long it took, what went wrong), a file never grows past
// MaxFileBytes (the rest is dropped after one marker line) and only the three
// most recent files are kept. The per-file and per-vehicle detail is written
// only when the debug_log setting asks for it.
internal static class Log
{
    public const long MaxFileBytes = 1L << 20;
    public const int KeptFiles = 3;
    private static readonly object Gate = new();
    private static StreamWriter? writer;
    private static long written;
    private static bool truncated;

    public static string? Path { get; private set; }
    // The debug_log setting: per-file and per-vehicle lines.
    public static bool Debug { get; set; }
    public static long WrittenBytes { get { lock (Gate) return written; } }

    public static void Open(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            written = 0; truncated = false;
            foreach (var old in new DirectoryInfo(directory).GetFiles("preloader-*.log").OrderByDescending(x => x.Name, StringComparer.Ordinal).Skip(KeptFiles - 1))
                try { old.Delete(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            Path = System.IO.Path.Combine(directory, "preloader-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture) + ".log");
            writer = new StreamWriter(new FileStream(Path, FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { writer = null; }
    }

    // A line of the normal log.
    public static void Write(string message)
    {
        lock (Gate)
        {
            if (writer is null) return;
            var line = DateTime.Now.ToString("HH:mm:ss.fff ", System.Globalization.CultureInfo.InvariantCulture) + (message.Length > 2000 ? message[..2000] + "..." : message);
            try
            {
                if (written + line.Length + 2 > MaxFileBytes)
                {
                    if (!truncated) { truncated = true; writer.WriteLine("-- log limit reached (" + (MaxFileBytes >> 10) + " KiB): further lines are dropped"); }
                    return;
                }
                writer.WriteLine(line);
                written += line.Length + 2;
            }
            catch (IOException) { }
        }
    }

    // A line of the debug log (per file, per vehicle): written only with debug_log.
    public static void Verbose(string message)
    {
        if (Debug) Write(message);
    }

    public static void Close()
    {
        lock (Gate) { writer?.Dispose(); writer = null; }
    }
}
