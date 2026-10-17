using OmsiLaunch.Api;
using OmsiLaunch.Runtime.Modules;

namespace OmsiLaunch.Modules.Prefetch;

// The prefetch module inside OMSI. The work is done by its native component
// (the file hooks, started by the bridge before this type is armed) and by its
// session program (the memory, outside OMSI); this component only reports the
// session's milestones.
public sealed class PrefetchModule : IRuntimeModule
{
    private IRuntimeModuleContext? context;

    public void Arm(IRuntimeModuleContext context)
    {
        this.context = context;
        context.Emit("armed", new Dictionary<string, string>
        {
            ["channel"] = context.SessionChannelName is null ? "none" : "session-program",
            ["read_ahead"] = Setting("read_ahead"),
            ["write_behind"] = Setting("write_behind"),
            ["read_only_open"] = Setting("read_only_open")
        });
    }

    public void GameplayReady() => context?.Emit("gameplay-ready");

    public IReadOnlyDictionary<string, string> Execute(string operation, IReadOnlyDictionary<string, string> arguments) =>
        throw new ModuleOperationException(PublicErrorCodes.MODULE_OPERATION_UNKNOWN, operation);

    public void Stop() => context?.Emit("stopping");

    private string Setting(string key) => context is not null && context.Settings.TryGetValue(key, out var value) ? value : "";
}
