namespace OmsiLaunch.Modules.Prefetch;

internal enum VehicleScope { Essential, Load, Complete }

internal sealed class Vehicle
{
    public string Folder = "";                   // folder name under Vehicles (or a label)
    public string Root = "";                     // its full path
    public bool Ai;                              // used by the map (lists), not a user vehicle
    public bool MapHof;                          // carries <map>.hof: the map's own fleet
    public VehicleScope Scope;
    public List<string> Entries = new();         // .bus/.ovh/.sco/.hum that make it appear
    public List<string> Files = new();           // the resolved scope, loading order
    public long Bytes;
}

// Which vehicles a session will load (the PoC's vehicles.h). The map's AI:
// every vehicle its lists cite (ailists.cfg, parklist_p.txt), and the people
// and objects they place (humans.txt, drivers.txt). The user's vehicles: the
// folders of Vehicles\ with a valid .bus/.ovh that the map's AI does not use;
// those carrying the map's timetable (<map>.hof) are the map's own fleet.
internal static class Vehicles
{
    public static List<Vehicle> MapAi(FileIndex files, AssetResolver resolver, MapModel map, VehicleScope scope, Action<string> log)
    {
        var vehiclesDirectory = Path.Combine(files.Root, "Vehicles");
        var byFolder = new Dictionary<string, Vehicle>(StringComparer.OrdinalIgnoreCase);
        var objects = new Vehicle { Folder = "(objects and people of the map)", Root = map.Directory, Ai = true };
        var cited = 0;
        foreach (var list in new[] { "ailists.cfg", "parklist_p.txt", "humans.txt", "drivers.txt", "unsched_vehgroups.txt" })
        {
            var path = Path.Combine(map.Directory, list);
            if (!files.IsFile(path)) continue;
            foreach (var token in Text.Tokens(Text.ReadLines(path)))
            {
                if (!Text.EndsWith(token, ".bus") && !Text.EndsWith(token, ".ovh") && !Text.EndsWith(token, ".sco") && !Text.EndsWith(token, ".hum")) continue;
                if (files.Full(token) is not { } full || !files.IsFile(full)) continue;
                cited++;
                var top = TopFolder(vehiclesDirectory, full);
                if (top is null)
                {
                    if (!objects.Entries.Contains(full, StringComparer.OrdinalIgnoreCase)) objects.Entries.Add(full);
                    continue;
                }
                if (!byFolder.TryGetValue(top, out var vehicle))
                    byFolder[top] = vehicle = new Vehicle { Folder = top, Root = Path.Combine(vehiclesDirectory, top), Ai = true };
                if (!vehicle.Entries.Contains(full, StringComparer.OrdinalIgnoreCase)) vehicle.Entries.Add(full);
            }
        }
        var result = byFolder.Values.ToList();
        foreach (var vehicle in result) Resolve(resolver, vehicle, scope);
        result.Sort((a, b) => a.Bytes.CompareTo(b.Bytes));
        if (objects.Entries.Count > 0)
        {
            // Objects and people are followed like scenery, whatever the scope.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in objects.Entries) foreach (var file in resolver.Scenery(entry)) if (seen.Add(file)) objects.Files.Add(file);
            objects.Bytes = objects.Files.Sum(files.Size);
            result.Insert(0, objects);
        }
        log("vehicles: map AI " + cited + " citations, " + byFolder.Count + " folders, " + objects.Entries.Count + " objects/people (" + Mb(result.Sum(v => v.Bytes)) + ")");
        return result;
    }

    // User vehicles: all (the map's fleet first, then smallest first) or the
    // listed folders in their order.
    public static List<Vehicle> User(FileIndex files, AssetResolver resolver, MapModel? map, IReadOnlyCollection<string> aiFolders, bool all, IReadOnlyList<string> list,
        VehicleScope scope, VehicleScope fleetScope, Action<string> log)
    {
        var vehiclesDirectory = Path.Combine(files.Root, "Vehicles");
        var candidates = new List<Vehicle>();
        var empty = 0;
        IEnumerable<string> folders;
        try { folders = Directory.EnumerateDirectories(vehiclesDirectory).Select(Path.GetFileName).OfType<string>().ToList(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return candidates; }
        var hofName = map is null ? null : "\\" + Path.GetFileName(map.Directory) + ".hof";
        foreach (var folder in folders)
        {
            if (folder.StartsWith('.') || aiFolders.Contains(folder, StringComparer.OrdinalIgnoreCase)) continue;
            if (!all && !list.Contains(folder, StringComparer.OrdinalIgnoreCase)) continue;
            var vehicle = new Vehicle { Folder = folder, Root = Path.Combine(vehiclesDirectory, folder) };
            foreach (var file in FileIndex.ListFiles(vehicle.Root, false))
            {
                if ((Text.EndsWith(file, ".bus") || Text.EndsWith(file, ".ovh")) && Text.ReadLines(file).Any(l => l.Equals("[model]", StringComparison.OrdinalIgnoreCase))) vehicle.Entries.Add(file);
                if (hofName is not null && Text.EndsWith(file, hofName)) vehicle.MapHof = true;
            }
            if (vehicle.Entries.Count == 0) { empty++; continue; }
            vehicle.Entries.Sort(StringComparer.OrdinalIgnoreCase);
            candidates.Add(vehicle);
        }
        foreach (var vehicle in candidates) Resolve(resolver, vehicle, vehicle.MapHof ? fleetScope : scope);
        List<Vehicle> result;
        if (all) result = candidates.OrderBy(v => v.MapHof ? 0 : 1).ThenBy(v => v.Bytes).ToList();
        else
        {
            result = new List<Vehicle>();
            foreach (var name in list)
            {
                var found = candidates.FirstOrDefault(v => v.Folder.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (found is not null) result.Add(found);
                else log("vehicles: list entry '" + name + "' is " + (aiFolders.Contains(name, StringComparer.OrdinalIgnoreCase) ? "a vehicle of the map's AI" : "not a valid vehicle folder"));
            }
        }
        log("vehicles: " + result.Count + " user vehicles (" + Mb(result.Sum(v => v.Bytes)) + "), " + empty + " folders without a valid vehicle");
        return result;
    }

    public static void Resolve(AssetResolver resolver, Vehicle vehicle, VehicleScope scope)
    {
        vehicle.Scope = scope;
        vehicle.Files.Clear();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        switch (scope)
        {
            case VehicleScope.Essential: resolver.Essential(vehicle.Root, vehicle.Entries, vehicle.Files, seen); break;
            case VehicleScope.Load:
                resolver.Closure(vehicle.Entries, vehicle.Root, vehicle.Files, seen);
                foreach (var hof in FileIndex.ListFiles(vehicle.Root, false).Where(x => Text.EndsWith(x, ".hof"))) if (seen.Add(hof)) vehicle.Files.Add(hof);
                break;
            default: resolver.Complete(vehicle.Root, vehicle.Entries, vehicle.Files, seen); break;
        }
        vehicle.Bytes = vehicle.Files.Sum(resolver.Files.Size);
    }

    // The folder directly below Vehicles\ that holds a file, or null.
    public static string? TopFolder(string vehiclesDirectory, string full)
    {
        if (full.Length <= vehiclesDirectory.Length + 1 || !full.StartsWith(vehiclesDirectory, StringComparison.OrdinalIgnoreCase) || full[vehiclesDirectory.Length] != '\\') return null;
        var rest = full[(vehiclesDirectory.Length + 1)..];
        var slash = rest.IndexOf('\\');
        return slash < 0 ? null : rest[..slash];
    }

    public static string Mb(long bytes) => (bytes / 1048576.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " MB";
}
