using System.Diagnostics;

namespace OmsiLaunch.Modules.Prefetch;

// Where a held file came from, in the order the budget is spent (and, in
// reverse, released under memory pressure).
internal enum Tier { Learned, Map, Tiles, AiVehicles, UserVehicles }

internal enum HoldState { Held, Outside, Released }

internal enum HoldResult { Held, Known, Budget, LowMemory, Unusable, Denied }

internal sealed class Holding
{
    public Holding(string path, long size, Tier tier) { Path = path; Size = size; Tier = tier; }
    public string Path { get; }
    public long Size { get; set; }
    public Tier Tier { get; set; }
    public HoldState State { get; set; }
    public bool Used { get; set; }
    public long UsedAtMs { get; set; }
    public bool Written { get; set; }
    public Native.View? View { get; set; }
}

// The files held in RAM. Each is mapped read-only and every page touched, and
// the views are kept, so the pages belong to this process (the amount held
// is exact and comparable with the budget) and Windows does not give them to
// something else while the game reads the same files through its own opens.
// A file the game writes is never held: the session program learns it from
// the use reports (and from earlier sessions), and releases it at once when
// OMSI asks (a mapped file cannot be truncated).
internal sealed class Holdings : IDisposable
{
    // The game rewrites these while it runs: it truncates laststn.osn and the
    // like when it closes the map, and it streams tile_X_Y.map.prt and
    // tile_X_Y.map.LM.bmp (and .owt) while it loads the tiles. A file mapped by
    // this process cannot be truncated (ERROR_USER_MAPPED_FILE, 1224): the
    // game stalls or its tile loader thread dies and half the map never loads
    // (the dump of 2026-10-12 and the PoC v2.1, which denies the same
    // extensions). Never held, whatever else is learned about the map.
    private static readonly string[] Denied = { ".osn", ".rdy", ".tbmp", ".odr", ".tmp", ".log", ".sav", ".bak", ".owt", ".prt", ".lm.bmp" };
    private readonly object gate = new();
    private readonly FileIndex files;
    private readonly Dictionary<string, Holding> index = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> written = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> sessionFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> usedOrder = new();
    private readonly long[] tierBytes = new long[5];
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private long held;
    private volatile bool memoryLow;
    private bool disposed;

    public Holdings(FileIndex files, long budgetBytes, IEnumerable<string> sessionFiles)
    {
        this.files = files;
        BudgetBytes = budgetBytes;
        foreach (var file in sessionFiles) if (files.Full(file) is { } full) this.sessionFiles.Add(full);
    }

    public long BudgetBytes { get; }
    public long StopBelowMb { get; init; } = 2048;
    public long HeldBytes { get { lock (gate) return held; } }
    public long TierBytes(Tier tier) { lock (gate) return tierBytes[(int)tier]; }
    public bool MemoryLow { get => memoryLow; set => memoryLow = value; }
    public bool Full { get { lock (gate) return held >= BudgetBytes - (1L << 20); } }

    // Files the game rewrites (laststn.osn, the .rdy terrain cache, logs ...).
    public static bool DeniedExtension(string path) => Text.EndsWithAny(path, Denied);

    public bool IsDenied(string full)
    {
        lock (gate) return written.Contains(full) || sessionFiles.Contains(full) || DeniedExtension(full);
    }

    public bool IsHeld(string full)
    {
        lock (gate) return index.TryGetValue(full, out var holding) && holding.State == HoldState.Held;
    }

    public void AddWritten(IEnumerable<string> fullPaths)
    {
        lock (gate) foreach (var full in fullPaths) written.Add(full);
    }

    public HoldResult Hold(string full, Tier tier, long tierLimit = long.MaxValue)
    {
        if (disposed) return HoldResult.Unusable;
        if (IsHeld(full)) return HoldResult.Known;
        if (IsDenied(full)) return HoldResult.Denied;
        if (memoryLow || Native.AvailableMb() < StopBelowMb) return HoldResult.LowMemory;
        var size = files.Size(full);
        if (size <= 0) return HoldResult.Unusable;
        lock (gate)
        {
            if (held + size > BudgetBytes || tierBytes[(int)tier] + size > tierLimit) return HoldResult.Budget;
            held += size;   // reserved while mapping
        }
        var view = Native.View.Map(full, out var actual, out _);
        if (view is null) { lock (gate) held -= size; return HoldResult.Unusable; }
        view.Touch();
        lock (gate)
        {
            held -= size;
            // The game wrote it, or another tier held it, while it was mapped.
            if (disposed || written.Contains(full)) { view.Release(); return HoldResult.Denied; }
            var known = index.TryGetValue(full, out var holding);
            if (known && holding!.State == HoldState.Held) { view.Release(); return HoldResult.Known; }
            holding ??= new Holding(full, actual, tier);
            holding.Size = actual; holding.Tier = tier; holding.State = HoldState.Held; holding.View = view;
            index[full] = holding;
            held += actual;
            tierBytes[(int)tier] += actual;
        }
        return HoldResult.Held;
    }

    // First use of a file by the game (U lines). Returns true the first time.
    public bool MarkUsed(string full)
    {
        lock (gate)
        {
            if (index.TryGetValue(full, out var holding))
            {
                if (holding.Used) return false;
                holding.Used = true; holding.UsedAtMs = clock.ElapsedMilliseconds;
                usedOrder.Add(full);
                return true;
            }
        }
        var size = files.Size(full);
        if (size < 0) return false;
        lock (gate)
        {
            if (index.ContainsKey(full)) return false;
            index[full] = new Holding(full, size, Tier.Learned) { State = HoldState.Outside, Used = true, UsedAtMs = clock.ElapsedMilliseconds };
            usedOrder.Add(full);
            return true;
        }
    }

    // The game wrote to a file (W lines), or asks for it to be released
    // because it cannot replace it (X lines): never hold it again.
    public bool MarkWritten(string full)
    {
        lock (gate)
        {
            written.Add(full);
            if (!index.TryGetValue(full, out var holding)) return false;
            holding.Written = true;
            return ReleaseLocked(holding);
        }
    }

    private bool ReleaseLocked(Holding holding)
    {
        if (holding.State != HoldState.Held || holding.View is null) return false;
        holding.View.Release();
        holding.View = null;
        holding.State = HoldState.Released;
        held -= holding.Size;
        tierBytes[(int)holding.Tier] -= holding.Size;
        return true;
    }

    // Under memory pressure: releases files the game has not used, the least
    // valuable tiers first, until about wantBytes were given back.
    public long ReleaseUnused(long wantBytes)
    {
        long freed = 0;
        lock (gate)
        {
            foreach (var holding in index.Values.Where(x => x.State == HoldState.Held && !x.Used).OrderByDescending(x => x.Tier).ThenByDescending(x => x.Size).ToList())
            {
                if (freed >= wantBytes) break;
                var size = holding.Size;
                if (ReleaseLocked(holding)) freed += size;
            }
        }
        return freed;
    }

    public List<string> UsedInOrder() { lock (gate) return usedOrder.ToList(); }
    public List<string> Written() { lock (gate) return written.ToList(); }

    public sealed record Summary(long Held, long Budget, int HeldFiles, int UsedHeld, long UsedHeldBytes, int UnusedHeld, long UnusedHeldBytes, int Outside, long OutsideBytes, long[] Tiers);

    public Summary Summarize()
    {
        lock (gate)
        {
            int heldFiles = 0, usedHeld = 0, unusedHeld = 0, outside = 0;
            long usedBytes = 0, unusedBytes = 0, outsideBytes = 0;
            foreach (var holding in index.Values)
            {
                if (holding.State == HoldState.Held)
                {
                    heldFiles++;
                    if (holding.Used) { usedHeld++; usedBytes += holding.Size; } else { unusedHeld++; unusedBytes += holding.Size; }
                }
                else if (holding.State == HoldState.Outside) { outside++; outsideBytes += holding.Size; }
            }
            return new Summary(held, BudgetBytes, heldFiles, usedHeld, usedBytes, unusedHeld, unusedBytes, outside, outsideBytes, tierBytes.ToArray());
        }
    }

    public List<Holding> Snapshot() { lock (gate) return index.Values.ToList(); }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            foreach (var holding in index.Values) ReleaseLocked(holding);
        }
    }
}
