using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace OmsiLaunch.Modules.Prefetch;

// The files each tile of the map needs: its own files, then every object and
// spline it places with what they cite (tools/tile_index.py of the PoC, now
// computed by the module). A tile is computed when it is first needed; the
// survey thread computes the rest in the background. Saved per map with the
// map's fingerprint, so a later session reuses it while the map is unchanged.
internal sealed class TileIndex
{
    private const string Header = "# omsilaunch prefetch tile index v1";
    private readonly MapModel map;
    private readonly AssetResolver resolver;
    private readonly ConcurrentDictionary<int, string[]> tiles = new();
    private readonly Dictionary<(int X, int Y), TileRef> positions = new();

    public TileIndex(MapModel map, AssetResolver resolver, string fingerprint)
    {
        this.map = map; this.resolver = resolver; Fingerprint = fingerprint;
        foreach (var tile in map.Tiles) positions.TryAdd((tile.X, tile.Y), tile);
    }

    public string Fingerprint { get; }
    public int Computed => tiles.Count;
    public int Total => map.Tiles.Count;
    public bool Loaded { get; private set; }

    public string[] Files(TileRef tile) => tiles.GetOrAdd(tile.Index, _ => Compute(tile));

    public bool IsComputed(TileRef tile) => tiles.ContainsKey(tile.Index);

    public TileRef? At(int x, int y) => positions.TryGetValue((x, y), out var tile) ? tile : null;

    // Tiles within radius (in tiles, square) of a point, nearest first.
    public List<TileRef> Around(double x, double y, int radius) => map.Tiles
        .Where(t => Math.Abs(t.X - x) <= radius && Math.Abs(t.Y - y) <= radius)
        .OrderBy(t => (t.X - x) * (t.X - x) + (t.Y - y) * (t.Y - y)).ThenBy(t => t.Index).ToList();

    private string[] Compute(TileRef tile)
    {
        var output = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var own in map.TileOwnFiles(tile)) if (seen.Add(own)) output.Add(own);
        var path = Path.Combine(map.Directory, tile.File);
        foreach (var token in Text.Tokens(Text.ReadLines(path, Text.TileLimit)))
        {
            if (!Text.EndsWith(token, ".sco") && !Text.EndsWith(token, ".sli")) continue;
            if (resolver.ResolveReference(token, resolver.Files.Root, null) is not { } asset) continue;
            foreach (var file in resolver.Scenery(asset)) if (seen.Add(file)) output.Add(file);
        }
        return output.ToArray();
    }

    // Computes every tile not computed yet, nearest to the start first.
    public void Survey(IReadOnlyList<TileRef> start, CancellationToken cancel)
    {
        var order = start.Count == 0 ? map.Tiles.ToList() : map.Tiles.OrderBy(t => start.Min(s => (t.X - s.X) * (t.X - s.X) + (t.Y - s.Y) * (t.Y - s.Y))).ToList();
        foreach (var tile in order)
        {
            if (cancel.IsCancellationRequested) return;
            Files(tile);
        }
    }

    public void Load(string path)
    {
        var lines = Text.ReadLines(path, 512L << 20);
        if (lines.Count < 2 || lines[0] != Header || lines[1] != "fingerprint " + Fingerprint) return;
        var table = new List<string?>();
        foreach (var line in lines.Skip(2))
        {
            var fields = line.Split('\t');
            if (fields[0] == "F" && fields.Length == 3 && int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id == table.Count)
                table.Add(resolver.Files.Full(fields[2]));
            else if (fields[0] == "T" && fields.Length == 3 && int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var index) && map.Tile(index) is { } tile)
            {
                var list = new List<string>();
                if (fields[2].Length > 0)
                    foreach (var item in fields[2].Split(','))
                        if (int.TryParse(item, NumberStyles.None, CultureInfo.InvariantCulture, out var file) && file < table.Count && table[file] is { } full) list.Add(full);
                tiles[tile.Index] = list.ToArray();
            }
        }
        Loaded = tiles.Count > 0;
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var snapshot = tiles.OrderBy(x => x.Key).ToArray();
        var temporary = path + ".tmp";
        using (var writer = new StreamWriter(temporary, false, new UTF8Encoding(false)))
        {
            writer.NewLine = "\n";
            writer.WriteLine(Header);
            writer.WriteLine("fingerprint " + Fingerprint);
            foreach (var (_, files) in snapshot)
                foreach (var file in files)
                    if (!ids.ContainsKey(file)) { ids[file] = ids.Count; writer.WriteLine("F\t" + ids[file].ToString(CultureInfo.InvariantCulture) + "\t" + resolver.Files.Relative(file)); }
            foreach (var (index, files) in snapshot)
                writer.WriteLine("T\t" + index.ToString(CultureInfo.InvariantCulture) + "\t" + string.Join(",", files.Select(f => ids[f].ToString(CultureInfo.InvariantCulture))));
        }
        File.Move(temporary, path, overwrite: true);
    }
}
