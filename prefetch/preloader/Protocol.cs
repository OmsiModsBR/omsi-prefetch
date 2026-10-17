using System.Globalization;
using System.Text;
using System.Text.Json;

namespace OmsiLaunch.Modules.Prefetch;

// The first line OmsiLaunch writes to a session program
// (schema omsilaunch.module-session/v1).
internal sealed class SessionContext
{
    public const string Schema = "omsilaunch.module-session/v1";

    public string SessionId { get; private init; } = "";
    public string ModuleVersion { get; private init; } = "";
    public string ProductVersion { get; private init; } = "";
    public string InstallationRoot { get; private init; } = "";
    public string ModuleDirectory { get; private init; } = "";
    public string DataDirectory { get; private init; } = "";
    public string Channel { get; private init; } = "";
    public IReadOnlyDictionary<string, string> Settings { get; private init; } = new Dictionary<string, string>();
    public string WorldMode { get; private init; } = "";
    public string? Map { get; private init; }
    public int? EntrypointIndex { get; private init; }
    public IReadOnlyList<string> SessionFiles { get; private init; } = Array.Empty<string>();

    public static SessionContext Parse(string line)
    {
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || Text(root, "type") != "context" || Text(root, "schema") != Schema) throw new InvalidDataException("not a " + Schema + " context");
        var settings = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root.TryGetProperty("settings", out var settingsElement) && settingsElement.ValueKind == JsonValueKind.Object)
            foreach (var property in settingsElement.EnumerateObject()) if (property.Value.ValueKind == JsonValueKind.String) settings[property.Name] = property.Value.GetString()!;
        var world = root.TryGetProperty("world", out var worldElement) && worldElement.ValueKind == JsonValueKind.Object ? worldElement : default;
        int? entrypoint = world.ValueKind == JsonValueKind.Object && world.TryGetProperty("entrypoint_index", out var index) && index.ValueKind == JsonValueKind.Number && index.TryGetInt32(out var parsed) ? parsed : null;
        var files = root.TryGetProperty("session_files", out var filesElement) && filesElement.ValueKind == JsonValueKind.Array
            ? filesElement.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToArray() : Array.Empty<string>();
        return new SessionContext
        {
            SessionId = Text(root, "session_id"), ModuleVersion = Text(root, "module_version"), ProductVersion = Text(root, "product_version"),
            InstallationRoot = Text(root, "installation_root"), ModuleDirectory = Text(root, "module_directory"), DataDirectory = Text(root, "data_directory"),
            Channel = Text(root, "channel"), Settings = settings,
            WorldMode = world.ValueKind == JsonValueKind.Object ? Optional(world, "mode") ?? "" : "",
            Map = world.ValueKind == JsonValueKind.Object ? Optional(world, "map") : null,
            EntrypointIndex = entrypoint, SessionFiles = files
        };
    }

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : throw new InvalidDataException("context field '" + name + "' is missing");

    private static string? Optional(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

internal enum PreOpenMode { None, Learned, All }

// The module's settings (module.json declares them; OmsiLaunch validated and
// normalized every value before the session started).
internal sealed class PreloadSettings
{
    public const long MaxBudgetMb = 12288;
    public const long ReservedForOmsiMb = 4096;

    public long? RamBudgetMb { get; private init; }
    public bool Monitor { get; private init; }
    public int TileRadius { get; private init; } = 12;
    public VehicleScope? AiVehicles { get; private init; } = VehicleScope.Complete;
    public string UserVehicles { get; private init; } = "none";
    public IReadOnlyList<string> UserVehicleList { get; private init; } = Array.Empty<string>();
    public VehicleScope UserScope { get; private init; } = VehicleScope.Essential;
    public VehicleScope FleetScope { get; private init; } = VehicleScope.Load;
    public bool Sounds { get; private init; } = true;
    public PreOpenMode PreOpen { get; private init; } = PreOpenMode.Learned;
    public bool PreOpenOnFirstUse { get; private init; } = true;
    public bool Learn { get; private init; } = true;
    public bool Survey { get; private init; } = true;

    public static PreloadSettings From(IReadOnlyDictionary<string, string> values)
    {
        string Get(string key, string fallback) => values.TryGetValue(key, out var value) ? value : fallback;
        bool Flag(string key, bool fallback) => Get(key, fallback ? "true" : "false") == "true";
        VehicleScope Scope(string value, VehicleScope fallback) => value switch { "essential" => VehicleScope.Essential, "load" => VehicleScope.Load, "complete" => VehicleScope.Complete, _ => fallback };
        var budget = Get("ram_budget_mb", "auto");
        var ai = Get("ai_vehicles", "complete");
        return new PreloadSettings
        {
            RamBudgetMb = long.TryParse(budget, NumberStyles.None, CultureInfo.InvariantCulture, out var mb) ? mb : null,
            Monitor = Flag("monitor", false),
            TileRadius = int.TryParse(Get("tile_radius", "12"), NumberStyles.None, CultureInfo.InvariantCulture, out var radius) ? Math.Clamp(radius, 0, 64) : 12,
            AiVehicles = ai == "off" ? null : Scope(ai, VehicleScope.Complete),
            UserVehicles = Get("user_vehicles", "none"),
            UserVehicleList = Get("user_vehicle_list", "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            UserScope = Scope(Get("user_vehicle_scope", "essential"), VehicleScope.Essential),
            FleetScope = Scope(Get("fleet_scope", "load"), VehicleScope.Load),
            Sounds = Flag("sounds", true),
            PreOpen = Get("pre_open", "learned") switch { "none" => PreOpenMode.None, "all" => PreOpenMode.All, _ => PreOpenMode.Learned },
            PreOpenOnFirstUse = Flag("pre_open_on_first_use", true),
            Learn = Flag("learn", true),
            Survey = Flag("survey", true)
        };
    }

    // auto: 70 % of the RAM free now, after keeping 4 GB for OMSI, at most 12 GB.
    public long BudgetBytes(long availableMb)
    {
        var mb = RamBudgetMb ?? (long)(0.7 * Math.Max(0, availableMb - ReservedForOmsiMb));
        return Math.Clamp(mb, 0, MaxBudgetMb) << 20;
    }
}

// The lines exchanged with OmsiLaunch (UTF-8 JSON, one object per line).
internal sealed class Controller
{
    private readonly StreamWriter output;
    private readonly object gate = new();
    private int? omsiProcessId;

    public Controller(Stream output) => this.output = new StreamWriter(output, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };

    public ManualResetEventSlim Stop { get; } = new();
    public int? OmsiProcessId { get { lock (gate) return omsiProcessId; } }

    public void Send(object message)
    {
        var line = JsonSerializer.Serialize(message);
        lock (gate)
        {
            try { output.WriteLine(line); }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException) { }
        }
    }

    public void Ready() => Send(new Dictionary<string, object> { ["type"] = "ready" });
    public void Event(string name, Dictionary<string, string> data) => Send(new Dictionary<string, object> { ["type"] = "event", ["name"] = name, ["data"] = data });
    public void Error(string detail) => Send(new Dictionary<string, object> { ["type"] = "error", ["data"] = new Dictionary<string, string> { ["code"] = "OL_E_MODULE_OPERATION_FAILED", ["detail"] = detail } });

    // Follows the controller's lines: omsi-started, stop (end of input is a stop too).
    public void Listen(TextReader input)
    {
        var thread = new Thread(() =>
        {
            try
            {
                string? line;
                while ((line = input.ReadLine()) is not null)
                {
                    if (line.Length > 65536) continue;
                    try
                    {
                        using var document = JsonDocument.Parse(line);
                        var root = document.RootElement;
                        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String) continue;
                        if (type.GetString() == "omsi-started" && root.TryGetProperty("pid", out var pid) && pid.TryGetInt32(out var id))
                        {
                            lock (gate) omsiProcessId = id;
                            Log.Write("controller: OMSI started, pid " + id.ToString(CultureInfo.InvariantCulture));
                        }
                        else if (type.GetString() == "stop") { Log.Write("controller: stop"); break; }
                    }
                    catch (JsonException) { }
                }
            }
            catch (IOException) { }
            Stop.Set();
        }) { IsBackground = true, Name = "controller" };
        thread.Start();
    }
}
