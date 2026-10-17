using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OmsiLaunch.Modules.Prefetch;

internal sealed record TileRef(int Index, int X, int Y, string File);
internal sealed record EntrypointRef(int Ordinal, int TileIndex, string Name);

// What global.cfg says about a map: its tiles ([map]: x, y, file), its
// entrypoints ([entrypoints]: a count, then twelve lines each: object, id, 0,
// position (3), rotation (4), the index of its tile in the [map] list, and
// the label; checked on Grundorf and Berlin-Spandau) and the files it cites
// (ground textures, money system, ticket pack).
internal sealed class MapModel
{
    public string Identity { get; private init; } = "";
    public string Directory { get; private init; } = "";
    public string GlobalConfig { get; private init; } = "";
    public string Name { get; private init; } = "";
    public IReadOnlyList<TileRef> Tiles { get; private init; } = Array.Empty<TileRef>();
    public IReadOnlyList<EntrypointRef> Entrypoints { get; private init; } = Array.Empty<EntrypointRef>();
    public IReadOnlyList<string> Cited { get; private init; } = Array.Empty<string>();

    // identity: "maps\<folder>\global.cfg", relative to the installation.
    public static MapModel? Read(FileIndex files, string identity)
    {
        var global = files.Full(identity);
        if (global is null || !files.IsFile(global) || !Path.GetFileName(global).Equals("global.cfg", StringComparison.OrdinalIgnoreCase)) return null;
        var directory = Path.GetDirectoryName(global)!;
        var lines = Text.ReadLines(global, 64L << 20);
        var tiles = new List<TileRef>();
        var entrypoints = new List<EntrypointRef>();
        var cited = new List<string>();
        var name = Path.GetFileName(directory);
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Length == 0 || line[0] != '[') continue;
            switch (line.ToLowerInvariant())
            {
                case "[map]":
                    // x, y, then the tile file (later builds may put more lines first)
                    if (i + 3 < lines.Count && int.TryParse(lines[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) && int.TryParse(lines[i + 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
                    {
                        var file = lines.Skip(i + 3).Take(4).TakeWhile(l => l.Length == 0 || l[0] != '[').FirstOrDefault(l => Text.EndsWith(l, ".map"));
                        if (file is not null) tiles.Add(new TileRef(tiles.Count, x, y, file));
                    }
                    break;
                case "[entrypoints]":
                    if (i + 1 < lines.Count && int.TryParse(lines[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count is > 0 and <= 4096)
                    {
                        var cursor = i + 2;
                        for (var ordinal = 0; ordinal < count && cursor + 11 < lines.Count; ordinal++, cursor += 12)
                            if (int.TryParse(lines[cursor + 10], NumberStyles.Integer, CultureInfo.InvariantCulture, out var tile)) entrypoints.Add(new EntrypointRef(ordinal, tile, lines[cursor + 11]));
                    }
                    break;
                case "[friendlyname]":
                    if (i + 1 < lines.Count && lines[i + 1].Length > 0) name = lines[i + 1];
                    break;
                case "[groundtex]":
                    for (var k = 1; k <= 2 && i + k < lines.Count; k++) if (Text.EndsWithAny(lines[i + k], AssetResolver.Images)) cited.Add(lines[i + k]);
                    break;
                case "[moneysystem]":
                case "[ticketpack]":
                    if (i + 1 < lines.Count && lines[i + 1].Length > 0) cited.Add(lines[i + 1]);
                    break;
            }
        }
        return new MapModel
        {
            Identity = files.Relative(global), Directory = directory, GlobalConfig = global, Name = name,
            Tiles = tiles, Entrypoints = entrypoints, Cited = cited
        };
    }

    public TileRef? Tile(int index) => index >= 0 && index < Tiles.Count ? Tiles[index] : null;

    // Where the session starts. OMSI's presented entrypoint list is not known
    // offline, so the file order and the name order are both tried; a saved
    // situation (or no index) starts from every entrypoint.
    public IReadOnlyList<TileRef> StartTiles(int? presentedIndex)
    {
        var result = new List<TileRef>();
        void AddEntry(EntrypointRef? entry) { if (entry is not null && Tile(entry.TileIndex) is { } tile && !result.Contains(tile)) result.Add(tile); }
        if (presentedIndex is { } index && index >= 0 && index < Entrypoints.Count)
        {
            AddEntry(Entrypoints[index]);
            AddEntry(Entrypoints.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ElementAt(index));
        }
        else foreach (var entry in Entrypoints) AddEntry(entry);
        if (result.Count == 0 && Tiles.Count > 0)
        {
            // No entrypoint: the tile nearest the middle of the map.
            var cx = Tiles.Average(t => t.X); var cy = Tiles.Average(t => t.Y);
            result.Add(Tiles.MinBy(t => (t.X - cx) * (t.X - cx) + (t.Y - cy) * (t.Y - cy))!);
        }
        return result;
    }

    // Files of the map folder needed wherever the player is: everything but
    // the tiles' own files, the per-tile textures, caches and chrono variants.
    public List<string> MapWideFiles()
    {
        var result = new List<string>();
        foreach (var file in FileIndex.ListFiles(Directory, true, 50_000))
        {
            var relative = file[(Directory.Length + 1)..];
            var lower = relative.ToLowerInvariant();
            if (lower.StartsWith("tile_", StringComparison.Ordinal) || lower.StartsWith("chrono\\", StringComparison.Ordinal) || lower.StartsWith("texture\\map\\", StringComparison.Ordinal)) continue;
            if (Holdings.DeniedExtension(lower) || Text.EndsWithAny(lower, AssetResolver.WorkFiles)) continue;
            result.Add(file);
        }
        return result;
    }

    // A tile's own files: tile_X_Y.map and its companions (.terrain, .water,
    // .LM.bmp, .prt ...; never the .rdy cache), and its texture\map files.
    public List<string> TileOwnFiles(TileRef tile)
    {
        var result = new List<string>();
        var stem = tile.File;
        foreach (var folder in new[] { Directory, Path.Combine(Directory, "texture", "map") })
        {
            if (!System.IO.Directory.Exists(folder)) continue;
            try
            {
                foreach (var file in System.IO.Directory.EnumerateFiles(folder, stem + "*"))
                    if (!Holdings.DeniedExtension(file.ToLowerInvariant())) result.Add(file);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
        // the tile file itself first: it is what OMSI opens first
        return result.OrderBy(x => Path.GetFileName(x).Equals(stem, StringComparison.OrdinalIgnoreCase) ? 0 : 1).ToList();
    }

    // Changes when global.cfg or any tile file changes (size or time).
    public string Fingerprint()
    {
        var text = new StringBuilder("v1|");
        void Stamp(string path)
        {
            var info = new FileInfo(path);
            text.Append(Path.GetFileName(path)).Append('|');
            if (info.Exists) text.Append(info.Length.ToString(CultureInfo.InvariantCulture)).Append('|').Append(info.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture));
            text.Append(';');
        }
        Stamp(GlobalConfig);
        foreach (var tile in Tiles) Stamp(Path.Combine(Directory, tile.File));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
}

// What the module remembers about one map, in its data directory
// (maps\<folder>-<hash>\): the files sessions used, in first-use order
// (learned.txt), the files the game wrote (written.txt), and the tile index
// (tiles.idx). Paths are stored relative to the installation.
internal sealed class MapStore
{
    private const string LearnedHeader = "# omsilaunch prefetch: files the game used on this map, in first-use order";
    private const string WrittenHeader = "# omsilaunch prefetch: files the game wrote: never held in memory";
    private readonly FileIndex files;

    public MapStore(string dataDirectory, FileIndex files, string mapIdentity)
    {
        this.files = files;
        var folder = Path.GetFileName(Path.GetDirectoryName(mapIdentity.Replace('/', '\\')) ?? "map");
        var safe = new string(folder.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_').ToArray());
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(mapIdentity.Replace('/', '\\').ToLowerInvariant())))[..8].ToLowerInvariant();
        Directory = Path.Combine(dataDirectory, "maps", (safe.Length > 40 ? safe[..40] : safe) + "-" + hash);
    }

    public string Directory { get; }
    public string LearnedPath => Path.Combine(Directory, "learned.txt");
    public string WrittenPath => Path.Combine(Directory, "written.txt");
    public string TileIndexPath => Path.Combine(Directory, "tiles.idx");

    public List<string> ReadList(string path)
    {
        var result = new List<string>();
        foreach (var line in Text.ReadLines(path, 64L << 20))
        {
            if (line.Length == 0 || line[0] == '#') continue;
            if (files.Full(line) is { } full) result.Add(full);
        }
        return result;
    }

    public void WriteList(string path, string header, IEnumerable<string> fullPaths)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var temporary = path + ".tmp";
        using (var writer = new StreamWriter(temporary, false, new UTF8Encoding(false)))
        {
            writer.NewLine = "\n";
            writer.WriteLine(header);
            foreach (var full in fullPaths) writer.WriteLine(files.Relative(full));
        }
        File.Move(temporary, path, overwrite: true);
    }

    // Keeps the module's data directory under limitBytes: when the maps' data
    // (learned lists, tile indexes) are larger, the maps used least recently go
    // first, never the current one. Returns the bytes removed. The logs have
    // their own bounds (Log.MaxFileBytes, Log.KeptFiles).
    public static long Prune(string dataDirectory, long limitBytes, string keepDirectory)
    {
        var maps = Path.Combine(dataDirectory, "maps");
        if (!System.IO.Directory.Exists(maps)) return 0;
        var folders = new List<(string Path, long Bytes, DateTime Used)>();
        try
        {
            foreach (var folder in System.IO.Directory.EnumerateDirectories(maps))
            {
                long bytes = 0; var used = DateTime.MinValue;
                foreach (var file in new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories)) { bytes += file.Length; if (file.LastWriteTimeUtc > used) used = file.LastWriteTimeUtc; }
                folders.Add((folder, bytes, used));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return 0; }
        var total = folders.Sum(x => x.Bytes);
        long removed = 0;
        foreach (var folder in folders.Where(x => !string.Equals(Path.GetFullPath(x.Path), Path.GetFullPath(keepDirectory), StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.Used))
        {
            if (total <= limitBytes) break;
            try { System.IO.Directory.Delete(folder.Path, recursive: true); total -= folder.Bytes; removed += folder.Bytes; }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
        return removed;
    }

    public void WriteLearned(IEnumerable<string> fullPaths) => WriteList(LearnedPath, LearnedHeader, fullPaths);
    public void WriteWritten(IEnumerable<string> fullPaths) => WriteList(WrittenPath, WrittenHeader, fullPaths);
}
