#include "Feed.h"
#include <string>
#include <unordered_set>
#include <vector>

namespace feed {
namespace {

constexpr size_t QueueMax = 400000;
constexpr size_t SeenMax = 500000;
// The session program serves the pipe before OMSI starts: when it cannot be
// reached for this long, the feed ends (and nothing more is queued).
constexpr DWORD GiveUpMs = 10000;

volatile LONG gLock = 0;
std::unordered_set<unsigned long long>* gSeen = nullptr;
std::vector<std::string>* gQueue = nullptr;
std::vector<std::string>* gUrgent = nullptr;
HANDLE gWake = nullptr;
volatile LONG gConnected = 0;
std::wstring gChannel;
HANDLE gThread = nullptr;
HANDLE gStopEvent = nullptr;
HANDLE gPipe = INVALID_HANDLE_VALUE;
volatile LONG64 gDropped = 0;
volatile LONG gGaveUp = 0;

void Acquire() { while (InterlockedCompareExchange(&gLock, 1, 0) != 0) YieldProcessor(); }
void Release() { InterlockedExchange(&gLock, 0); }

bool Connect() {
    if (gPipe != INVALID_HANDLE_VALUE) return true;
    const HANDLE pipe = CreateFileW(gChannel.c_str(), GENERIC_WRITE, 0, nullptr, OPEN_EXISTING, 0, nullptr);
    if (pipe == INVALID_HANDLE_VALUE) {
        if (GetLastError() == ERROR_PIPE_BUSY) WaitNamedPipeW(gChannel.c_str(), 500);
        return false;
    }
    gPipe = pipe;
    InterlockedExchange(&gConnected, 1);
    return true;
}

void Disconnect() {
    if (gPipe != INVALID_HANDLE_VALUE) { CloseHandle(gPipe); gPipe = INVALID_HANDLE_VALUE; }
    InterlockedExchange(&gConnected, 0);
}

// Sends the urgent lines, then the queue, in chunks; a broken pipe drops the
// batch and reconnects later.
void Send() {
    std::vector<std::string> batch;
    Acquire();
    if (gUrgent) batch.swap(*gUrgent);
    if (gQueue) { batch.insert(batch.end(), gQueue->begin(), gQueue->end()); gQueue->clear(); }
    Release();
    if (batch.empty() || !Connect()) { InterlockedAdd64(&gDropped, static_cast<LONG64>(gPipe == INVALID_HANDLE_VALUE ? batch.size() : 0)); return; }
    std::string chunk;
    auto write = [&]() {
        DWORD written = 0;
        if (!WriteFile(gPipe, chunk.data(), static_cast<DWORD>(chunk.size()), &written, nullptr) || written != chunk.size()) Disconnect();
        chunk.clear();
    };
    for (auto& line : batch) {
        chunk += line;
        if (chunk.size() > 30000) { write(); if (gPipe == INVALID_HANDLE_VALUE) return; }
    }
    if (!chunk.empty()) write();
}

void GiveUp() {
    InterlockedExchange(&gGaveUp, 1);
    Acquire();
    if (gQueue) { InterlockedAdd64(&gDropped, static_cast<LONG64>(gQueue->size())); gQueue->clear(); gQueue->shrink_to_fit(); }
    Release();
}

DWORD WINAPI Run(LPVOID) {
    // While the pipe cannot be reached: retry every half second, up to GiveUpMs.
    // A release request wakes the thread at once.
    ULONGLONG failingSince = 0;
    const HANDLE events[2] = { gStopEvent, gWake };
    for (;;) {
        const DWORD woke = WaitForMultipleObjects(2, events, FALSE, 60);
        if (woke == WAIT_OBJECT_0) break;
        if (gPipe == INVALID_HANDLE_VALUE && !Connect()) {
            const ULONGLONG now = GetTickCount64();
            if (failingSince == 0) failingSince = now;
            if (now - failingSince > GiveUpMs) { GiveUp(); return 0; }
            if (WaitForSingleObject(gStopEvent, 500) != WAIT_TIMEOUT) break;
            continue;
        }
        failingSince = 0;
        Send();
    }
    Send();
    Disconnect();
    return 0;
}

} // namespace

void Start(const wchar_t* channel) {
    if (gThread || !channel || !*channel) return;
    gChannel = channel;
    if (!gSeen) { gSeen = new std::unordered_set<unsigned long long>(); gSeen->reserve(16384); gQueue = new std::vector<std::string>(); gQueue->reserve(8192); gUrgent = new std::vector<std::string>(); }
    gStopEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    gWake = CreateEventW(nullptr, FALSE, FALSE, nullptr);
    gThread = CreateThread(nullptr, 0, Run, nullptr, 0, nullptr);
}

void OnUse(const wchar_t* path, int kind) {
    if (!path || !path[0] || !gSeen || gGaveUp) return;
    unsigned long long hash = 1469598103934665603ull ^ static_cast<unsigned long long>(kind);
    for (const wchar_t* p = path; *p; ++p) {
        const wchar_t c = *p == L'/' ? L'\\' : (*p >= L'A' && *p <= L'Z' ? static_cast<wchar_t>(*p + 32) : *p);
        hash = (hash ^ static_cast<unsigned long long>(c)) * 1099511628211ull;
    }
    // tile_X_Y.map opens are position information: every open is sent, as T.
    const wchar_t* base = path;
    for (const wchar_t* q = path; *q; ++q) if (*q == L'\\' || *q == L'/') base = q + 1;
    const size_t length = wcslen(base);
    const bool tile = kind == 0 && length > 8 && _wcsnicmp(base, L"tile_", 5) == 0 && _wcsicmp(base + length - 4, L".map") == 0;
    char utf8[1200];
    const int n = WideCharToMultiByte(CP_UTF8, 0, path, -1, utf8 + 2, static_cast<int>(sizeof(utf8)) - 4, nullptr, nullptr);
    if (n <= 1) return;
    utf8[0] = tile ? 'T' : (kind ? 'W' : 'U'); utf8[1] = '\t'; utf8[n + 1] = '\n'; utf8[n + 2] = 0;
    Acquire();
    bool queue = tile;
    if (!queue && gSeen->size() < SeenMax) queue = gSeen->insert(hash).second;
    if (queue) {
        if (gQueue->size() < QueueMax) gQueue->push_back(std::string(utf8, static_cast<size_t>(n) + 2));
        else InterlockedIncrement64(&gDropped);
    }
    Release();
}

bool RequestRelease(const wchar_t* path) {
    if (!path || !path[0] || !gUrgent || gGaveUp || !gConnected || !gWake) return false;
    char utf8[1200];
    const int n = WideCharToMultiByte(CP_UTF8, 0, path, -1, utf8 + 2, static_cast<int>(sizeof(utf8)) - 4, nullptr, nullptr);
    if (n <= 1) return false;
    utf8[0] = 'X'; utf8[1] = '\t'; utf8[n + 1] = '\n'; utf8[n + 2] = 0;
    Acquire();
    gUrgent->push_back(std::string(utf8, static_cast<size_t>(n) + 2));
    Release();
    SetEvent(gWake);
    return true;
}

void Stop() {
    if (!gThread) return;
    SetEvent(gStopEvent);
    WaitForSingleObject(gThread, 3000);
    CloseHandle(gThread); gThread = nullptr;
    CloseHandle(gStopEvent); gStopEvent = nullptr;
    CloseHandle(gWake); gWake = nullptr;
}

long long Dropped() { return gDropped; }

} // namespace feed
