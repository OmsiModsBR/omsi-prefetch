// Tests of the prefetch module's native component (x86) against a fake
// bridge: the detours the component hands to request_hook are called exactly
// as Omsi.exe would call its kernel32 imports, on real files in a temporary
// folder, and the use reports are read from a pipe served by the test.
//
// ShimTests.exe [dll]            runs every scenario, each in its own process
// ShimTests.exe dll <scenario>   runs one scenario
#include <windows.h>
#include <stddef.h>
#include <stdio.h>
#include <stdint.h>
#include <map>
#include <mutex>
#include <string>
#include <vector>
#include "omsilaunch_module_native_v1.h"

namespace {

int gFailures = 0;
#define CHECK(condition) do { if (!(condition)) { ++gFailures; printf("  FAIL line %d: %s\n", __LINE__, #condition); } } while (0)

using CreateFn = HANDLE (WINAPI*)(LPCWSTR, DWORD, DWORD, LPSECURITY_ATTRIBUTES, DWORD, DWORD, HANDLE);
using ReadFn = BOOL (WINAPI*)(HANDLE, LPVOID, DWORD, LPDWORD, LPOVERLAPPED);
using WriteFn = BOOL (WINAPI*)(HANDLE, LPCVOID, DWORD, LPDWORD, LPOVERLAPPED);
using CloseFn = BOOL (WINAPI*)(HANDLE);
using SeekFn = DWORD (WINAPI*)(HANDLE, LONG, PLONG, DWORD);
using SizeFn = DWORD (WINAPI*)(HANDLE, LPDWORD);
using TruncateFn = BOOL (WINAPI*)(HANDLE);

// ---- fake bridge
struct Fake {
    omsilaunch_bridge_v1 table{};
    std::map<std::string, void*> detours;
    std::map<std::string, int> refuse;
    std::map<std::string, std::string> values;
    std::vector<std::pair<std::string, std::string>> events;
    std::mutex lock;
};
Fake gFake;

int __cdecl RequestHook(void* context, const char* target, void* detour, void** original) {
    auto* fake = static_cast<Fake*>(context);
    const std::string name = target ? target : "";
    if (fake->refuse.count(name)) return fake->refuse[name];
    const std::string prefix = "iat:kernel32.dll!";
    if (name.rfind(prefix, 0) != 0) return 2;
    const FARPROC real = GetProcAddress(GetModuleHandleW(L"kernel32.dll"), name.substr(prefix.size()).c_str());
    if (!real || !detour || !original) return 2;
    *original = reinterpret_cast<void*>(real);
    fake->detours[name] = detour;
    return 0;
}

int __cdecl Emit(void* context, const char* name, const char* json) {
    auto* fake = static_cast<Fake*>(context);
    std::lock_guard<std::mutex> guard(fake->lock);
    fake->events.emplace_back(name ? name : "", json ? json : "");
    return 0;
}

int __cdecl GetValue(void* context, const char* name, char* buffer, uint32_t capacity, uint32_t* length) {
    auto* fake = static_cast<Fake*>(context);
    const auto found = fake->values.find(name ? name : "");
    if (found == fake->values.end()) return 2;
    *length = static_cast<uint32_t>(found->second.size());
    if (!buffer || capacity < *length + 1) return OMSILAUNCH_BRIDGE_BUFFER_TOO_SMALL;
    memcpy(buffer, found->second.c_str(), *length + 1);
    return 0;
}

std::string EventData(const char* name) {
    std::lock_guard<std::mutex> guard(gFake.lock);
    for (auto& event : gFake.events) if (event.first == name) return event.second;
    return "";
}

// "key":"123" in an event's data.
long long Field(const std::string& json, const char* key) {
    const std::string needle = std::string("\"") + key + "\":\"";
    const size_t at = json.find(needle);
    if (at == std::string::npos) return -999;
    return _atoi64(json.c_str() + at + needle.size());
}

// ---- the detours as Omsi.exe sees them (the real function when not hooked)
struct Api {
    CreateFn create; ReadFn read; WriteFn write; CloseFn close; SeekFn seek; SizeFn size; TruncateFn truncate;
};

template <typename T> T Detour(const char* name, T real) {
    const auto found = gFake.detours.find(std::string("iat:kernel32.dll!") + name);
    return found == gFake.detours.end() ? real : reinterpret_cast<T>(found->second);
}

Api Detours() {
    return Api{ Detour("CreateFileW", &CreateFileW), Detour("ReadFile", &ReadFile), Detour("WriteFile", &WriteFile), Detour("CloseHandle", &CloseHandle),
                Detour("SetFilePointer", &SetFilePointer), Detour("GetFileSize", &GetFileSize), Detour("SetEndOfFile", &SetEndOfFile) };
}

// ---- use reports: the session program's side of the pipe
struct PipeServer {
    HANDLE pipe = INVALID_HANDLE_VALUE;
    HANDLE thread = nullptr;
    std::string received;
    std::mutex lock;
};
PipeServer gServer;

// A file the "session program" (this test) holds mapped; released when the
// component asks for it with an X line.
struct Mapped {
    std::string releaseLine;
    HANDLE mapping = nullptr;
    void* view = nullptr;
    volatile LONG released = 0;
};
Mapped gMapped;

void ReleaseIfAsked() {
    if (gMapped.view && gMapped.releaseLine.size() && gServer.received.find(gMapped.releaseLine) != std::string::npos) {
        UnmapViewOfFile(gMapped.view); gMapped.view = nullptr;
        CloseHandle(gMapped.mapping); gMapped.mapping = nullptr;
        InterlockedExchange(&gMapped.released, 1);
    }
}

DWORD WINAPI Serve(LPVOID) {
    if (!ConnectNamedPipe(gServer.pipe, nullptr) && GetLastError() != ERROR_PIPE_CONNECTED) return 1;
    char buffer[65536];
    DWORD n = 0;
    while (ReadFile(gServer.pipe, buffer, sizeof(buffer), &n, nullptr) && n) {
        std::lock_guard<std::mutex> guard(gServer.lock);
        gServer.received.append(buffer, n);
        ReleaseIfAsked();
    }
    return 0;
}

std::wstring StartServer() {
    wchar_t name[128];
    swprintf_s(name, L"\\\\.\\pipe\\OmsiLaunch.Module.test%lu.loading", GetCurrentProcessId());
    gServer.pipe = CreateNamedPipeW(name, PIPE_ACCESS_INBOUND, PIPE_TYPE_BYTE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS, 1, 0, 1 << 16, 0, nullptr);
    gServer.thread = CreateThread(nullptr, 0, Serve, nullptr, 0, nullptr);
    return name;
}

std::string Utf8(const std::wstring& text) {
    const int n = WideCharToMultiByte(CP_UTF8, 0, text.c_str(), -1, nullptr, 0, nullptr, nullptr);
    std::string out(static_cast<size_t>(n > 0 ? n - 1 : 0), '\0');
    if (n > 1) WideCharToMultiByte(CP_UTF8, 0, text.c_str(), -1, &out[0], n, nullptr, nullptr);
    return out;
}

size_t CountLines(const std::string& line) {
    std::lock_guard<std::mutex> guard(gServer.lock);
    size_t count = 0;
    for (size_t at = 0; (at = gServer.received.find(line, at)) != std::string::npos; at += line.size())
        if (at == 0 || gServer.received[at - 1] == '\n') ++count;
    return count;
}

// Waits until every line arrived (at least `times` each).
bool WaitLines(const std::vector<std::pair<std::string, size_t>>& lines, DWORD timeoutMs) {
    const ULONGLONG until = GetTickCount64() + timeoutMs;
    for (;;) {
        bool all = true;
        for (auto& line : lines) if (CountLines(line.first) < line.second) { all = false; break; }
        if (all) return true;
        if (GetTickCount64() > until) return false;
        Sleep(20);
    }
}

// ---- files
std::vector<uint8_t> Data(size_t n, uint32_t seed) {
    std::vector<uint8_t> data(n);
    for (auto& b : data) { seed = seed * 1103515245u + 12345u; b = static_cast<uint8_t>(seed >> 16); }
    return data;
}

void WriteAll(const std::wstring& path, const std::vector<uint8_t>& data) {
    const HANDLE h = CreateFileW(path.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, 0, nullptr);
    DWORD written = 0;
    if (h != INVALID_HANDLE_VALUE) { WriteFile(h, data.data(), static_cast<DWORD>(data.size()), &written, nullptr); CloseHandle(h); }
}

std::vector<uint8_t> ReadAll(const std::wstring& path) {
    std::vector<uint8_t> data;
    const HANDLE h = CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, 0, nullptr);
    if (h == INVALID_HANDLE_VALUE) return data;
    LARGE_INTEGER size{};
    GetFileSizeEx(h, &size);
    data.resize(static_cast<size_t>(size.QuadPart));
    DWORD read = 0;
    if (!data.empty()) ReadFile(h, data.data(), static_cast<DWORD>(data.size()), &read, nullptr);
    data.resize(read);
    CloseHandle(h);
    return data;
}

std::wstring gRoot;

std::wstring MakeRoot() {
    wchar_t temp[MAX_PATH];
    GetTempPathW(MAX_PATH, temp);
    wchar_t root[MAX_PATH];
    swprintf_s(root, L"%sOmsiLaunchLoadingTests-%lu\\", temp, GetCurrentProcessId());
    gRoot = root;
    for (const wchar_t* folder : { L"", L"vehicles", L"vehicles\\Bus", L"maps", L"maps\\Test" }) CreateDirectoryW((gRoot + folder).c_str(), nullptr);
    return gRoot;
}

void RemoveTree(const std::wstring& folder) {
    WIN32_FIND_DATAW found;
    const HANDLE find = FindFirstFileW((folder + L"*").c_str(), &found);
    if (find != INVALID_HANDLE_VALUE) {
        do {
            const std::wstring name = found.cFileName;
            if (name == L"." || name == L"..") continue;
            if (found.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) RemoveTree(folder + name + L"\\");
            else DeleteFileW((folder + name).c_str());
        } while (FindNextFileW(find, &found));
        FindClose(find);
    }
    RemoveDirectoryW(folder.c_str());
}

// ---- component
using StartFn = int (__cdecl*)(const omsilaunch_bridge_v1*);
using StopFn = void (__cdecl*)(void);
StartFn gStart = nullptr;
StopFn gStop = nullptr;

bool Load(const wchar_t* dll) {
    const HMODULE module = LoadLibraryW(dll);
    if (!module) { printf("  cannot load %ls (%lu)\n", dll, GetLastError()); return false; }
    gStart = reinterpret_cast<StartFn>(GetProcAddress(module, "omsilaunch_module_native_v1_start"));
    gStop = reinterpret_cast<StopFn>(GetProcAddress(module, "omsilaunch_module_native_v1_stop"));
    return gStart && gStop;
}

void Bridge(bool features, bool channel) {
    gFake.table.size = sizeof(omsilaunch_bridge_v1);
    gFake.table.version = OMSILAUNCH_MODULE_NATIVE_V1;
    gFake.table.context = &gFake;
    gFake.table.request_hook = RequestHook;
    gFake.table.emit = Emit;
    gFake.table.get_value = GetValue;
    const char* on = features ? "true" : "false";
    gFake.values["setting.read_ahead"] = on;
    gFake.values["setting.write_behind"] = on;
    gFake.values["setting.read_only_open"] = on;
    gFake.values["setting.read_only_folders"] = "vehicles\\";
    if (channel) gFake.values["session_channel"] = Utf8(StartServer());
    gFake.values["omsi_version"] = "2.3.004";
}

// ---- checks
// Byte by byte, with the position and size queries the Delphi runtime makes
// on every Eof(), then seeks of every kind and reads of every size.
void ReadSequence(const Api& api, HANDLE h, const std::vector<uint8_t>& data) {
    const DWORD size = static_cast<DWORD>(data.size());
    bool same = true;
    for (DWORD i = 0; i < size && same; ++i) {
        DWORD high = 1;
        same = api.seek(h, 0, nullptr, FILE_CURRENT) == i && api.size(h, &high) == size && high == 0;
        uint8_t b = 0; DWORD got = 0;
        same = same && api.read(h, &b, 1, &got, nullptr) && got == 1 && b == data[i];
        if (!same) printf("  byte %lu differs\n", i);
    }
    CHECK(same);
    uint8_t b = 0; DWORD got = 7;
    CHECK(api.read(h, &b, 1, &got, nullptr) && got == 0);   // end of file

    uint32_t seed = 7;
    bool ok = true;
    for (int i = 0; i < 4000 && ok; ++i) {
        seed = seed * 1103515245u + 12345u;
        const DWORD method = (seed >> 8) % 3;
        LONG distance;
        const DWORD current = api.seek(h, 0, nullptr, FILE_CURRENT);
        const DWORD target = (seed >> 4) % size;
        if (method == FILE_BEGIN) distance = static_cast<LONG>(target);
        else if (method == FILE_CURRENT) distance = static_cast<LONG>(target) - static_cast<LONG>(current);
        else distance = static_cast<LONG>(target) - static_cast<LONG>(size);
        ok = api.seek(h, distance, nullptr, method) == target;
        const DWORD count = (seed >> 12) % 5 == 0 ? 300 + (seed >> 20) % 5000 : 1 + (seed >> 20) % 200;
        std::vector<uint8_t> buffer(count);
        DWORD read = 0;
        ok = ok && api.read(h, buffer.data(), count, &read, nullptr);
        const DWORD expected = target + count <= size ? count : size - target;
        ok = ok && read == expected && memcmp(buffer.data(), data.data() + target, read) == 0;
        ok = ok && api.seek(h, 0, nullptr, FILE_CURRENT) == target + read;
        if (!ok) printf("  seek %d (method %lu to %lu, %lu bytes) differs\n", i, method, target, count);
    }
    CHECK(ok);
    // The system's answers at the edges: a negative position, and one past
    // 4 GiB (low part 0xFFFFFFFF with no error), where nothing is read.
    const DWORD before = api.seek(h, 0, nullptr, FILE_CURRENT);
    LONG high = -1;
    SetLastError(0);
    CHECK(api.seek(h, -1, &high, FILE_BEGIN) == INVALID_SET_FILE_POINTER && GetLastError() == ERROR_NEGATIVE_SEEK);
    CHECK(api.seek(h, 0, nullptr, FILE_CURRENT) == before);
    high = 0;
    SetLastError(1);
    CHECK(api.seek(h, -1, &high, FILE_BEGIN) == 0xFFFFFFFFu && GetLastError() == NO_ERROR && high == 0);
    b = 0; got = 7;
    CHECK(api.read(h, &b, 1, &got, nullptr) && got == 0);
    high = 0;
    CHECK(api.seek(h, 0, &high, FILE_CURRENT) == 0xFFFFFFFFu && high == 0);
    CHECK(api.seek(h, static_cast<LONG>(before), nullptr, FILE_BEGIN) == before);
}

void ReadAhead(const Api& api, const std::wstring& path, std::vector<uint8_t>& data) {
    printf("read-ahead\n");
    const HANDLE h = api.create(path.c_str(), GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    CHECK(h != INVALID_HANDLE_VALUE);
    ReadSequence(api, h, data);
    // A write ends the shim for the handle: it lands where the game expects.
    CHECK(api.seek(h, 1000, nullptr, FILE_BEGIN) == 1000);
    for (int i = 0; i < 10; ++i) { uint8_t b = 0; DWORD got = 0; api.read(h, &b, 1, &got, nullptr); }
    const uint8_t patch[4] = { 0xDE, 0xAD, 0xBE, 0xEF };
    DWORD written = 0;
    CHECK(api.write(h, patch, 4, &written, nullptr) && written == 4);
    memcpy(data.data() + 1010, patch, 4);
    CHECK(api.seek(h, 0, nullptr, FILE_CURRENT) == 1014);
    uint8_t next[4] = {}; DWORD got = 0;
    CHECK(api.read(h, next, 4, &got, nullptr) && got == 4 && memcmp(next, data.data() + 1014, 4) == 0);
    CHECK(api.close(h));
    CHECK(ReadAll(path) == data);
}

void WriteBehind(const Api& api, const std::wstring& path) {
    printf("write-behind\n");
    const auto data = Data(600 * 1024 + 123, 3);
    const HANDLE h = api.create(path.c_str(), GENERIC_WRITE, FILE_SHARE_READ, nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    CHECK(h != INVALID_HANDLE_VALUE);
    bool ok = true;
    for (size_t i = 0; i < data.size() && ok; ++i) {
        DWORD written = 0;
        ok = api.write(h, &data[i], 1, &written, nullptr) && written == 1;
        if (i + 1 == 300000) {
            // Another open of the file finds every byte written so far.
            const HANDLE reader = api.create(path.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, 0, nullptr);
            CHECK(reader != INVALID_HANDLE_VALUE);
            std::vector<uint8_t> seen(400000);
            DWORD read = 0;
            CHECK(api.read(reader, seen.data(), static_cast<DWORD>(seen.size()), &read, nullptr) && read == 300000);
            CHECK(memcmp(seen.data(), data.data(), 300000) == 0);
            CHECK(api.close(reader));
        }
    }
    CHECK(ok);
    // A position query sees the bytes written.
    CHECK(api.seek(h, 0, nullptr, FILE_CURRENT) == data.size());
    CHECK(api.close(h));
    CHECK(ReadAll(path) == data);
}

void ReadOnlyOpen(const Api& api, const std::wstring& path) {
    printf("read-only open\n");
    auto data = Data(5000, 5);
    WriteAll(path, data);
    const HANDLE h = api.create(path.c_str(), GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    CHECK(h != INVALID_HANDLE_VALUE);
    // The handle itself has no write access...
    DWORD written = 0;
    CHECK(!WriteFile(h, "x", 1, &written, nullptr) && GetLastError() == ERROR_ACCESS_DENIED);
    for (int i = 0; i < 10; ++i) { uint8_t b = 0; DWORD got = 0; CHECK(api.read(h, &b, 1, &got, nullptr) && got == 1 && b == data[static_cast<size_t>(i)]); }
    // ...but a write through the hooks is served from a reopened handle.
    const uint8_t patch[3] = { 1, 2, 3 };
    CHECK(api.write(h, patch, 3, &written, nullptr) && written == 3);
    memcpy(data.data() + 10, patch, 3);
    CHECK(api.seek(h, 0, nullptr, FILE_CURRENT) == 13);
    uint8_t next[2] = {}; DWORD got = 0;
    CHECK(api.read(h, next, 2, &got, nullptr) && got == 2 && memcmp(next, data.data() + 13, 2) == 0);
    CHECK(api.size(h, nullptr) == 5000);
    CHECK(api.seek(h, 20, nullptr, FILE_BEGIN) == 20);
    CHECK(api.truncate(h));
    CHECK(api.size(h, nullptr) == 20);
    data.resize(20);
    CHECK(api.close(h));
    CHECK(ReadAll(path) == data);
}

// A file the session program maps cannot be replaced (ERROR_USER_MAPPED_FILE):
// the component asks for the release (X line) and retries, so the game's
// CreateFileW succeeds.
void MappedFile(const Api& api, const std::wstring& path) {
    printf("held file replaced by the game\n");
    WriteAll(path, Data(50000, 12));
    const HANDLE file = CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, 0, nullptr);
    gMapped.mapping = CreateFileMappingW(file, nullptr, PAGE_READONLY, 0, 0, nullptr);
    CloseHandle(file);
    gMapped.view = MapViewOfFile(gMapped.mapping, FILE_MAP_READ, 0, 0, 0);
    CHECK(gMapped.view != nullptr);
    // The system refuses to replace a mapped file...
    const HANDLE refused = CreateFileW(path.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, 0, nullptr);
    CHECK(refused == INVALID_HANDLE_VALUE && GetLastError() == ERROR_USER_MAPPED_FILE);
    if (refused != INVALID_HANDLE_VALUE) CloseHandle(refused);
    {
        std::lock_guard<std::mutex> guard(gServer.lock);
        gMapped.releaseLine = "X\t" + Utf8(path) + "\n";
    }
    // ...but through the hooks the request reaches the session program first.
    const HANDLE h = api.create(path.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, 0, nullptr);
    CHECK(h != INVALID_HANDLE_VALUE);
    CHECK(gMapped.released == 1);
    DWORD written = 0;
    CHECK(api.write(h, "new", 3, &written, nullptr) && written == 3);
    CHECK(api.close(h));
    CHECK(ReadAll(path) == std::vector<uint8_t>({ 'n', 'e', 'w' }));
}

void Tiles(const Api& api, const std::wstring& path) {
    printf("tile opens\n");
    WriteAll(path, Data(100, 9));
    for (int i = 0; i < 2; ++i) {
        const HANDLE h = api.create(path.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, 0, nullptr);
        CHECK(h != INVALID_HANDLE_VALUE);
        CHECK(api.close(h));
    }
}

// stop() mid-session: buffered state is handed back to the handles, which
// the game then uses without the hooks.
void Drain(const Api& api, const std::wstring& readPath, const std::vector<uint8_t>& data, const std::wstring& writePath) {
    printf("stop with open handles\n");
    const HANDLE r = api.create(readPath.c_str(), GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, 0, nullptr);
    for (size_t i = 0; i < 20; ++i) { uint8_t b = 0; DWORD got = 0; CHECK(api.read(r, &b, 1, &got, nullptr) && got == 1 && b == data[i]); }
    CHECK(SetFilePointer(r, 0, nullptr, FILE_CURRENT) != 20);   // the kernel position is a block ahead
    const HANDLE w = api.create(writePath.c_str(), GENERIC_WRITE, FILE_SHARE_READ, nullptr, CREATE_ALWAYS, 0, nullptr);
    const auto bytes = Data(1000, 11);
    for (auto& b : bytes) { DWORD written = 0; CHECK(api.write(w, &b, 1, &written, nullptr) && written == 1); }
    CHECK(ReadAll(writePath).empty());                          // still buffered
    gStop();
    CHECK(SetFilePointer(r, 0, nullptr, FILE_CURRENT) == 20);
    uint8_t four[4] = {}; DWORD got = 0;
    CHECK(ReadFile(r, four, 4, &got, nullptr) && got == 4 && memcmp(four, data.data() + 20, 4) == 0);
    CHECK(ReadAll(writePath) == bytes);
    // The detours pass through from now on.
    uint8_t b = 0;
    CHECK(api.read(r, &b, 1, &got, nullptr) && got == 1 && b == data[24]);
    CHECK(api.close(r));
    CHECK(api.close(w));
}

// ---- scenarios
int Main(const wchar_t* dll) {
    MakeRoot();
    const std::wstring big = gRoot + L"maps\\Test\\big.sco";
    const std::wstring named = gRoot + L"maps\\Test\\estação.sco";
    const std::wstring rdy = gRoot + L"maps\\Test\\tile_0_0.map.terrain_1.rdy";
    const std::wstring rdy2 = gRoot + L"maps\\Test\\tile_0_1.map.terrain_1.rdy";
    const std::wstring cfg = gRoot + L"vehicles\\Bus\\a.cfg";
    const std::wstring tile = gRoot + L"maps\\Test\\tile_1_2.map";
    auto data = Data(300 * 1024 + 77, 1);
    WriteAll(big, data);
    WriteAll(named, Data(10, 2));
    if (!Load(dll)) return 2;
    Bridge(true, true);
    CHECK(gStart(&gFake.table) == 0);
    const std::string hooks = EventData("hooks");
    for (const char* key : { "create", "read", "close", "write", "seek", "size", "truncate" }) CHECK(Field(hooks, key) == 0);
    CHECK(Field(hooks, "tex_ex_w") == -1);                      // no d3dx9.dll in this process
    CHECK(Field(hooks, "feed") == 1);
    const Api api = Detours();
    ReadAhead(api, big, data);
    WriteBehind(api, rdy);
    ReadOnlyOpen(api, cfg);
    Tiles(api, tile);
    MappedFile(api, gRoot + L"maps\\Test\\held.cfg");
    const HANDLE h = api.create(named.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, 0, nullptr);
    CHECK(h != INVALID_HANDLE_VALUE && api.close(h));

    printf("use reports\n");
    CHECK(WaitLines({ { "U\t" + Utf8(big) + "\n", 1 }, { "W\t" + Utf8(big) + "\n", 1 }, { "U\t" + Utf8(rdy) + "\n", 1 }, { "W\t" + Utf8(rdy) + "\n", 1 },
                      { "U\t" + Utf8(cfg) + "\n", 1 }, { "W\t" + Utf8(cfg) + "\n", 1 }, { "T\t" + Utf8(tile) + "\n", 2 }, { "U\t" + Utf8(named) + "\n", 1 } }, 5000));
    CHECK(CountLines("V\t2.3.004\n") == 1);                  // the OMSI version of bridge protocol 3, once
    CHECK(CountLines("U\t" + Utf8(big) + "\n") == 1);         // first use only
    CHECK(CountLines("W\t" + Utf8(cfg) + "\n") == 1);         // first write only
    CHECK(CountLines("U\t" + Utf8(tile) + "\n") == 0);        // tile opens are T lines

    Drain(api, big, data, rdy2);
    const std::string stats = EventData("stats");
    printf("  stats %s\n", stats.c_str());
    CHECK(Field(stats, "served_from_buffer") > 0);
    CHECK(Field(stats, "app_reads") > 20 * Field(stats, "real_reads"));
    CHECK(Field(stats, "write_behind_app_writes") >= 600 * 1024);
    CHECK(Field(stats, "write_behind_real_writes") > 0 && Field(stats, "write_behind_real_writes") < 20);
    CHECK(Field(stats, "write_behind_failed") == 0);
    CHECK(Field(stats, "read_only_opens") == 1);
    CHECK(Field(stats, "read_only_reopened") == 1);
    CHECK(Field(stats, "read_only_reopen_failed") == 0);
    CHECK(Field(stats, "track_full") == 0);
    CHECK(Field(stats, "release_requests") == 1);
    CHECK(Field(stats, "feed_dropped") == 0);
    RemoveTree(gRoot);
    return 0;
}

// The position hooks are refused: nothing is shimmed, everything stays exact.
int MissingHooks(const wchar_t* dll) {
    MakeRoot();
    const std::wstring big = gRoot + L"maps\\Test\\big.sco";
    const std::wstring cfg = gRoot + L"vehicles\\Bus\\a.cfg";
    auto data = Data(100 * 1024, 4);
    WriteAll(big, data);
    WriteAll(cfg, Data(100, 6));
    if (!Load(dll)) return 2;
    Bridge(true, false);
    gFake.refuse["iat:kernel32.dll!SetFilePointer"] = 2;
    CHECK(gStart(&gFake.table) == 0);
    const std::string hooks = EventData("hooks");
    CHECK(Field(hooks, "seek") == 2);
    CHECK(Field(hooks, "feed") == 0);
    const Api api = Detours();
    const HANDLE h = api.create(big.c_str(), GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ, nullptr, OPEN_EXISTING, 0, nullptr);
    ReadSequence(api, h, data);
    CHECK(api.close(h));
    const HANDLE c = api.create(cfg.c_str(), GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ, nullptr, OPEN_EXISTING, 0, nullptr);
    DWORD written = 0;
    CHECK(WriteFile(c, "x", 1, &written, nullptr) && written == 1);   // opened as asked
    CHECK(api.close(c));
    gStop();
    const std::string stats = EventData("stats");
    CHECK(Field(stats, "served_from_buffer") == 0);
    CHECK(Field(stats, "read_only_opens") == 0);
    RemoveTree(gRoot);
    return 0;
}

// Settings turn every feature off: only the use reports remain.
int FeaturesOff(const wchar_t* dll) {
    MakeRoot();
    const std::wstring big = gRoot + L"maps\\Test\\big.sco";
    auto data = Data(70 * 1024, 8);
    WriteAll(big, data);
    if (!Load(dll)) return 2;
    Bridge(false, true);
    CHECK(gStart(&gFake.table) == 0);
    const std::string hooks = EventData("hooks");
    CHECK(Field(hooks, "seek") == -1);                          // not requested
    const Api api = Detours();
    const HANDLE h = api.create(big.c_str(), GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ, nullptr, OPEN_EXISTING, 0, nullptr);
    ReadSequence(api, h, data);
    CHECK(api.close(h));
    CHECK(WaitLines({ { "U\t" + Utf8(big) + "\n", 1 } }, 5000));
    gStop();
    const std::string stats = EventData("stats");
    CHECK(Field(stats, "served_from_buffer") == 0);
    CHECK(Field(stats, "app_reads") == Field(stats, "real_reads"));
    RemoveTree(gRoot);
    return 0;
}

// A protocol 1 bridge has no get_value: the component refuses to start.
int Protocol1(const wchar_t* dll) {
    if (!Load(dll)) return 2;
    Bridge(true, false);
    gFake.table.size = static_cast<uint32_t>(offsetof(omsilaunch_bridge_v1, get_value));
    CHECK(gStart(&gFake.table) == 1);
    CHECK(gFake.detours.empty());
    return 0;
}

// CreateFileW refused: start fails; the bridge then calls stop.
int CoreRefused(const wchar_t* dll) {
    if (!Load(dll)) return 2;
    Bridge(true, false);
    gFake.refuse["iat:kernel32.dll!CreateFileW"] = 4;
    CHECK(gStart(&gFake.table) != 0);
    gStop();
    CHECK(!EventData("stats").empty());
    return 0;
}

int RunScenario(const wchar_t* dll, const std::wstring& name) {
    int result = 3;
    if (name == L"main") result = Main(dll);
    else if (name == L"missing-hooks") result = MissingHooks(dll);
    else if (name == L"features-off") result = FeaturesOff(dll);
    else if (name == L"protocol1") result = Protocol1(dll);
    else if (name == L"core-refused") result = CoreRefused(dll);
    if (gServer.pipe != INVALID_HANDLE_VALUE) { DisconnectNamedPipe(gServer.pipe); CloseHandle(gServer.pipe); }
    return result != 0 ? result : (gFailures ? 1 : 0);
}

} // namespace

int wmain(int argc, wchar_t** argv) {
    std::wstring dll;
    if (argc > 1) dll = argv[1];
    else {
        wchar_t self[MAX_PATH];
        GetModuleFileNameW(nullptr, self, MAX_PATH);
        dll = self;
        dll = dll.substr(0, dll.find_last_of(L'\\') + 1) + L"..\\..\\native\\prefetch-native.dll";
    }
    if (argc > 2) return RunScenario(dll.c_str(), argv[2]);

    int failed = 0;
    for (const wchar_t* scenario : { L"main", L"missing-hooks", L"features-off", L"protocol1", L"core-refused" }) {
        wchar_t self[MAX_PATH];
        GetModuleFileNameW(nullptr, self, MAX_PATH);
        std::wstring command = L"\"" + std::wstring(self) + L"\" \"" + dll + L"\" " + scenario;
        STARTUPINFOW si{ sizeof(si) };
        PROCESS_INFORMATION pi{};
        printf("== %ls\n", scenario);
        fflush(stdout);
        if (!CreateProcessW(nullptr, &command[0], nullptr, nullptr, FALSE, 0, nullptr, nullptr, &si, &pi)) { printf("cannot start (%lu)\n", GetLastError()); ++failed; continue; }
        WaitForSingleObject(pi.hProcess, 120000);
        DWORD code = 1;
        GetExitCodeProcess(pi.hProcess, &code);
        CloseHandle(pi.hThread); CloseHandle(pi.hProcess);
        printf("%s %ls\n", code == 0 ? "PASS" : "FAIL", scenario);
        if (code != 0) ++failed;
    }
    printf(failed ? "%d scenario(s) failed\n" : "all scenarios passed\n", failed);
    return failed ? 1 : 0;
}
