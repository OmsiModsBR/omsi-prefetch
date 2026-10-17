using System.Text;

namespace OmsiLaunch.Modules.Prefetch;

// The preloader's own log, in its data directory (logs\preloader-*.log); the
// ten most recent are kept.
internal static class Log
{
    private static readonly object Gate = new();
    private static StreamWriter? writer;

    public static string? Path { get; private set; }

    public static void Open(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            foreach (var old in new DirectoryInfo(directory).GetFiles("preloader-*.log").OrderByDescending(x => x.Name, StringComparer.Ordinal).Skip(9))
                try { old.Delete(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            Path = System.IO.Path.Combine(directory, "preloader-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture) + ".log");
            writer = new StreamWriter(new FileStream(Path, FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { writer = null; }
    }

    public static void Write(string message)
    {
        lock (Gate)
        {
            try { writer?.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff ", System.Globalization.CultureInfo.InvariantCulture) + message); }
            catch (IOException) { }
        }
    }

    public static void Close()
    {
        lock (Gate) { writer?.Dispose(); writer = null; }
    }
}
