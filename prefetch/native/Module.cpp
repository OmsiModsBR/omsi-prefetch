#include <windows.h>
#include <stdio.h>
#include <string>
#include "omsilaunch_module_native_v1.h"
#include "FileShim.h"
#include "Feed.h"

// Native component of the prefetch module (omsilaunch_module_native_v1,
// bridge protocol 2): reads its settings from the bridge, asks the bridge for
// the hooks of the file shim, and reports what OMSI uses to the module's
// session program. stop() drains the shim before the bridge removes the hooks.
namespace {

const omsilaunch_bridge_v1* gBridge = nullptr;

std::wstring Value(const char* name) {
    char buffer[2048] = {};
    uint32_t length = 0;
    if (gBridge->get_value(gBridge->context, name, buffer, sizeof(buffer), &length) != 0) return L"";
    wchar_t wide[2048] = {};
    MultiByteToWideChar(CP_UTF8, 0, buffer, -1, wide, 2047);
    return wide;
}

bool Setting(const char* key, bool fallback) {
    const std::wstring value = Value((std::string("setting.") + key).c_str());
    if (value == L"true") return true;
    if (value == L"false") return false;
    return fallback;
}

int Request(const char* target, void* detour, void** original) {
    return gBridge->request_hook(gBridge->context, target, detour, original);
}

std::wstring gFolders;

} // namespace

extern "C" __declspec(dllexport) int __cdecl omsilaunch_module_native_v1_start(const omsilaunch_bridge_v1* bridge) {
    // Settings come through get_value (bridge protocol 2).
    if (bridge == nullptr || bridge->version != OMSILAUNCH_MODULE_NATIVE_V1 || bridge->size < OMSILAUNCH_BRIDGE_V1_SIZE_WITH_GET_VALUE || bridge->get_value == nullptr) return 1;
    gBridge = bridge;
    fileshim::InitChain();
    fileshim::Options options;
    options.readAhead = Setting("read_ahead", true);
    options.writeBehind = Setting("write_behind", true);
    options.readOnlyOpen = Setting("read_only_open", true);
    gFolders = Value("setting.read_only_folders");
    options.readOnlyPrefixes = gFolders.empty() ? L"vehicles\\" : gFolders.c_str();
    fileshim::Configure(options, feed::OnUse, feed::RequestRelease);

    auto& chain = fileshim::chain;
    const int create = Request("iat:kernel32.dll!CreateFileW", reinterpret_cast<void*>(&fileshim::HookCreateFileW), reinterpret_cast<void**>(&chain.createFileW));
    const int read = Request("iat:kernel32.dll!ReadFile", reinterpret_cast<void*>(&fileshim::HookReadFile), reinterpret_cast<void**>(&chain.readFile));
    const int close = Request("iat:kernel32.dll!CloseHandle", reinterpret_cast<void*>(&fileshim::HookCloseHandle), reinterpret_cast<void**>(&chain.closeHandle));
    const int write = Request("iat:kernel32.dll!WriteFile", reinterpret_cast<void*>(&fileshim::HookWriteFile), reinterpret_cast<void**>(&chain.writeFile));
    int seek = -1, size = -1, truncate = -1;
    if (options.readAhead || options.writeBehind || options.readOnlyOpen) {
        seek = Request("iat:kernel32.dll!SetFilePointer", reinterpret_cast<void*>(&fileshim::HookSetFilePointer), reinterpret_cast<void**>(&chain.setFilePointer));
        size = Request("iat:kernel32.dll!GetFileSize", reinterpret_cast<void*>(&fileshim::HookGetFileSize), reinterpret_cast<void**>(&chain.getFileSize));
        truncate = Request("iat:kernel32.dll!SetEndOfFile", reinterpret_cast<void*>(&fileshim::HookSetEndOfFile), reinterpret_cast<void**>(&chain.setEndOfFile));
    }
    // The features start only now, with every hook they need in place.
    fileshim::Confirm(create == 0 && read == 0 && close == 0 && write == 0 && seek == 0 && size == 0 && truncate == 0);
    int texExW = -1, texExA = -1, texW = -1, mesh = -1;
    if (chain.texExW) texExW = Request("iat:d3dx9.dll!D3DXCreateTextureFromFileExW", reinterpret_cast<void*>(&fileshim::HookTexExW), reinterpret_cast<void**>(&chain.texExW));
    if (chain.texExA) texExA = Request("iat:d3dx9.dll!D3DXCreateTextureFromFileExA", reinterpret_cast<void*>(&fileshim::HookTexExA), reinterpret_cast<void**>(&chain.texExA));
    if (chain.texW) texW = Request("iat:d3dx9.dll!D3DXCreateTextureFromFileW", reinterpret_cast<void*>(&fileshim::HookTexW), reinterpret_cast<void**>(&chain.texW));
    if (chain.meshXA) mesh = Request("iat:d3dx9.dll!D3DXLoadMeshFromXA", reinterpret_cast<void*>(&fileshim::HookMeshXA), reinterpret_cast<void**>(&chain.meshXA));

    const std::wstring channel = Value("session_channel");
    if (!channel.empty()) feed::Start(channel.c_str());

    char json[512];
    sprintf_s(json, "{\"create\":\"%d\",\"read\":\"%d\",\"close\":\"%d\",\"write\":\"%d\",\"seek\":\"%d\",\"size\":\"%d\",\"truncate\":\"%d\",\"tex_ex_w\":\"%d\",\"tex_ex_a\":\"%d\",\"tex_w\":\"%d\",\"mesh\":\"%d\",\"feed\":\"%d\"}",
        create, read, close, write, seek, size, truncate, texExW, texExA, texW, mesh, channel.empty() ? 0 : 1);
    bridge->emit(bridge->context, "hooks", json);
    // Without these three the module does nothing useful: the phase fails and
    // the bridge removes whatever was installed.
    return create == 0 && read == 0 && close == 0 ? 0 : 2;
}

extern "C" __declspec(dllexport) void __cdecl omsilaunch_module_native_v1_stop(void) {
    fileshim::Drain();
    feed::Stop();
    if (gBridge == nullptr) return;
    const auto stats = fileshim::GetStats();
    char json[720];
    sprintf_s(json, "{\"app_reads\":\"%lld\",\"real_reads\":\"%lld\",\"served_from_buffer\":\"%lld\",\"shim_stopped\":\"%lld\",\"write_behind_app_writes\":\"%lld\",\"write_behind_real_writes\":\"%lld\",\"write_behind_failed\":\"%lld\",\"read_only_opens\":\"%lld\",\"read_only_reopened\":\"%lld\",\"read_only_reopen_failed\":\"%lld\",\"track_full\":\"%lld\",\"release_requests\":\"%lld\",\"feed_dropped\":\"%lld\"}",
        stats.appReads, stats.realReads, stats.servedFromBuffer, stats.shimStopped, stats.wbAppWrites, stats.wbRealWrites, stats.wbFailedFlushes,
        stats.roDowngraded, stats.roPromoted, stats.roPromoteFailed, stats.trackFull, stats.releaseRequests, feed::Dropped());
    gBridge->emit(gBridge->context, "stats", json);
}
