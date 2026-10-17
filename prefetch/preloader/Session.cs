using System.Diagnostics;
using System.Globalization;

namespace OmsiLaunch.Modules.Prefetch;

// One session of the prefetch module.
//
// Before OMSI (Prepare): the map requested to OmsiLaunch is read and as much
// as the budget allows is brought into memory, in this order: what earlier
// sessions of this map used (first-use order), the map-wide files, the tiles
// nearest the start, the map's AI vehicles, more tiles around the start. Then
// the program reports ready and OmsiLaunch starts OMSI. On the first open of
// a map nothing was learned yet: the module says so in its own window, and
// the session itself completes what the survey could not foresee.
//
// While OMSI runs (RunSession): the native component reports every file the
// game uses. The tiles around the player (estimated from the tile files OMSI
// opens) are held nearest first; the user's vehicles are held when the map is
// served; the vehicle files earlier sessions used are opened ahead of the
// game; when a vehicle preview starts, its folder is opened in parallel ahead
// of the game. A memory guard gives memory back when the system runs low.
//
// At the end the module remembers, for the next session of this map, what the
// game used and what it wrote.
internal sealed class Session
{
    private static readonly TimeSpan PrepareLimit = TimeSpan.FromMinutes(5);
    private readonly SessionContext context;
    private readonly PreloadSettings settings;
    private readonly Controller controller;
    private readonly Ui ui;
    private readonly FileIndex files;
    private readonly AssetResolver resolver;
    private readonly CancellationTokenSource stopping = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly List<Thread> workers = new();
    private readonly object touchGate = new();
    private readonly Queue<(int X, int Y, long Ms)> touches = new();
    private readonly AutoResetEvent touched = new(false);
    private readonly HashSet<string> raced = new(StringComparer.OrdinalIgnoreCase);
    private readonly ManualResetEventSlim vehiclesDone = new(false);
    private int touchGeneration;
    private MapModel? map;
    private MapStore? store;
    private List<string> learned = new();
    private List<Vehicle> aiVehicles = new();
    private IReadOnlyList<TileRef> startTiles = Array.Empty<TileRef>();
    private int savedTiles;

    public Session(SessionContext context, Controller controller, Ui ui)
    {
        this.context = context; this.controller = controller; this.ui = ui;
        settings = PreloadSettings.From(context.Settings);
        files = new FileIndex(context.InstallationRoot);
        resolver = new AssetResolver(files, settings.Sounds);
        Holdings = new Holdings(files, 0, Array.Empty<string>());
        ThreadPool.RegisterWaitForSingleObject(controller.Stop.WaitHandle, (_, _) => stopping.Cancel(), null, Timeout.Infinite, true);
    }

    public PreloadSettings Settings => settings;
    public Holdings Holdings { get; private set; }
    public TileIndex? Tiles { get; private set; }
    public Feed? Feed { get; private set; }
    public string Phase { get; private set; } = "survey";
    public bool FirstOpen { get; private set; }
    public string MapName => map?.Name ?? "";
    public (int X, int Y)? Center { get; private set; }
    public volatile bool PredictionBusy;
    public string Root => files.Root;

    // ---------------------------------------------------------------- before OMSI
    public void Prepare()
    {
        var memory = Native.Memory();
        var availableMb = (long)(memory.AvailPhys >> 20);
        var budget = settings.BudgetBytes(availableMb);
        // An explicit budget is the user's choice: loading then stops only when
        // less than 1 GB is free (2 GB with the automatic budget).
        Holdings = new Holdings(files, budget, context.SessionFiles) { StopBelowMb = settings.RamBudgetMb is null ? 2048 : 1024 };
        Log.Write("session " + context.SessionId + ", module " + context.ModuleVersion + ", OmsiLaunch " + context.ProductVersion);
        Log.Write("budget " + Vehicles.Mb(budget) + " (setting " + (settings.RamBudgetMb?.ToString(CultureInfo.InvariantCulture) ?? "auto") + "), free " + availableMb + " MB of " + (memory.TotalPhys >> 20) + " MB");
        if (budget > 0 && !Native.KeepResident(budget)) Log.Write("the working set minimum was not raised: held pages may be trimmed under memory pressure");
        if (settings.Monitor) ui.ShowMonitor(this);

        // The channel exists before ready: the native component connects as
        // soon as OMSI loads it.
        try { Feed = new Feed(context.Channel, () => controller.OmsiProcessId, OnFeed); Feed.Start(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Feed = null;
            Log.Write("the session channel cannot be served: " + exception.Message);
        }

        if (context.Map is null) { Log.Write("no map in this session: nothing to prepare"); return; }
        map = MapModel.Read(files, context.Map);
        if (map is null) { Log.Write("the map " + context.Map + " cannot be read: nothing to prepare"); return; }
        store = new MapStore(context.DataDirectory, files, map.Identity);
        Holdings.AddWritten(store.ReadList(store.WrittenPath));
        learned = store.ReadList(store.LearnedPath);
        Tiles = new TileIndex(map, resolver, map.Fingerprint());
        if (settings.Survey) Tiles.Load(store.TileIndexPath);
        savedTiles = Tiles.Computed;
        FirstOpen = learned.Count == 0 && !Tiles.Loaded;
        startTiles = map.StartTiles(context.WorldMode == "NewMap" ? context.EntrypointIndex : null);
        Log.Write("map " + map.Identity + " (" + map.Name + "): " + map.Tiles.Count + " tiles, " + map.Entrypoints.Count + " entrypoints; learned " + learned.Count + " files; tile index " +
            (Tiles.Loaded ? Tiles.Computed + " tiles from an earlier session" : "new") + "; start " + string.Join(" ", startTiles.Select(t => t.X + "," + t.Y)) + (FirstOpen ? "; first open" : ""));
        if (FirstOpen) ui.ShowFirstOpen(map.Name, () => Holdings.HeldBytes);
        if (budget == 0) { Log.Write("no memory to hold anything: preparation skipped"); return; }

        Phase = "prepare";
        var started = clock.Elapsed;
        var deadline = started + PrepareLimit;
        bool Go() => !stopping.IsCancellationRequested && clock.Elapsed < deadline && !Holdings.Full && !Holdings.MemoryLow;

        Step("learned", Tier.Learned, learned, Go);
        Step("map", Tier.Map, MapFiles(map), Go);
        if (settings.Survey) StepTiles("start tiles", Math.Min(2, settings.TileRadius), Go);
        if (settings.AiVehicles is { } scope && Go())
        {
            aiVehicles = Vehicles.MapAi(files, resolver, map, scope, Log.Write);
            foreach (var vehicle in aiVehicles) { if (!Go()) break; HoldVehicle(vehicle, Tier.AiVehicles); }
        }
        if (settings.Survey) StepTiles("tiles around the start", settings.TileRadius, Go);
        var summary = Holdings.Summarize();
        var elapsed = clock.Elapsed - started;
        Log.Write("prepared in " + elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s: " + summary.HeldFiles + " files, " + Vehicles.Mb(summary.Held) + " of " + Vehicles.Mb(summary.Budget) +
            (clock.Elapsed >= deadline ? " (time limit reached; the rest continues during the session)" : Holdings.Full ? " (budget reached)" : Holdings.MemoryLow ? " (free memory low)" : ""));
        controller.Event("prepared", new Dictionary<string, string>
        {
            ["held_mb"] = (summary.Held >> 20).ToString(CultureInfo.InvariantCulture), ["budget_mb"] = (summary.Budget >> 20).ToString(CultureInfo.InvariantCulture),
            ["files"] = summary.HeldFiles.ToString(CultureInfo.InvariantCulture), ["ms"] = ((long)elapsed.TotalMilliseconds).ToString(CultureInfo.InvariantCulture),
            ["first_open"] = FirstOpen ? "true" : "false"
        });
    }

    private IEnumerable<string> MapFiles(MapModel model)
    {
        foreach (var cited in model.Cited)
        {
            if (Text.EndsWithAny(cited, AssetResolver.Images)) { if (resolver.ResolveTexture(cited, new[] { model.Directory }, null) is { } texture) yield return texture; }
            else if (files.Full(cited) is { } full && files.IsFile(full)) foreach (var file in resolver.Scenery(full)) yield return file;
        }
        foreach (var file in model.MapWideFiles()) yield return file;
    }

    private void Step(string name, Tier tier, IEnumerable<string> paths, Func<bool> go)
    {
        var before = Holdings.HeldBytes;
        var held = 0;
        foreach (var path in paths)
        {
            if (!go()) break;
            var result = Holdings.Hold(path, tier);
            if (result == HoldResult.Held) held++;
            else if (result == HoldResult.LowMemory) { Holdings.MemoryLow = true; break; }
        }
        Log.Write("prepare: " + name + ": " + held + " files, " + Vehicles.Mb(Holdings.HeldBytes - before));
    }

    private void StepTiles(string name, int radius, Func<bool> go)
    {
        if (Tiles is null || map is null) return;
        var order = map.Tiles.Where(t => startTiles.Any(s => Math.Abs(t.X - s.X) <= radius && Math.Abs(t.Y - s.Y) <= radius))
            .OrderBy(t => startTiles.Min(s => (t.X - s.X) * (t.X - s.X) + (t.Y - s.Y) * (t.Y - s.Y))).ToList();
        var tiles = Tiles;
        Step(name + " (" + order.Count + " tiles)", Tier.Tiles, order.SelectMany(tile => go() ? tiles.Files(tile) : Array.Empty<string>()), go);
    }

    // A vehicle is held whole when what it still needs fits the budget.
    private void HoldVehicle(Vehicle vehicle, Tier tier)
    {
        var need = vehicle.Files.Where(f => !Holdings.IsHeld(f) && !Holdings.IsDenied(f)).Sum(files.Size);
        if (need == 0) return;
        if (Holdings.HeldBytes + need > Holdings.BudgetBytes) { Log.Write("vehicles: over the budget: " + vehicle.Folder + " needs " + Vehicles.Mb(need)); return; }
        long held = 0;
        foreach (var file in vehicle.Files)
        {
            if (stopping.IsCancellationRequested) return;
            var result = Holdings.Hold(file, tier);
            if (result == HoldResult.Held) held += files.Size(file);
            else if (result == HoldResult.LowMemory) { Holdings.MemoryLow = true; break; }
        }
        Log.Write("vehicles: held " + vehicle.Folder + " (" + vehicle.Scope.ToString().ToLowerInvariant() + "): " + Vehicles.Mb(held));
    }

    // ---------------------------------------------------------------- while OMSI runs
    public void RunSession()
    {
        Phase = "session";
        if (Tiles is not null && settings.Survey)
        {
            Start("survey", Survey, ThreadPriority.BelowNormal);
            Start("prediction", Prediction, ThreadPriority.Normal);
        }
        if (settings.UserVehicles != "none") Start("vehicles", UserVehicles, ThreadPriority.BelowNormal); else vehiclesDone.Set();
        if (settings.PreOpen != PreOpenMode.None) Start("pre-open", PreOpen, ThreadPriority.BelowNormal);
        Start("guard", Guard, ThreadPriority.Normal);
        while (!controller.Stop.Wait(TimeSpan.FromMinutes(2))) Save();
    }

    public void Shutdown()
    {
        Phase = "stopping";
        stopping.Cancel();
        touched.Set();
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(6);
        foreach (var worker in workers) worker.Join(Max(until - DateTime.UtcNow));
        Save();
        var summary = Holdings.Summarize();
        controller.Event("session", new Dictionary<string, string>
        {
            ["held_mb"] = (summary.Held >> 20).ToString(CultureInfo.InvariantCulture), ["used_held"] = summary.UsedHeld.ToString(CultureInfo.InvariantCulture),
            ["used_outside"] = summary.Outside.ToString(CultureInfo.InvariantCulture), ["feed_lines"] = (Feed?.Lines ?? 0).ToString(CultureInfo.InvariantCulture),
            ["feed_refused"] = (Feed?.Refused ?? 0).ToString(CultureInfo.InvariantCulture)
        });
        Log.Write("end: " + summary.HeldFiles + " files held (" + Vehicles.Mb(summary.Held) + "), used " + summary.UsedHeld + " of them (" + Vehicles.Mb(summary.UsedHeldBytes) + "), " + summary.Outside + " used outside memory (" + Vehicles.Mb(summary.OutsideBytes) + "); feed " + (Feed?.Lines ?? 0) + " lines");
        Feed?.Dispose();
        Holdings.Dispose();
    }

    private static TimeSpan Max(TimeSpan value) => value < TimeSpan.Zero ? TimeSpan.Zero : value;

    private void Start(string name, Action work, ThreadPriority priority)
    {
        var thread = new Thread(() =>
        {
            try { work(); }
            catch (Exception exception) { Log.Write(name + ": stopped by an unexpected error: " + exception); }
        }) { IsBackground = true, Name = name, Priority = priority };
        workers.Add(thread);
        thread.Start();
    }

    private void OnFeed(char kind, string path)
    {
        if (files.Full(path) is not { } full) return;
        switch (kind)
        {
            case 'U':
                if (Holdings.MarkUsed(full)) Race(full);
                break;
            case 'T':
                Holdings.MarkUsed(full);
                Touch(full);
                break;
            case 'W':
                Holdings.MarkWritten(full);
                break;
            case 'X':
                // OMSI cannot replace or truncate the file while it is mapped.
                if (Holdings.MarkWritten(full)) Log.Write("released at OMSI's request: " + files.Relative(full));
                break;
        }
    }

    // Tile prediction: the centroid of the tiles OMSI opened recently is the
    // estimated position (OMSI loads a window around the player and new tiles
    // appear at its leading edge, so the centroid follows the movement).
    private void Touch(string full)
    {
        var name = Path.GetFileName(full);
        if (!name.StartsWith("tile_", StringComparison.OrdinalIgnoreCase) || !name.EndsWith(".map", StringComparison.OrdinalIgnoreCase)) return;
        var parts = name[5..^4].Split('_');
        if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var x) || !int.TryParse(parts[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var y)) return;
        lock (touchGate)
        {
            touches.Enqueue((x, y, clock.ElapsedMilliseconds));
            while (touches.Count > 4096) touches.Dequeue();
            touchGeneration++;
        }
        touched.Set();
    }

    private void Prediction()
    {
        var seen = -1;
        var blocked = false;
        while (!stopping.IsCancellationRequested)
        {
            int generation;
            lock (touchGate) generation = touchGeneration;
            if (generation == seen) { touched.WaitOne(blocked ? 5000 : 2000); if (!blocked) continue; }
            seen = generation;
            double cx = 0, cy = 0;
            var count = 0;
            lock (touchGate)
            {
                var now = clock.ElapsedMilliseconds;
                foreach (var touch in touches.Reverse())
                {
                    if (now - touch.Ms > 60_000 && count >= 20) break;
                    cx += touch.X; cy += touch.Y; count++;
                }
            }
            if (count == 0) { touched.WaitOne(1000); seen = -1; continue; }
            cx /= count; cy /= count;
            Center = ((int)Math.Round(cx), (int)Math.Round(cy));
            PredictionBusy = true;
            blocked = false;
            var moved = false;
            foreach (var tile in Tiles!.Around(cx, cy, settings.TileRadius))
            {
                foreach (var file in Tiles.Files(tile))
                {
                    if (stopping.IsCancellationRequested) return;
                    var result = Holdings.Hold(file, Tier.Tiles);
                    if (result == HoldResult.LowMemory || Holdings.Full) { blocked = true; break; }
                }
                lock (touchGate) moved = touchGeneration != seen;
                if (blocked || moved) break;
            }
            PredictionBusy = false;
            // moved: the next pass starts at once; blocked: it retries after a
            // while (memory may come back); otherwise it waits for movement.
        }
    }

    private void Survey()
    {
        Tiles!.Survey(startTiles, stopping.Token);
        if (!stopping.IsCancellationRequested) Log.Write("survey: " + Tiles.Computed + " tiles indexed");
    }

    private void UserVehicles()
    {
        try
        {
            WaitWhilePredicting(TimeSpan.FromMinutes(2));
            var all = settings.UserVehicles == "all";
            var aiFolders = aiVehicles.Where(v => v.Root.StartsWith(Path.Combine(files.Root, "Vehicles"), StringComparison.OrdinalIgnoreCase)).Select(v => v.Folder).ToArray();
            var list = Vehicles.User(files, resolver, map, aiFolders, all, settings.UserVehicleList, settings.UserScope, settings.FleetScope, Log.Write);
            foreach (var vehicle in list)
            {
                if (stopping.IsCancellationRequested || Holdings.Full || Holdings.MemoryLow) break;
                WaitWhilePredicting(TimeSpan.FromSeconds(30));
                HoldVehicle(vehicle, Tier.UserVehicles);
            }
        }
        finally { vehiclesDone.Set(); }
    }

    private void WaitWhilePredicting(TimeSpan limit)
    {
        var until = clock.Elapsed + limit;
        while (PredictionBusy && clock.Elapsed < until && !stopping.IsCancellationRequested) Thread.Sleep(100);
    }

    // Opens (and closes) vehicle files the memory does not hold, ahead of the
    // game: the first open of a file costs a full read of it on a cold disk.
    private void PreOpen()
    {
        try { vehiclesDone.Wait(TimeSpan.FromMinutes(3), stopping.Token); }
        catch (OperationCanceledException) { return; }
        WaitWhilePredicting(TimeSpan.FromMinutes(1));
        var vehiclesDirectory = Path.Combine(files.Root, "Vehicles") + "\\";
        List<string> work;
        if (settings.PreOpen == PreOpenMode.Learned)
            work = learned.Concat(Holdings.UsedInOrder()).Where(f => f.StartsWith(vehiclesDirectory, StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        else
        {
            var aiFolders = aiVehicles.Select(v => v.Folder).ToArray();
            work = Vehicles.User(files, resolver, map, aiFolders, true, Array.Empty<string>(), VehicleScope.Load, VehicleScope.Load, Log.Write).SelectMany(v => v.Files).ToList();
        }
        work = work.Where(f => !Holdings.IsHeld(f) && !Holdings.IsDenied(f)).ToList();
        var result = OpenAhead(work, 2, 2048L << 20);
        Log.Write("pre-open: " + work.Count + " files: " + result);
    }

    // When a vehicle preview starts (the first use of its .bus/.ovh), its
    // folder is opened in parallel ahead of the game: scripts and models
    // first, then textures, repaints last; no sounds, no work files.
    private void Race(string full)
    {
        if (!settings.PreOpenOnFirstUse || !(Text.EndsWith(full, ".bus") || Text.EndsWith(full, ".ovh"))) return;
        var top = Vehicles.TopFolder(Path.Combine(files.Root, "Vehicles"), full);
        if (top is null || aiVehicles.Any(v => v.Folder.Equals(top, StringComparison.OrdinalIgnoreCase))) return;
        lock (raced) if (!raced.Add(top)) return;
        var thread = new Thread(() =>
        {
            try
            {
                var folder = Path.Combine(files.Root, "Vehicles", top);
                var all = FileIndex.ListFiles(folder, true, 20_000).Select(f => (Priority: RacePriority(f), Path: f)).Where(x => x.Priority >= 0).OrderBy(x => x.Priority).Select(x => x.Path).ToList();
                var held = all.Count(Holdings.IsHeld);
                var work = all.Where(f => !Holdings.IsHeld(f) && !Holdings.IsDenied(f)).ToList();
                if (work.Count == 0 || held * 10 >= all.Count * 9) return;
                Log.Write("first use of " + top + ": " + work.Count + " files opened ahead: " + OpenAhead(work, 4, 400L << 20));
            }
            catch (Exception exception) { Log.Write("first use of " + top + ": " + exception.Message); }
        }) { IsBackground = true, Name = "race", Priority = ThreadPriority.BelowNormal };
        thread.Start();
    }

    private static readonly string[] RaceSkipped = { ".wav", ".ogg", ".mp3", ".flac", ".ini" };

    private static int RacePriority(string path)
    {
        if (Text.EndsWithAny(path, RaceSkipped) || Text.EndsWithAny(path, AssetResolver.WorkFiles)) return -1;
        var lower = path.ToLowerInvariant();
        var repaint = lower.Contains("\\skins\\", StringComparison.Ordinal) || lower.Contains("\\pinturas", StringComparison.Ordinal) || lower.Contains("\\paint", StringComparison.Ordinal) || lower.Contains("\\lackierung", StringComparison.Ordinal);
        if (Text.EndsWithAny(path, AssetResolver.Images)) return repaint ? 4 : 3;
        return Text.EndsWith(path, ".o3d") ? 2 : 1;
    }

    // Opens and closes files on background-priority threads; an open slower
    // than 1.5 ms was cold, and cold bytes are capped.
    private string OpenAhead(IReadOnlyList<string> work, int threads, long coldLimit)
    {
        var next = -1;
        long cold = 0;
        int warmCount = 0, coldCount = 0, failed = 0, stop = 0;
        var started = clock.Elapsed;
        var pool = Enumerable.Range(0, threads).Select(_ => new Thread(() =>
        {
            Native.BackgroundIo(true);
            try
            {
                while (!stopping.IsCancellationRequested && Volatile.Read(ref stop) == 0)
                {
                    var i = Interlocked.Increment(ref next);
                    if (i >= work.Count) break;
                    if ((i & 15) == 0 && Native.AvailableMb() < Holdings.StopBelowMb) { Interlocked.Exchange(ref stop, 2); break; }
                    if (Interlocked.Read(ref cold) >= coldLimit) { Interlocked.Exchange(ref stop, 1); break; }
                    var watch = Stopwatch.StartNew();
                    using var handle = Native.CreateFileW(work[i], Native.GenericRead, Native.FileShareAll, 0, Native.OpenExisting, Native.FileAttributeNormal, 0);
                    var ms = watch.Elapsed.TotalMilliseconds;
                    if (handle.IsInvalid) { Interlocked.Increment(ref failed); continue; }
                    if (ms < 1.5) Interlocked.Increment(ref warmCount);
                    else { Interlocked.Increment(ref coldCount); Interlocked.Add(ref cold, Math.Max(0, files.Size(work[i]))); }
                }
            }
            finally { Native.BackgroundIo(false); }
        }) { IsBackground = true, Name = "open-ahead" }).ToList();
        foreach (var thread in pool) thread.Start();
        foreach (var thread in pool) thread.Join();
        return warmCount + " warm, " + coldCount + " cold (" + Vehicles.Mb(cold) + "), " + failed + " failed in " + (clock.Elapsed - started).TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s" +
            (stop == 1 ? "; cold data limit reached" : stop == 2 ? "; free memory low" : "");
    }

    // Gives memory back when the system runs low: files the game has not used
    // go first, the least valuable tiers first.
    private void Guard()
    {
        while (!stopping.Token.WaitHandle.WaitOne(2000))
        {
            var available = Native.AvailableMb();
            if (available < 1024)
            {
                Holdings.MemoryLow = true;
                var freed = Holdings.ReleaseUnused((1536 - available) << 20);
                Log.Write("guard: free memory " + available + " MB: released " + Vehicles.Mb(freed));
            }
            else if (available > 2560 && Holdings.MemoryLow) { Holdings.MemoryLow = false; Log.Write("guard: free memory " + available + " MB: loading resumes"); }
        }
    }

    // What the next session of this map starts from.
    private void Save()
    {
        if (store is null) return;
        try
        {
            if (settings.Learn)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var list = learned.Concat(Holdings.UsedInOrder()).Where(f => seen.Add(f) && !Holdings.IsDenied(f) && files.IsFile(f)).Take(200_000).ToList();
                store.WriteLearned(list);
                store.WriteWritten(Holdings.Written());
            }
            if (settings.Survey && Tiles is not null && Tiles.Computed > savedTiles)
            {
                Tiles.Save(store.TileIndexPath);
                savedTiles = Tiles.Computed;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { Log.Write("save: " + exception.Message); }
    }
}
