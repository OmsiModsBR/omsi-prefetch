using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;

namespace OmsiLaunch.Modules.Prefetch;

// The session channel: the module's native component in OMSI connects to it
// and reports what the game uses, one UTF-8 line each:
//   U<TAB><path>  first use of a file      T<TAB><path>  a tile_X_Y.map opened
//   W<TAB><path>  the game wrote to a file X<TAB><path>  OMSI cannot replace a
//                                                        held file: release it
// The pipe is restricted to the current user, and a client that is not OMSI's
// process is refused. Paths are data: bounded, resolved inside the
// installation, never opened by anything but the holding code.
//
// An X line is OMSI waiting: its native shim retries the file operation for a
// short, fixed time (about 1.5 s) and then fails it. So the pipe's reading
// thread answers X at once and hands every other line (which can mean reading
// a directory or mapping files) to a worker; a burst of U lines can never
// delay a release. The worker queue is bounded: when it is full a U line (a
// hint) is dropped and counted, a T or W line waits briefly.
internal sealed class Feed : IDisposable
{
    private const string PipePrefix = @"\\.\pipe\";
    private const int MaxLine = 4096;
    private readonly string name;
    private readonly Func<int?> omsiProcessId;
    private readonly Action<char, string> onLine;
    private readonly CancellationTokenSource stop = new();
    private NamedPipeServerStream? server;
    private Thread? thread;
    private Thread? worker;
    private readonly BlockingCollection<(char Kind, string Path)> queue = new(new ConcurrentQueue<(char, string)>(), 4096);

    public Feed(string channel, Func<int?> omsiProcessId, Action<char, string> onLine)
    {
        if (!channel.StartsWith(PipePrefix, StringComparison.Ordinal) || channel.Length <= PipePrefix.Length) throw new ArgumentException("not a pipe name", nameof(channel));
        name = channel[PipePrefix.Length..];
        this.omsiProcessId = omsiProcessId; this.onLine = onLine;
    }

    public long Lines;
    public long Refused;
    // U lines dropped because the worker could not keep up.
    public long Dropped;
    public volatile bool Connected;

    // Creates the pipe (before the session program reports ready) and serves it.
    public void Start()
    {
        server = Create();
        worker = new Thread(Work) { IsBackground = true, Name = "feed-work" };
        worker.Start();
        thread = new Thread(Run) { IsBackground = true, Name = "feed" };
        thread.Start();
    }

    private NamedPipeServerStream Create() => new(name, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, 1 << 16, 0);

    private void Run()
    {
        var buffer = new byte[1 << 16];
        var line = new List<byte>(512);
        while (!stop.IsCancellationRequested)
        {
            var pipe = server!;
            try { pipe.WaitForConnectionAsync(stop.Token).GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { return; }
            catch (IOException) { Recreate(); continue; }
            if (!IsOmsi(pipe)) { Interlocked.Increment(ref Refused); Log.Write("feed: refused a client that is not OMSI's process"); Recreate(); continue; }
            Connected = true;
            Log.Write("feed: OMSI connected");
            line.Clear();
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    var read = pipe.ReadAsync(buffer, 0, buffer.Length, stop.Token).GetAwaiter().GetResult();
                    if (read <= 0) break;
                    for (var i = 0; i < read; i++)
                    {
                        var b = buffer[i];
                        if (b != (byte)'\n') { if (line.Count < MaxLine) line.Add(b); continue; }
                        Dispatch(line);
                        line.Clear();
                    }
                }
            }
            catch (OperationCanceledException) { return; }
            catch (IOException) { }
            Connected = false;
            Log.Write("feed: OMSI disconnected");
            Recreate();
        }
    }

    private void Dispatch(List<byte> line)
    {
        if (line.Count < 3 || line.Count >= MaxLine || line[1] != (byte)'\t') return;
        var kind = (char)line[0];
        if (kind is not ('U' or 'T' or 'W' or 'X')) return;
        string path;
        try { path = new UTF8Encoding(false, true).GetString(line.ToArray(), 2, line.Count - 2); }
        catch (DecoderFallbackException) { return; }
        if (path.Any(char.IsControl)) return;
        Interlocked.Increment(ref Lines);
        // OMSI is waiting on an X: answer it now, on this thread.
        if (kind == 'X') { Handle(kind, path); return; }
        if (queue.TryAdd((kind, path))) return;
        if (kind == 'U') { Interlocked.Increment(ref Dropped); return; }
        try { if (!queue.TryAdd((kind, path), 50, stop.Token)) Interlocked.Increment(ref Dropped); }
        catch (OperationCanceledException) { }
    }

    private void Work()
    {
        try { foreach (var (kind, path) in queue.GetConsumingEnumerable(stop.Token)) Handle(kind, path); }
        catch (OperationCanceledException) { }
    }

    private void Handle(char kind, string path)
    {
        try { onLine(kind, path); }
        catch (Exception exception) { Log.Write("feed: line not handled: " + exception.GetType().Name + ": " + exception.Message); }
    }

    // The client must be OMSI's process (the controller says which, right
    // after creating it; a connection that comes first waits for it).
    private bool IsOmsi(NamedPipeServerStream pipe)
    {
        var client = Native.PipeClientProcessId(pipe.SafePipeHandle);
        for (var waited = 0; omsiProcessId() is null && waited < 10_000 && !stop.IsCancellationRequested; waited += 50) Thread.Sleep(50);
        return client is not null && client == omsiProcessId();
    }

    private void Recreate()
    {
        try { server?.Dispose(); } catch (IOException) { }
        if (stop.IsCancellationRequested) return;
        try { server = Create(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Write("feed: the pipe cannot be created again: " + exception.Message);
            stop.Cancel();
        }
    }

    public void Dispose()
    {
        stop.Cancel();
        thread?.Join(2000);
        worker?.Join(2000);
        try { server?.Dispose(); } catch (IOException) { }
    }
}
