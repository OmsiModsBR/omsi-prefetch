using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using OmsiLaunch.Process;
using OmsiLaunch.Runtime.Modules;

namespace OmsiLaunch.Modules.Prefetch.Tests;

// Prefetch.Tests [--package <artifacts\modules\omsi-prefetch>]
// Unit gates on a synthetic installation, then the session program end to
// end (the packaged one when --package is given).
internal static class Program
{
    private static int failures;

    private static int Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "--survey") return Survey(args[1], args[2]);
        var package = args.Length == 2 && args[0] == "--package" ? Path.GetFullPath(args[1]) : null;
        Gate("text.encodings-and-tokens", TextGate);
        Gate("text.o3d-texture-names", O3dGate);
        Gate("settings.budget", BudgetGate);
        Gate("map.global-cfg", MapGate);
        Gate("tiles.closure-and-index", TilesGate);
        Gate("vehicles.scopes", VehiclesGate);
        Gate("holdings.hold-release", HoldingsGate);
        Gate("holdings.game-streamed-files-never-held", StreamedFilesGate);
        Gate("feed.release-is-never-queued", FeedReleaseGate);
        if (package is not null) Gate("package.manifest", () => PackageGate(package));
        var exe = package is not null ? Path.Combine(package, "Prefetch.Preloader.exe") : Path.Combine(AppContext.BaseDirectory, "Prefetch.Preloader.exe");
        Gate("session.end-to-end", () => EndToEnd(exe));
        Console.WriteLine(failures == 0 ? "all gates passed" : failures + " gate(s) failed");
        return failures == 0 ? 0 : 1;
    }

    // Prefetch.Tests --survey <OMSI folder> <maps\<map>\global.cfg>
    // Reads a real installation the way the session program does before OMSI
    // (map, every tile, AI and user vehicles) and reports what it found and how
    // long it took. Read-only: nothing is mapped, held or written.
    private static int Survey(string root, string mapIdentity)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        string Seconds() => clock.Elapsed.TotalSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s";
        var files = new FileIndex(root);
        var map = MapModel.Read(files, mapIdentity);
        if (map is null) { Console.WriteLine("the map cannot be read"); return 1; }
        long Bytes(IEnumerable<string> paths) => paths.Distinct(StringComparer.OrdinalIgnoreCase).Sum(p => Math.Max(0, files.Size(p)));
        Console.WriteLine("map " + map.Identity + " (" + map.Name + "): " + map.Tiles.Count + " tiles, " + map.Entrypoints.Count + " entrypoints; cited " + string.Join(", ", map.Cited));
        Console.WriteLine("start (entrypoint 0): " + string.Join(" ", map.StartTiles(0).Select(t => t.X + "," + t.Y)) + "; entrypoints: " + string.Join(" | ", map.Entrypoints.Take(5).Select(e => e.Name + "@" + e.TileIndex)));
        var wide = map.MapWideFiles();
        Console.WriteLine("map-wide: " + wide.Count + " files, " + Vehicles.Mb(Bytes(wide)) + " (" + Seconds() + ")");
        var resolver = new AssetResolver(files, true);
        var index = new TileIndex(map, resolver, map.Fingerprint());
        clock.Restart();
        index.Survey(map.StartTiles(0), CancellationToken.None);
        var all = map.Tiles.SelectMany(index.Files).ToList();
        var perTile = map.Tiles.Select(t => index.Files(t).Length).ToList();
        Console.WriteLine("tiles: " + index.Computed + " indexed in " + Seconds() + "; " + all.Distinct(StringComparer.OrdinalIgnoreCase).Count() + " distinct files, " + Vehicles.Mb(Bytes(all)) +
            "; files per tile min " + perTile.DefaultIfEmpty().Min() + " max " + perTile.DefaultIfEmpty().Max());
        var start = map.StartTiles(0).FirstOrDefault();
        if (start is not null)
            foreach (var radius in new[] { 0, 1, 2, 4 })
                Console.WriteLine("  around the start, radius " + radius + ": " + Vehicles.Mb(Bytes(index.Around(start.X, start.Y, radius).SelectMany(index.Files))));
        var sample = index.Files(map.Tiles[0]).Take(12).Select(files.Relative);
        Console.WriteLine("  first files of " + map.Tiles[0].File + ": " + string.Join(", ", sample));
        clock.Restart();
        var ai = Vehicles.MapAi(files, resolver, map, VehicleScope.Complete, Console.WriteLine);
        Console.WriteLine("AI (complete): " + ai.Count + " entries, " + Vehicles.Mb(ai.Sum(v => v.Bytes)) + " (" + Seconds() + "): " + string.Join(", ", ai.Take(8).Select(v => v.Folder + " " + Vehicles.Mb(v.Bytes))));
        clock.Restart();
        var aiFolders = ai.Select(v => v.Folder).ToArray();
        var user = Vehicles.User(files, resolver, map, aiFolders, true, Array.Empty<string>(), VehicleScope.Load, VehicleScope.Load, Console.WriteLine);
        Console.WriteLine("user vehicles (load): " + user.Count + ", " + Vehicles.Mb(user.Sum(v => v.Bytes)) + " (" + Seconds() + "): " + string.Join(", ", user.Take(8).Select(v => v.Folder + " " + Vehicles.Mb(v.Bytes) + (v.MapHof ? " [fleet]" : ""))));
        return 0;
    }

    private static void Gate(string name, Action body)
    {
        try { body(); Console.WriteLine("PASS " + name); }
        catch (Exception exception) { failures++; Console.WriteLine("FAIL " + name + ": " + exception.Message); }
    }

    private static void Check(bool condition, string what)
    {
        if (!condition) throw new InvalidOperationException(what);
    }

    private static void TextGate()
    {
        Check(Text.Decode(new byte[] { 0xFF, 0xFE, (byte)'a', 0, 0xE9, 0 }) == "aé", "UTF-16 LE");
        Check(Text.Decode(new byte[] { 0xEF, 0xBB, 0xBF, (byte)'a', 0xC3, 0xA9 }) == "aé", "UTF-8 with BOM");
        Check(Text.Decode(new byte[] { (byte)'a', 0xE9 }) == "aé", "ANSI");
        var lines = Text.Lines("a\r\nb\nc\rd\r\n\r\ne");
        Check(lines.SequenceEqual(new[] { "a", "b", "c", "d", "", "e" }), "line breaks: " + string.Join("|", lines));
        Check(Text.Lines("x\r\n").Count == 1 && Text.Lines("").Count == 0, "trailing line break");
        Check(Text.LineTokens("model\\a.o3d\t1\t\"tex.bmp\" and \"b.bmp\"").SequenceEqual(new[] { "model\\a.o3d", "tex.bmp", "b.bmp" }), "tokens");
        Check(!Text.LineTokens("[mesh]").Any(), "section lines have no tokens");
    }

    private static void O3dGate()
    {
        var path = Path.Combine(Path.GetTempPath(), "loading-o3d-" + Guid.NewGuid().ToString("N") + ".o3d");
        try
        {
            File.WriteAllBytes(path, Fixture.O3d(5000, "Some Texture.DDS"));
            Check(Text.O3dTextures(path).SequenceEqual(new[] { "Some Texture.DDS" }), "one length-prefixed name");
            File.WriteAllBytes(path, Fixture.O3d(5000, null));
            Check(Text.O3dTextures(path).Count == 0, "no name");
        }
        finally { File.Delete(path); }
    }

    private static void BudgetGate()
    {
        var auto = PreloadSettings.From(new Dictionary<string, string> { ["ram_budget_mb"] = "auto" });
        Check(auto.BudgetBytes(20_000) == (long)(0.7 * (20_000 - 4096)) << 20, "auto: 70% of free after 4 GB");
        Check(auto.BudgetBytes(40_000) == 12288L << 20, "auto: at most 12 GB");
        Check(auto.BudgetBytes(3000) == 0, "auto: nothing when little is free");
        var fixedBudget = PreloadSettings.From(new Dictionary<string, string> { ["ram_budget_mb"] = "5000" });
        Check(fixedBudget.BudgetBytes(1000) == 5000L << 20, "explicit value");
        var settings = PreloadSettings.From(new Dictionary<string, string> { ["ai_vehicles"] = "off", ["user_vehicles"] = "list", ["user_vehicle_list"] = "A; B ;", ["pre_open"] = "none", ["monitor"] = "true" });
        Check(settings.AiVehicles is null && settings.UserVehicleList.SequenceEqual(new[] { "A", "B" }) && settings.PreOpen == PreOpenMode.None && settings.Monitor, "choices and lists");
        var defaults = PreloadSettings.From(new Dictionary<string, string>());
        Check(defaults.AiVehicles == VehicleScope.Complete && defaults.UserVehicles == "none" && defaults.PreOpen == PreOpenMode.Learned && !defaults.Monitor && defaults.Learn && defaults.Survey && defaults.TileRadius == 12, "defaults");
    }

    private static void MapGate()
    {
        using var fixture = new Fixture();
        var files = new FileIndex(fixture.Root);
        var map = MapModel.Read(files, "maps\\Test\\global.cfg") ?? throw new InvalidOperationException("map not read");
        Check(map.Name == "Test Map", "friendly name");
        Check(map.Tiles.Select(t => (t.X, t.Y, t.File)).SequenceEqual(new[] { (0, 0, "tile_0_0.map"), (1, 0, "tile_1_0.map"), (5, 5, "tile_5_5.map") }), "tiles");
        Check(map.Entrypoints.Count == 2 && map.Entrypoints[0].TileIndex == 0 && map.Entrypoints[1].Name == "Alpha Stop", "entrypoints");
        Check(map.StartTiles(0).Select(t => t.Index).SequenceEqual(new[] { 0, 1 }), "start: file order then name order");
        Check(map.StartTiles(1).Select(t => t.Index).SequenceEqual(new[] { 1, 0 }), "start: index 1");
        Check(map.StartTiles(null).Count == 2, "start: every entrypoint");
        Check(map.Cited.SequenceEqual(new[] { "texture\\ground.bmp", "texture\\ground_det.bmp" }), "ground textures");
        var wide = map.MapWideFiles().Select(files.Relative).ToList();
        Check(wide.Contains("maps\\Test\\global.cfg") && wide.Contains("maps\\Test\\TTData\\line.ttl") && wide.Contains("maps\\Test\\ailists.cfg"), "map-wide files");
        Check(!wide.Any(x => x.Contains("tile_", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".osn", StringComparison.OrdinalIgnoreCase)), "map-wide files: no tiles, no .osn");
        Check(MapModel.Read(files, "maps\\Missing\\global.cfg") is null && MapModel.Read(files, "..\\global.cfg") is null, "unknown or outside");
        var fingerprint = map.Fingerprint();
        File.AppendAllText(fixture.Path("maps\\Test\\tile_1_0.map"), "\r\n");
        Check(MapModel.Read(files, "maps\\Test\\global.cfg")!.Fingerprint() != fingerprint, "fingerprint follows the tiles");
    }

    private static void TilesGate()
    {
        using var fixture = new Fixture();
        var files = new FileIndex(fixture.Root);
        var map = MapModel.Read(files, "maps\\Test\\global.cfg")!;
        var resolver = new AssetResolver(files, true);
        var index = new TileIndex(map, resolver, map.Fingerprint());
        var tile = index.Files(map.Tiles[0]).Select(files.Relative).ToList();
        var expected = new[]
        {
            "maps\\Test\\tile_0_0.map", "maps\\Test\\tile_0_0.map.terrain",
            "Sceneryobjects\\Obj\\a.sco", "Sceneryobjects\\Obj\\model\\a.o3d", "Sceneryobjects\\Obj\\texture\\tex.bmp", "Sceneryobjects\\Obj\\texture\\tex.bmp.cfg",
            "Splines\\road.sli", "Splines\\texture\\road.bmp"
        };
        Check(tile[0] == expected[0], "the tile file comes first");
        // tile_X_Y.map.LM.bmp is rewritten by the game while it streams tiles: not part of what is held.
        Check(!tile.Any(x => x.EndsWith(".LM.bmp", StringComparison.OrdinalIgnoreCase)), "the streamed LM.bmp of the tile is not in its closure");
        Check(tile.OrderBy(x => x).SequenceEqual(expected.OrderBy(x => x)), "tile 0,0: " + string.Join(", ", tile));
        Check(index.Around(0, 0, 2).Select(t => t.Index).SequenceEqual(new[] { 0, 1 }), "around");
        var path = Path.Combine(fixture.Root, "tiles.idx");
        index.Files(map.Tiles[2]);
        index.Save(path);
        var loaded = new TileIndex(map, resolver, map.Fingerprint());
        loaded.Load(path);
        Check(loaded.Loaded && loaded.Computed == 2 && loaded.IsComputed(map.Tiles[2]) && !loaded.IsComputed(map.Tiles[1]), "saved and loaded");
        Check(loaded.Files(map.Tiles[0]).Select(files.Relative).SequenceEqual(index.Files(map.Tiles[0]).Select(files.Relative)), "same files after loading");
        var stale = new TileIndex(map, resolver, "other");
        stale.Load(path);
        Check(!stale.Loaded, "another fingerprint is not loaded");
    }

    private static void VehiclesGate()
    {
        using var fixture = new Fixture();
        var files = new FileIndex(fixture.Root);
        var map = MapModel.Read(files, "maps\\Test\\global.cfg")!;
        var resolver = new AssetResolver(files, true);
        var log = new List<string>();
        var ai = Vehicles.MapAi(files, resolver, map, VehicleScope.Load, log.Add);
        Check(ai.Count == 1 && ai[0].Folder == "Bus", "the map's AI: " + string.Join(", ", ai.Select(v => v.Folder)));
        var load = ai[0].Files.Select(files.Relative).ToList();
        Check(load.Contains("Vehicles\\Bus\\model\\body.o3d") && load.Contains("Vehicles\\Bus\\model\\wheel.o3d") && load.Contains("Vehicles\\Bus\\texture\\body.bmp") && load.Contains("Vehicles\\Bus\\sound\\engine.wav"), "load: " + string.Join(", ", load));
        Check(!load.Contains("Vehicles\\Bus\\readme.txt") && !load.Contains("Vehicles\\Bus\\source.psd"), "load: nothing it does not cite");
        Vehicles.Resolve(resolver, ai[0], VehicleScope.Essential);
        var essential = ai[0].Files.Select(files.Relative).ToList();
        Check(essential.Contains("Vehicles\\Bus\\model\\body.o3d") && !essential.Contains("Vehicles\\Bus\\model\\wheel.o3d") && essential.Contains("Vehicles\\Bus\\texture\\body.bmp"), "essential: the largest mesh and its textures: " + string.Join(", ", essential));
        Vehicles.Resolve(resolver, ai[0], VehicleScope.Complete);
        var complete = ai[0].Files.Select(files.Relative).ToList();
        Check(complete.Contains("Vehicles\\Bus\\readme.txt") && !complete.Contains("Vehicles\\Bus\\source.psd"), "complete: the folder without work files");
        var withoutSounds = new AssetResolver(files, false);
        var quiet = Vehicles.MapAi(files, withoutSounds, map, VehicleScope.Load, log.Add);
        Check(!quiet[0].Files.Any(f => f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)), "sounds off");
        var user = Vehicles.User(files, resolver, map, new[] { "Bus" }, true, Array.Empty<string>(), VehicleScope.Load, VehicleScope.Load, log.Add);
        Check(user.Count == 1 && user[0].Folder == "User1" && user[0].Files.Select(files.Relative).Contains("Vehicles\\User1\\model\\u.o3d"), "user vehicles");
        var listed = Vehicles.User(files, resolver, map, new[] { "Bus" }, false, new[] { "Bus", "Nope", "User1" }, VehicleScope.Load, VehicleScope.Load, log.Add);
        Check(listed.Count == 1 && log.Any(l => l.Contains("'Nope'")) && log.Any(l => l.Contains("'Bus' is a vehicle of the map's AI")), "listed user vehicles");
    }

    private static void HoldingsGate()
    {
        using var fixture = new Fixture();
        var files = new FileIndex(fixture.Root);
        var tex = fixture.Path("Sceneryobjects\\Obj\\texture\\tex.bmp");
        var body = fixture.Path("Vehicles\\Bus\\model\\body.o3d");
        var osn = fixture.Path("maps\\Test\\laststn.osn");
        var options = fixture.Path("options.cfg");
        using var holdings = new Holdings(files, 25_000, new[] { "options.cfg" }) { StopBelowMb = 0 };
        var first = holdings.Hold(tex, Tier.Map);
        Check(first == HoldResult.Held, "held: " + first);
        Check(Fixture.IsMapped(tex), "mapped");
        Check(holdings.Hold(tex, Tier.Tiles) == HoldResult.Known, "held once");
        Check(holdings.Hold(osn, Tier.Map) == HoldResult.Denied && holdings.Hold(options, Tier.Map) == HoldResult.Denied, "files the game rewrites and session files are never held");
        Check(holdings.Hold(body, Tier.AiVehicles) == HoldResult.Budget, "over the budget");
        Check(holdings.Hold(fixture.Path("nothing.bmp"), Tier.Map) == HoldResult.Unusable, "missing file");
        Check(holdings.HeldBytes == 7000 && holdings.TierBytes(Tier.Map) == 7000, "accounting");
        Check(holdings.MarkUsed(tex) && !holdings.MarkUsed(tex) && holdings.MarkUsed(fixture.Path("Splines\\road.sli")), "first use");
        Check(holdings.UsedInOrder().Select(files.Relative).SequenceEqual(new[] { "Sceneryobjects\\Obj\\texture\\tex.bmp", "Splines\\road.sli" }), "first-use order");
        Check(holdings.MarkWritten(tex) && !Fixture.IsMapped(tex) && holdings.HeldBytes == 0, "written: released at once");
        Check(holdings.Hold(tex, Tier.Map) == HoldResult.Denied, "written: never held again");
        var b = fixture.Path("Sceneryobjects\\Obj\\model\\b.o3d");
        Check(holdings.Hold(b, Tier.UserVehicles) == HoldResult.Held && holdings.ReleaseUnused(1) == 3000 && !Fixture.IsMapped(b), "released under memory pressure");
    }

    // The dump of 2026-10-12: tile_X_Y.map.prt and tile_X_Y.map.LM.bmp are rewritten while the game loads tiles.
    private static void StreamedFilesGate()
    {
        using var fixture = new Fixture();
        var files = new FileIndex(fixture.Root);
        foreach (var name in new[] { "maps\\Test\\tile_0_0.map.prt", "maps\\Test\\tile_0_0.map.LM.bmp", "maps\\Test\\tile_0_0.map.lm.bmp", "maps\\Test\\x.owt", "maps\\Test\\Tile_1_0.MAP.PRT" })
        {
            var path = fixture.Path(name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "streamed by the game");
            using var holdings = new Holdings(files, 1_000_000, Array.Empty<string>()) { StopBelowMb = 0 };
            Check(Holdings.DeniedExtension(path) && holdings.IsDenied(path), "denied by extension: " + name);
            Check(holdings.Hold(path, Tier.Tiles) == HoldResult.Denied && !Fixture.IsMapped(path), "never mapped: " + name);
        }
        // They are not part of what the map, a tile or the map folder asks to be held.
        var map = MapModel.Read(files, "maps\\Test\\global.cfg")!;
        Check(!map.MapWideFiles().Any(x => x.EndsWith(".prt", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".lm.bmp", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".owt", StringComparison.OrdinalIgnoreCase)), "map-wide files: no streamed files");
        Check(!map.TileOwnFiles(map.Tiles[0]).Any(x => x.EndsWith(".prt", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".lm.bmp", StringComparison.OrdinalIgnoreCase)), "a tile's own files: no streamed files");
        // An ordinary tile file is still held.
        var tile = fixture.Path("maps\\Test\\tile_0_0.map");
        using var normal = new Holdings(files, 1_000_000, Array.Empty<string>()) { StopBelowMb = 0 };
        Check(normal.Hold(tile, Tier.Tiles) == HoldResult.Held, "the tile file itself is still held");
    }

    // OMSI's shim waits about 1.5 s for a release: the X line is answered at once even
    // when a burst of U lines is being worked slowly.
    private static void FeedReleaseGate()
    {
        var channel = @"\\.\pipe\OmsiLaunch.Module.test" + Guid.NewGuid().ToString("N");
        var releasedAfter = new ConcurrentQueue<long>();
        var started = System.Diagnostics.Stopwatch.StartNew();
        var uSeen = 0;
        using var feed = new Feed(channel, () => Environment.ProcessId, (kind, path) =>
        {
            if (kind == 'U') { Interlocked.Increment(ref uSeen); Thread.Sleep(40); }
            else if (kind == 'X') releasedAfter.Enqueue(started.ElapsedMilliseconds);
        });
        feed.Start();
        using (var client = new NamedPipeClientStream(".", channel[9..], PipeDirection.Out))
        {
            client.Connect(5000);
            var burst = new StringBuilder();
            for (var index = 0; index < 100; index++) burst.Append("U\tmaps\\Test\\f").Append(index).Append(".txt\n");
            burst.Append("X\tmaps\\Test\\held.bmp\n");
            var bytes = Encoding.UTF8.GetBytes(burst.ToString());
            started.Restart();
            client.Write(bytes, 0, bytes.Length);
            client.Flush();
            var until = DateTime.UtcNow + TimeSpan.FromSeconds(3);
            while (releasedAfter.IsEmpty && DateTime.UtcNow < until) Thread.Sleep(5);
            Check(releasedAfter.TryPeek(out var after) && after < 500, "the release is answered at once behind a burst of slow U lines (" + (releasedAfter.TryPeek(out var shown) ? shown : -1) + " ms; the U lines take 4 s)");
            Check(uSeen < 90, "the burst was still being worked when the release came (" + uSeen + " of 100 U lines done)");
            // A burst larger than the queue drops hints, never the release.
            var flood = new StringBuilder();
            for (var index = 0; index < 6000; index++) flood.Append("U\tmaps\\Test\\g").Append(index).Append(".txt\n");
            flood.Append("X\tmaps\\Test\\held2.bmp\n");
            var floodBytes = Encoding.UTF8.GetBytes(flood.ToString());
            client.Write(floodBytes, 0, floodBytes.Length);
            client.Flush();
            var floodUntil = DateTime.UtcNow + TimeSpan.FromSeconds(3);
            while (releasedAfter.Count < 2 && DateTime.UtcNow < floodUntil) Thread.Sleep(5);
            Check(releasedAfter.Count == 2, "the release survives a flood larger than the queue");
            Check(feed.Dropped > 0, "an overfull queue drops hints and counts them (" + feed.Dropped + ")");
        }
    }

    private static void PackageGate(string package)
    {
        var module = ModuleCatalog.Inspect(package);
        Check(module.Code is null, "the package is a valid module: " + module.Code + " " + module.Message);
        var manifest = module.Manifest!;
        Check(manifest.Id == "omsi-prefetch" && manifest.Activation == ModuleActivation.Default && manifest.BridgeProtocolMin == 2 && manifest.BridgeProtocolMax == 2, "identity and activation");
        Check(manifest.HostComponent?.Path == "Prefetch.Preloader.exe" && manifest.Components.Any(c => c.Kind == ModuleComponentKind.Native), "components");
        Check(manifest.EffectiveSettings.Count == 17 && manifest.EffectiveSettings.Single(s => s.Key == "ram_budget_mb").Flag == "prefetch-ram", "settings");
        Check(manifest.EffectiveConflictingPlugins.SequenceEqual(new[] { "OmsiLoadingPoC.dll" }), "conflicts");
        var files = Directory.GetFiles(package).Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Check(files.SequenceEqual(new[] { "GPL-3.0.txt", "LGPL-3.0.txt", "Prefetch.Module.dll", "Prefetch.Preloader.exe", "NOTICE.txt", "prefetch-native.dll", "module.json" }.OrderBy(x => x, StringComparer.Ordinal)), "exactly the module's files: " + string.Join(", ", files));
        Check(File.ReadAllText(Path.Combine(package, "LGPL-3.0.txt")).Contains("GNU LESSER GENERAL PUBLIC LICENSE", StringComparison.Ordinal), "the LGPL-3.0 text");
    }

    // ---------------------------------------------------------------- end to end
    private sealed class Run : IDisposable
    {
        private readonly ConcurrentQueue<JsonElement> lines = new();
        private readonly AutoResetEvent arrived = new(false);
        public ModuleSessionProcess Process { get; }

        public Run(string exe, Fixture fixture, Guid session, Dictionary<string, string> settings)
        {
            Process = ModuleSessionProcess.Start(exe, Path.GetDirectoryName(exe)!, session, line =>
            {
                try { lines.Enqueue(JsonDocument.Parse(line).RootElement.Clone()); arrived.Set(); } catch (JsonException) { }
            });
            var data = Path.Combine(fixture.Root, ".omsilaunch", "module-data", "omsi-prefetch");
            Directory.CreateDirectory(data);
            var context = new Dictionary<string, object?>
            {
                ["type"] = "context", ["schema"] = "omsilaunch.module-session/v1", ["session_id"] = session.ToString("D"), ["module_id"] = "omsi-prefetch",
                ["module_version"] = "1.0.0-beta.1", ["product_version"] = "0.1.0-test", ["installation_root"] = fixture.Root, ["module_directory"] = Path.GetDirectoryName(exe),
                ["data_directory"] = data, ["channel"] = RuntimeModuleProtocol.SessionChannelName(session, "omsi-prefetch"), ["settings"] = settings,
                ["world"] = new Dictionary<string, object?> { ["mode"] = "NewMap", ["map"] = "maps\\Test\\global.cfg", ["situation"] = null, ["entrypoint_index"] = 0, ["entrypoint"] = null },
                ["session_files"] = new[] { "options.cfg", "closecheck" }
            };
            Check(Process.TryWriteLine(JsonSerializer.Serialize(context)), "context written");
        }

        public List<JsonElement> Seen { get; } = new();

        public JsonElement? WaitFor(Func<JsonElement, bool> match, TimeSpan timeout)
        {
            var until = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < until)
            {
                while (lines.TryDequeue(out var line)) Seen.Add(line);
                var found = Seen.FirstOrDefault(match);
                if (found.ValueKind != JsonValueKind.Undefined) return found;
                if (Process.HasExited) return null;
                arrived.WaitOne(100);
            }
            return null;
        }

        public JsonElement? Event(string name, TimeSpan timeout) => WaitFor(x => x.GetProperty("type").GetString() == "event" && x.GetProperty("name").GetString() == name, timeout);

        public void Dispose() => Process.Dispose();
    }

    private static void EndToEnd(string exe)
    {
        Check(File.Exists(exe), "the session program exists: " + exe);
        using var fixture = new Fixture();
        var settings = new Dictionary<string, string> { ["ram_budget_mb"] = "512", ["tile_radius"] = "3", ["monitor"] = "false", ["user_vehicles"] = "none", ["pre_open"] = "learned" };
        var session = Guid.NewGuid();
        var data = Path.Combine(fixture.Root, ".omsilaunch", "module-data", "omsi-prefetch");
        using (var run = new Run(exe, fixture, session, settings))
        {
            Check(run.WaitFor(x => x.GetProperty("type").GetString() == "ready", TimeSpan.FromSeconds(60)) is not null, "ready");
            var prepared = run.Event("prepared", TimeSpan.FromSeconds(5)) ?? throw new InvalidOperationException("no prepared event");
            Check(prepared.GetProperty("data").GetProperty("first_open").GetString() == "true", "first open");
            Check(Fixture.IsMapped(fixture.Path("Sceneryobjects\\Obj\\model\\a.o3d")) && Fixture.IsMapped(fixture.Path("Vehicles\\Bus\\model\\body.o3d")) && Fixture.IsMapped(fixture.Path("maps\\Test\\global.cfg")),
                "the start tiles, the map and its AI are held");
            Check(!Fixture.IsMapped(fixture.Path("Sceneryobjects\\Far\\model\\far.o3d")), "a tile beyond tile_radius is not held before OMSI");
            Check(!Fixture.IsMapped(fixture.Path("maps\\Test\\laststn.osn")) && !Fixture.IsMapped(fixture.Path("options.cfg")) && !Fixture.IsMapped(fixture.Path("maps\\Test\\tile_0_0.map.terrain_0.rdy")), "files the game rewrites are never held");

            // OMSI: the controller names its process, then the native component connects.
            Check(run.Process.TryWriteLine("{\"type\":\"omsi-started\",\"pid\":" + Environment.ProcessId + "}"), "omsi-started");
            using (var client = new NamedPipeClientStream(".", RuntimeModuleProtocol.SessionChannelName(session, "omsi-prefetch")[9..], PipeDirection.Out))
            {
                client.Connect(5000);
                var feed = "U\tmaps\\Test\\global.cfg\nT\tmaps\\Test\\tile_0_0.map\nU\tSceneryobjects\\Obj\\model\\a.o3d\nU\t..\\outside.txt\nW\tmaps\\Test\\written.txt\n" +
                           "X\t" + fixture.Path("Sceneryobjects\\Obj\\texture\\tex.bmp") + "\nU\tVehicles\\User1\\u.bus\n";
                var bytes = Encoding.UTF8.GetBytes(feed);
                client.Write(bytes, 0, bytes.Length);
                client.Flush();
                var until = DateTime.UtcNow + TimeSpan.FromSeconds(5);
                while (Fixture.IsMapped(fixture.Path("Sceneryobjects\\Obj\\texture\\tex.bmp")) && DateTime.UtcNow < until) Thread.Sleep(50);
                Check(!Fixture.IsMapped(fixture.Path("Sceneryobjects\\Obj\\texture\\tex.bmp")), "released at OMSI's request");
            }
            Thread.Sleep(300);
            Check(run.Process.TryWriteLine("{\"type\":\"stop\"}"), "stop");
            Check(run.Process.WaitForExit(TimeSpan.FromSeconds(25)) && run.Process.ExitCode == 0, "exits after stop");
            var lines = run.Event("session", TimeSpan.FromSeconds(2))?.GetProperty("data").GetProperty("feed_lines").GetString();
            Check(lines == "7", "session summary: every line received (" + lines + ")");
        }
        var mapData = Directory.GetDirectories(Path.Combine(data, "maps")).Single();
        var learned = File.ReadAllLines(Path.Combine(mapData, "learned.txt")).Where(l => !l.StartsWith('#')).ToList();
        Check(learned.SequenceEqual(new[] { "maps\\Test\\global.cfg", "maps\\Test\\tile_0_0.map", "Sceneryobjects\\Obj\\model\\a.o3d", "Vehicles\\User1\\u.bus" }), "learned in first-use order: " + string.Join(", ", learned));
        var written = File.ReadAllLines(Path.Combine(mapData, "written.txt")).Where(l => !l.StartsWith('#')).OrderBy(x => x).ToList();
        Check(written.SequenceEqual(new[] { "maps\\Test\\written.txt", "Sceneryobjects\\Obj\\texture\\tex.bmp" }), "written: " + string.Join(", ", written));
        Check(File.ReadLines(Path.Combine(mapData, "tiles.idx")).Skip(1).First().StartsWith("fingerprint ", StringComparison.Ordinal), "tile index saved");
        Check(Directory.GetFiles(Path.Combine(data, "logs"), "preloader-*.log").Length == 1, "log written");

        // Second session: what was learned comes first; a client that is not OMSI is refused.
        var second = Guid.NewGuid();
        using (var run = new Run(exe, fixture, second, settings))
        {
            Check(run.WaitFor(x => x.GetProperty("type").GetString() == "ready", TimeSpan.FromSeconds(60)) is not null, "ready again");
            var prepared = run.Event("prepared", TimeSpan.FromSeconds(5)) ?? throw new InvalidOperationException("no prepared event");
            Check(prepared.GetProperty("data").GetProperty("first_open").GetString() == "false", "not the first open any more");
            Check(Fixture.IsMapped(fixture.Path("Vehicles\\User1\\u.bus")), "a learned file is held");
            Check(!Fixture.IsMapped(fixture.Path("Sceneryobjects\\Obj\\texture\\tex.bmp")), "a file OMSI replaced is not held again");
            Check(run.Process.TryWriteLine("{\"type\":\"omsi-started\",\"pid\":4}"), "omsi-started");
            using (var client = new NamedPipeClientStream(".", RuntimeModuleProtocol.SessionChannelName(second, "omsi-prefetch")[9..], PipeDirection.Out))
            {
                client.Connect(5000);
                var refused = false;
                var bytes = Encoding.UTF8.GetBytes("U\tmaps\\Test\\ailists.cfg\n");
                for (var i = 0; i < 40 && !refused; i++)
                {
                    try { client.Write(bytes, 0, bytes.Length); client.Flush(); Thread.Sleep(50); }
                    catch (IOException) { refused = true; }
                }
                Check(refused, "a client that is not OMSI is disconnected");
            }
            Check(run.Process.TryWriteLine("{\"type\":\"stop\"}"), "stop");
            Check(run.Process.WaitForExit(TimeSpan.FromSeconds(25)) && run.Process.ExitCode == 0, "exits after stop");
            Check(run.Event("session", TimeSpan.FromSeconds(2)) is { } summary && summary.GetProperty("data").GetProperty("feed_refused").GetString() == "1" && summary.GetProperty("data").GetProperty("feed_lines").GetString() == "0", "refused client counted, its lines ignored");
        }
    }
}
