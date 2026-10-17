using System.Collections.Concurrent;

namespace OmsiLaunch.Modules.Prefetch;

// Paths below the OMSI installation and what is known about them. Keys are
// full paths in lower case; values keep the case found on disk.
internal sealed class FileIndex
{
    private readonly ConcurrentDictionary<string, long> sizes = new(StringComparer.OrdinalIgnoreCase);

    public FileIndex(string root) => Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

    public string Root { get; }

    // Full path of a reference (absolute, or relative to the installation);
    // null when it leaves the installation or is not a valid path.
    public string? Full(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 1024 || path.IndexOfAny(Path.GetInvalidPathChars()) >= 0) return null;
        try
        {
            var normalized = path.Trim().Replace('/', '\\');
            var full = Path.GetFullPath(Path.IsPathRooted(normalized) ? normalized : Path.Combine(Root, normalized));
            return Inside(full) ? full : null;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
    }

    public bool Inside(string full) => full.Length > Root.Length + 1 && full.StartsWith(Root, StringComparison.OrdinalIgnoreCase) && full[Root.Length] == '\\';

    public string Relative(string full) => Inside(full) ? full[(Root.Length + 1)..] : full;

    public static string Key(string full) => full.ToLowerInvariant();

    // -1 when the path is not a file.
    public long Size(string full)
    {
        if (sizes.TryGetValue(full, out var size)) return size;
        size = Native.FileSize(full);
        if (sizes.Count < 1_000_000) sizes[full] = size;
        return size;
    }

    public bool IsFile(string full) => Size(full) >= 0;

    public string Combine(string directory, string reference)
    {
        try { return Path.GetFullPath(Path.Combine(directory, reference)); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException) { return ""; }
    }

    // Files of a directory (recursively when asked), skipping links.
    public static List<string> ListFiles(string directory, bool recursive, int limit = 200_000)
    {
        var files = new List<string>();
        var pending = new Stack<string>();
        pending.Push(directory);
        while (pending.Count > 0 && files.Count < limit)
        {
            var current = pending.Pop();
            try
            {
                foreach (var entry in new DirectoryInfo(current).EnumerateFileSystemInfos())
                {
                    if ((entry.Attributes & FileAttributes.Directory) != 0)
                    {
                        if (recursive && (entry.Attributes & FileAttributes.ReparsePoint) == 0) pending.Push(entry.FullName);
                    }
                    else files.Add(entry.FullName);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        }
        return files;
    }
}

// Follows what an OMSI asset cites: models, textures, scripts, sounds,
// sub-objects. The rules are those of the loading PoC (vehicles.h,
// tools/tile_index.py): a reference is the first tab field of a line or a
// quoted string; models are also looked for in a model\ folder; textures in
// texture\ folders up the tree, with another image extension as a last
// resort; a texture's .cfg goes with it.
internal sealed class AssetResolver
{
    public static readonly string[] Images = { ".bmp", ".dds", ".tga", ".png", ".jpg", ".jpeg" };
    // Text files whose references are followed.
    private static readonly string[] Followed = { ".bus", ".ovh", ".sco", ".sli", ".cfg", ".cti", ".hum", ".zug" };
    // Files a followed file may cite.
    private static readonly string[] Cited = { ".bus", ".ovh", ".sco", ".sli", ".cfg", ".cti", ".hum", ".zug", ".o3d", ".osc", ".txt", ".wav", ".x", ".hof", ".org", ".oft", ".clb" };
    // Work files and archives the game never reads.
    public static readonly string[] WorkFiles = { ".psd", ".rar", ".zip", ".7z", ".blend", ".blend1", ".aup3", ".aup", ".pdn", ".max", ".3ds", ".xcf", ".pdf", ".doc", ".docx",
        ".xls", ".xlsx", ".db", ".bak", ".tmp", ".exe", ".dll", ".url", ".lnk", ".fbx", ".obj", ".mtl", ".kra", ".svg", ".ai", ".log", ".osn", ".rdy", ".sav" };
    private const int MaxFiles = 60_000;

    private readonly FileIndex files;
    private readonly bool sounds;
    private readonly ConcurrentDictionary<string, string[]> scenery = new(StringComparer.OrdinalIgnoreCase);

    public AssetResolver(FileIndex files, bool sounds) { this.files = files; this.sounds = sounds; }

    public FileIndex Files => files;

    // Everything an object or spline placed by a tile needs (memoized).
    public string[] Scenery(string asset) => scenery.GetOrAdd(asset, key =>
    {
        var output = new List<string>();
        Closure(new[] { key }, null, output, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        return output.ToArray();
    });

    // The loading closure of entry files (vroot: the vehicle's folder, or null).
    public void Closure(IEnumerable<string> entries, string? vroot, List<string> output, HashSet<string> seen)
    {
        var pending = new Stack<string>(entries.Reverse());
        while (pending.Count > 0)
        {
            var path = pending.Pop();
            if (!Add(path, output, seen)) continue;
            if (Text.EndsWith(path, ".o3d"))
            {
                foreach (var name in Text.O3dTextures(path)) AddTexture(ResolveTexture(name, new[] { Path.GetDirectoryName(path)! }, vroot), output, seen);
                continue;
            }
            if (!Text.EndsWithAny(path, Followed)) continue;
            var here = Path.GetDirectoryName(path)!;
            var meshDirectories = new List<string> { here };
            var section = "";
            var sinceSection = 0;
            foreach (var line in Text.ReadLines(path))
            {
                if (line.Length == 0) continue;
                if (line[0] == '[') { section = line.ToLowerInvariant(); sinceSection = 0; continue; }
                sinceSection++;
                if (section == "[ctc]" && sinceSection == 2 && vroot is not null)
                {
                    // The repaint folder: every .cti in it and the textures it lists.
                    foreach (var basis in new[] { vroot, here })
                        if (EnumerateCti(files.Combine(basis, line), pending)) break;
                    continue;
                }
                foreach (var token in Text.LineTokens(line))
                {
                    if (token.Length == 0 || token.Length > 400) continue;
                    if (Text.EndsWithAny(token, Images))
                    {
                        var directories = Enumerable.Reverse(meshDirectories).ToArray();
                        AddTexture(ResolveTexture(token, directories, vroot), output, seen);
                    }
                    else if (Text.EndsWithAny(token, Cited))
                    {
                        if (!sounds && Text.EndsWith(token, ".wav")) continue;
                        var resolved = ResolveReference(token, here, vroot);
                        if (resolved is null) continue;
                        if (Text.EndsWith(resolved, ".o3d")) meshDirectories.Add(Path.GetDirectoryName(resolved)!);
                        pending.Push(resolved);
                    }
                }
            }
        }
    }

    // What a preview needs first: the entry, its model .cfg, the largest mesh
    // and its base textures, and the .hof files.
    public void Essential(string vroot, IEnumerable<string> entries, List<string> output, HashSet<string> seen)
    {
        foreach (var entry in entries)
        {
            Add(entry, output, seen);
            var lines = Text.ReadLines(entry);
            var configs = new List<string>();
            for (var i = 0; i + 1 < lines.Count; i++)
                if (lines[i].Equals("[model]", StringComparison.OrdinalIgnoreCase) && ResolveReference(lines[i + 1], Path.GetDirectoryName(entry)!, vroot) is { } config) configs.Add(config);
            if (lines.Any(x => x.Equals("[mesh]", StringComparison.OrdinalIgnoreCase))) configs.Add(entry);
            foreach (var config in configs)
            {
                Add(config, output, seen);
                var configLines = Text.ReadLines(config);
                var directory = Path.GetDirectoryName(config)!;
                var meshes = new List<(long Size, int Line, string Path)>();
                for (var i = 0; i + 1 < configLines.Count; i++)
                    if (configLines[i].Equals("[mesh]", StringComparison.OrdinalIgnoreCase) && ResolveReference(configLines[i + 1], directory, vroot) is { } mesh) meshes.Add((files.Size(mesh), i, mesh));
                if (meshes.Count == 0) continue;
                var main = meshes.MaxBy(x => x.Size);
                Add(main.Path, output, seen);
                var meshDirectory = Path.GetDirectoryName(main.Path)!;
                foreach (var name in Text.O3dTextures(main.Path)) AddTexture(ResolveTexture(name, new[] { meshDirectory }, vroot), output, seen);
                var end = meshes.Where(x => x.Line > main.Line).Select(x => x.Line).DefaultIfEmpty(configLines.Count).Min();
                for (var i = 0; i + 1 < configLines.Count; i++)
                {
                    if (configLines[i].Equals("[matl]", StringComparison.OrdinalIgnoreCase) && i > main.Line && i < end) AddTexture(ResolveTexture(configLines[i + 1], new[] { meshDirectory }, vroot), output, seen);
                    if (configLines[i].Equals("[ctctexture]", StringComparison.OrdinalIgnoreCase) && i + 2 < configLines.Count) AddTexture(ResolveTexture(configLines[i + 2], new[] { meshDirectory }, vroot), output, seen);
                }
            }
        }
        foreach (var hof in FileIndex.ListFiles(vroot, false).Where(x => Text.EndsWith(x, ".hof"))) Add(hof, output, seen);
    }

    // The whole folder without work files, after what loading reads first.
    public void Complete(string vroot, IEnumerable<string> entries, List<string> output, HashSet<string> seen)
    {
        var closure = new List<string>();
        Closure(entries, vroot, closure, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        foreach (var file in closure) Add(file, output, seen);
        foreach (var file in FileIndex.ListFiles(vroot, true))
        {
            if (Text.EndsWithAny(file, WorkFiles)) continue;
            if (!sounds && Text.EndsWith(file, ".wav")) continue;
            Add(file, output, seen);
        }
    }

    public string? ResolveReference(string reference, string here, string? vroot)
    {
        var token = reference.Trim().Replace('/', '\\');
        if (token.Length == 0) return null;
        if (token.Length > 1 && token[1] == ':') { var absolute = files.Full(token); return absolute is not null && files.IsFile(absolute) ? absolute : null; }
        foreach (var basis in vroot is null ? new[] { here, files.Root } : new[] { here, vroot, files.Root })
        {
            var direct = files.Combine(basis, token);
            if (direct.Length > 0 && files.Inside(direct) && files.IsFile(direct)) return direct;
            var model = files.Combine(Path.Combine(basis, "model"), token);
            if (model.Length > 0 && files.Inside(model) && files.IsFile(model)) return model;
        }
        return null;
    }

    public string? ResolveTexture(string reference, IReadOnlyList<string> directories, string? vroot)
    {
        var name = reference.Trim().Replace('/', '\\');
        if (name.Length == 0) return null;
        var candidates = new List<string>();
        foreach (var directory in directories)
        {
            // texture\ folders from the citing file's folder up to the installation
            for (var current = directory; current is not null && files.Inside(current + "\\x"); current = Path.GetDirectoryName(current))
            {
                candidates.Add(Path.Combine(current, "texture", name));
                candidates.Add(Path.Combine(current, name));
                if (current.Length <= files.Root.Length) break;
            }
        }
        if (vroot is not null) { candidates.Add(Path.Combine(vroot, "texture", name)); candidates.Add(Path.Combine(vroot, "model", "texture", name)); }
        candidates.Add(Path.Combine(files.Root, "texture", name));
        candidates.Add(Path.Combine(files.Root, name));
        foreach (var candidate in candidates)
        {
            var full = files.Combine(candidate, ".");
            if (full.Length > 0 && files.Inside(full) && files.IsFile(full)) return full;
        }
        // The same name with another image extension (textures converted to
        // DDS keep the name the model cites).
        var stem = Path.GetFileNameWithoutExtension(name);
        foreach (var directory in directories)
            foreach (var sub in new[] { "texture", "", "..\\texture" })
                foreach (var extension in Images)
                {
                    var full = files.Combine(sub.Length == 0 ? directory : Path.Combine(directory, sub), stem + extension);
                    if (full.Length > 0 && files.Inside(full) && files.IsFile(full)) return full;
                }
        return null;
    }

    private bool Add(string path, List<string> output, HashSet<string> seen)
    {
        if (output.Count >= MaxFiles || !files.Inside(path) || !files.IsFile(path)) return false;
        if (!seen.Add(path)) return false;
        output.Add(path);
        return true;
    }

    private void AddTexture(string? texture, List<string> output, HashSet<string> seen)
    {
        if (texture is null) return;
        Add(texture, output, seen);
        var config = texture + ".cfg";
        if (files.IsFile(config)) Add(config, output, seen);
    }

    private bool EnumerateCti(string directory, Stack<string> pending)
    {
        if (directory.Length == 0 || !files.Inside(directory + "\\x") || !Directory.Exists(directory)) return false;
        try
        {
            foreach (var cti in Directory.EnumerateFiles(directory, "*.cti")) pending.Push(cti);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return false; }
    }
}
