#include "FileShim.h"
#include <stddef.h>
#include <stdlib.h>
#include <string.h>
#include <wchar.h>
#include <string>
#include <vector>

// Ported from the loading PoC (OmsiLoadingPoC v2, iat_hooks.cpp) to the
// OmsiLaunch bridge: the bridge installs the hooks; this file only holds the
// detours and their state. Changes from the PoC: no observer hooks, no timing
// per call, the write-behind map scan takes the table lock, a failed flush is
// retried and turns write-behind off, read-only opens share writing so that a
// write the game makes after all can be served from a reopened handle, the
// features start only once every hook they need is installed, each detour
// holds its handle's operation lock, and Drain() hands every handle back
// before the hooks go.
//
// Locks, always taken in this order: the table lock (gTrackLock, short), a
// handle's operation lock (Tracked::opLock, held by the detour serving the
// handle for the whole call), a handle's buffer lock (Tracked::writeLock).

namespace fileshim {

Chain chain{};

namespace {

constexpr uint32_t ReadAheadBlock = 64 * 1024;
constexpr DWORD ReadAheadSmallMax = 256;       // requests up to this size are coalesced
constexpr uint32_t ReadAheadSequentialStart = 8; // consecutive tiny reads before buffering starts
constexpr uint32_t WriteBehindBlock = 256 * 1024;
constexpr int TrackMax = 4096;

struct Tracked {
    volatile LONG opLock;            // first member: ReleaseSlot keeps it
    bool drained;                    // handed back by Drain(): served natively
    HANDLE h;
    wchar_t* path;
    bool readAhead;                  // eligible for read-ahead
    uint8_t* buffer; uint32_t bufferLength; uint64_t bufferPosition;
    uint64_t logical, real;          // the application's position / the kernel's
    uint64_t size;                   // cached at open (read-only files do not change while read)
    uint32_t sequentialTiny;
    bool writeBehind; uint8_t* writeBuffer; uint32_t writeLength; volatile LONG writeLock;
    bool readOnly;                   // opened read-only although the game asked for write access
    DWORD access, share, flags;      // what the game asked for (to reopen a read-only handle)
    HANDLE promoted;                 // the reopened handle with the access the game asked for
    bool reportedWrite;              // the first write was reported
};

const HANDLE Tomb = reinterpret_cast<HANDLE>(static_cast<uintptr_t>(-1));
Tracked gTrack[TrackMax] = {};
volatile LONG gTrackLock = 0;
__declspec(thread) int tReentry = 0;

Options gOptions;
bool gReadAhead = false, gWriteBehind = false, gReadOnlyOpen = false;
volatile LONG gPassThrough = 0;
UseCallback gOnUse = nullptr;
ReleaseCallback gOnRelease = nullptr;
// How long a call refused with ERROR_USER_MAPPED_FILE waits for the release.
constexpr int ReleaseRetries = 60;
constexpr DWORD ReleaseRetryMs = 25;
std::vector<std::wstring> gReadOnlyPrefixes;   // lower case, trailing backslash
volatile LONG gWriteBehindActive = 0;

volatile LONG64 sAppReads = 0, sRealReads = 0, sServed = 0, sShimStopped = 0, sWbAppWrites = 0, sWbRealWrites = 0, sWbFailed = 0,
    sRoDowngraded = 0, sRoPromoted = 0, sRoPromoteFailed = 0, sTrackFull = 0, sReleaseRequests = 0;

void Lock() { while (InterlockedCompareExchange(&gTrackLock, 1, 0) != 0) YieldProcessor(); }
void Unlock() { InterlockedExchange(&gTrackLock, 0); }

int SlotOf(HANDLE h) { return static_cast<int>((reinterpret_cast<uintptr_t>(h) >> 2) % TrackMax); }

void OpLock(Tracked& t) { while (InterlockedCompareExchange(&t.opLock, 1, 0) != 0) YieldProcessor(); }
void OpUnlock(Tracked& t) { InterlockedExchange(&t.opLock, 0); }

// Frees what the slot owns and zeroes it, except its operation lock (held by
// the caller).
void ReleaseSlot(Tracked& t) {
    if (t.path) free(t.path);
    if (t.buffer) free(t.buffer);
    if (t.writeBuffer) free(t.writeBuffer);
    if (t.writeBehind) InterlockedDecrement(&gWriteBehindActive);
    memset(reinterpret_cast<uint8_t*>(&t) + offsetof(Tracked, drained), 0, sizeof(Tracked) - offsetof(Tracked, drained));
}

struct Opened {
    bool readAhead; uint64_t size; bool writeBehind;
    bool readOnly; DWORD access, share, flags;
};

// A slot whose handle was closed outside the hooks is reused when the
// system hands out the same value again.
bool TrackAdd(HANDLE h, const wchar_t* path, const Opened& opened) {
    const size_t n = wcslen(path);
    auto* copy = static_cast<wchar_t*>(malloc((n + 1) * sizeof(wchar_t)));
    if (!copy) return false;
    memcpy(copy, path, (n + 1) * sizeof(wchar_t));
    bool placed = false;
    Lock();
    const int slot = SlotOf(h);
    for (int i = 0; i < TrackMax; ++i) {
        Tracked& t = gTrack[(slot + i) % TrackMax];
        if (t.h == nullptr || t.h == Tomb || t.h == h) {
            OpLock(t);
            ReleaseSlot(t);
            t.h = h; t.path = copy; t.readAhead = opened.readAhead; t.size = opened.size;
            t.readOnly = opened.readOnly; t.access = opened.access; t.share = opened.share; t.flags = opened.flags;
            if (opened.writeBehind) {
                t.writeBuffer = static_cast<uint8_t*>(malloc(WriteBehindBlock));
                if (t.writeBuffer) { t.writeBehind = true; InterlockedIncrement(&gWriteBehindActive); }
            }
            OpUnlock(t);
            placed = true;
            break;
        }
    }
    Unlock();
    if (!placed) { free(copy); InterlockedIncrement64(&sTrackFull); }
    return placed;
}

// The slot of a tracked handle (stable address) or nullptr.
Tracked* TrackFind(HANDLE h) {
    Lock();
    const int slot = SlotOf(h);
    Tracked* found = nullptr;
    for (int i = 0; i < TrackMax; ++i) {
        Tracked& t = gTrack[(slot + i) % TrackMax];
        if (t.h == nullptr) break;
        if (t.h == h) { found = &t; break; }
    }
    Unlock();
    return found;
}

// The tracked slot of h with its operation lock held, or nullptr (not
// tracked, or handed back by Drain()). Only the thread using a handle
// operates on it, as in the unhooked program; the lock is uncontended except
// against Drain().
Tracked* Enter(HANDLE h) {
    Tracked* t = TrackFind(h);
    if (!t) return nullptr;
    OpLock(*t);
    if (t->h != h || t->drained) { OpUnlock(*t); return nullptr; }
    return t;
}

void Leave(Tracked* t) { if (t) OpUnlock(*t); }

// Removes the handle; returns its path (the caller frees it) and the
// reopened handle, if any.
bool TrackRemove(HANDLE h, wchar_t** path, HANDLE* promoted) {
    Lock();
    const int slot = SlotOf(h);
    bool found = false;
    for (int i = 0; i < TrackMax; ++i) {
        Tracked& t = gTrack[(slot + i) % TrackMax];
        if (t.h == nullptr) break;
        if (t.h == h) {
            OpLock(t);
            *path = t.path; *promoted = t.promoted;
            t.path = nullptr; t.promoted = nullptr;
            if (t.buffer) { free(t.buffer); t.buffer = nullptr; }
            if (t.writeBuffer) { free(t.writeBuffer); t.writeBuffer = nullptr; }
            if (t.writeBehind) { InterlockedDecrement(&gWriteBehindActive); t.writeBehind = false; }
            t.h = Tomb;
            OpUnlock(t);
            found = true;
            break;
        }
    }
    Unlock();
    return found;
}

// ---- read-ahead
bool SyncPointer(HANDLE h, Tracked* t) {
    if (t->real == t->logical) return true;
    LARGE_INTEGER position; position.QuadPart = static_cast<LONGLONG>(t->logical);
    if (!SetFilePointerEx(h, position, nullptr, FILE_BEGIN)) return false;
    t->real = t->logical;
    return true;
}

BOOL PlainRead(HANDLE h, Tracked* t, LPVOID destination, DWORD count, LPDWORD got) {
    if (!SyncPointer(h, t)) { *got = 0; return FALSE; }
    DWORD read = 0;
    const BOOL ok = chain.readFile(h, destination, count, &read, nullptr);
    if (ok) { t->logical += read; t->real = t->logical; }
    else t->real = ~0ull;            // unknown pointer: re-sync next time
    *got = read;
    InterlockedIncrement64(&sRealReads);
    return ok;
}

// Exactly what an unhooked ReadFile would return.
BOOL ReadAheadRead(HANDLE h, Tracked* t, LPVOID destinationIn, DWORD count, LPDWORD got) {
    auto* destination = static_cast<uint8_t*>(destinationIn);
    if (count == 0) { *got = 0; return TRUE; }
    if (count > ReadAheadSmallMax) { t->sequentialTiny = 0; return PlainRead(h, t, destination, count, got); }
    if (t->sequentialTiny < 0xFFFFFFFFu) ++t->sequentialTiny;
    if (t->sequentialTiny < ReadAheadSequentialStart) return PlainRead(h, t, destination, count, got);
    DWORD total = 0;
    while (total < count) {
        if (t->buffer && t->logical >= t->bufferPosition && t->logical < t->bufferPosition + t->bufferLength) {
            const uint64_t available = t->bufferPosition + t->bufferLength - t->logical;
            const DWORD chunk = static_cast<DWORD>(available < static_cast<uint64_t>(count - total) ? available : count - total);
            memcpy(destination + total, t->buffer + (t->logical - t->bufferPosition), chunk);
            total += chunk; t->logical += chunk;
            continue;
        }
        if (!t->buffer) {
            t->buffer = static_cast<uint8_t*>(malloc(ReadAheadBlock));
            if (!t->buffer) { t->readAhead = false; DWORD rest = 0; const BOOL ok = PlainRead(h, t, destination + total, count - total, &rest); *got = total + rest; return ok || total > 0; }
        }
        if (!SyncPointer(h, t)) { if (total) break; *got = 0; return FALSE; }
        DWORD read = 0;
        const BOOL ok = chain.readFile(h, t->buffer, ReadAheadBlock, &read, nullptr);
        InterlockedIncrement64(&sRealReads);
        if (!ok) { t->real = ~0ull; t->bufferLength = 0; if (total) break; *got = 0; return FALSE; }
        if (read == 0) { t->bufferLength = 0; t->real = t->logical; break; }   // end of file
        t->bufferPosition = t->logical; t->bufferLength = read; t->real = t->logical + read;
    }
    if (total) InterlockedIncrement64(&sServed);
    *got = total;
    return TRUE;
}

// A write or truncate ends the shim for the handle: the kernel gets the
// application's true position first.
void StopShim(HANDLE h, Tracked* t) {
    SyncPointer(h, t);
    t->readAhead = false; t->bufferLength = 0; t->sequentialTiny = 0;
    InterlockedIncrement64(&sShimStopped);
}

// ---- write-behind
void WriteLock(Tracked* t) { while (InterlockedCompareExchange(&t->writeLock, 1, 0) != 0) YieldProcessor(); }
void WriteUnlock(Tracked* t) { InterlockedExchange(&t->writeLock, 0); }

// The kernel pointer is always the application's position minus the
// buffered length, so nothing else needs adjusting. A failed write is retried
// once; a second failure turns write-behind off for new handles.
void FlushLocked(HANDLE h, Tracked* t) {
    if (t->writeLength == 0) return;
    DWORD done = 0, written = 0;
    BOOL ok = chain.writeFile(h, t->writeBuffer, t->writeLength, &written, nullptr);
    InterlockedIncrement64(&sWbRealWrites);
    done = ok ? written : 0;
    if (done < t->writeLength) {
        written = 0;
        ok = chain.writeFile(h, t->writeBuffer + done, t->writeLength - done, &written, nullptr);
        InterlockedIncrement64(&sWbRealWrites);
        if (!ok || done + written != t->writeLength) { InterlockedIncrement64(&sWbFailed); gWriteBehind = false; }
    }
    t->writeLength = 0;
}

void Flush(HANDLE h, Tracked* t) {
    if (!t || !t->writeBehind) return;
    const DWORD error = GetLastError();
    WriteLock(t); FlushLocked(h, t); WriteUnlock(t);
    SetLastError(error);
}

// A new open of a path with buffered data must find that data on disk. The
// buffer lock keeps the owning thread's writes in order with this flush.
void FlushPath(LPCWSTR name) {
    Lock();
    for (int i = 0; i < TrackMax; ++i) {
        Tracked& t = gTrack[i];
        if (t.writeBehind && t.writeLength && t.h && t.h != Tomb && t.path && _wcsicmp(t.path, name) == 0) Flush(t.h, &t);
    }
    Unlock();
}

bool EndsWithI(LPCWSTR name, const wchar_t* suffix) {
    const size_t n = wcslen(name), m = wcslen(suffix);
    return n > m && _wcsicmp(name + n - m, suffix) == 0;
}

bool EligibleForWriteBehind(HANDLE h, LPCWSTR name, DWORD access, DWORD flags) {
    if (!gWriteBehind || !EndsWithI(name, L".rdy")) return false;
    if (!(access & (GENERIC_WRITE | FILE_WRITE_DATA))) return false;
    if (access & (GENERIC_ALL | FILE_APPEND_DATA | DELETE)) return false;
    if (flags & (FILE_FLAG_OVERLAPPED | FILE_FLAG_NO_BUFFERING | FILE_FLAG_DELETE_ON_CLOSE | FILE_FLAG_WRITE_THROUGH)) return false;
    return GetFileType(h) == FILE_TYPE_DISK;
}

// The Delphi runtime opens files read/write by default, so most game assets
// arrive with write access although they are only read. Such handles are
// eligible; the first write or truncate ends the shim for that handle.
bool EligibleForReadAhead(HANDLE h, DWORD access, DWORD disposition, DWORD flags) {
    if (!gReadAhead) return false;
    if (disposition != OPEN_EXISTING) return false;
    if (access & (GENERIC_ALL | FILE_APPEND_DATA | DELETE)) return false;
    if (!(access & (GENERIC_READ | FILE_READ_DATA))) return false;
    if (flags & (FILE_FLAG_OVERLAPPED | FILE_FLAG_NO_BUFFERING | FILE_FLAG_DELETE_ON_CLOSE | FILE_FLAG_WRITE_THROUGH | FILE_FLAG_RANDOM_ACCESS)) return false;
    return GetFileType(h) == FILE_TYPE_DISK;
}

// ---- read-only open
// Asset extensions the game only reads; .cfg/.txt/.bmp count only inside the
// configured folders.
const wchar_t* const ReadOnlyExtensions[] = { L".o3d", L".sco", L".bus", L".ovh", L".hof", L".osc", L".cti", L".org", L".cfg", L".txt",
                                              L".wav", L".ogg", L".bmp", L".png", L".tga", L".jpg", L".dds", L".x" };

bool HasReadOnlyExtension(const wchar_t* name) {
    const wchar_t* dot = nullptr;
    for (size_t i = wcslen(name); i > 0; --i) {
        const wchar_t c = name[i - 1];
        if (c == L'.') { dot = name + i - 1; break; }
        if (c == L'\\' || c == L'/') break;
    }
    if (!dot) return false;
    for (const wchar_t* extension : ReadOnlyExtensions) if (_wcsicmp(dot, extension) == 0) return true;
    return false;
}

void ParsePrefixes(const wchar_t* list) {
    gReadOnlyPrefixes.clear();
    std::wstring all = list ? list : L"";
    size_t start = 0;
    while (start <= all.size()) {
        size_t end = all.find(L';', start);
        if (end == std::wstring::npos) end = all.size();
        std::wstring one = all.substr(start, end - start);
        while (!one.empty() && (one.front() == L' ' || one.front() == L'\t')) one.erase(0, 1);
        while (!one.empty() && (one.back() == L' ' || one.back() == L'\t')) one.pop_back();
        for (auto& c : one) c = c == L'/' ? L'\\' : static_cast<wchar_t>(towlower(c));
        if (!one.empty()) { if (one.back() != L'\\') one += L'\\'; gReadOnlyPrefixes.push_back(one); }
        start = end + 1;
    }
}

// Under a configured folder ("vehicles\" matches "vehicles\Bus\x.o3d" and
// "D:\OMSI\Vehicles\Bus\x.o3d") with a read-only asset extension.
bool ReadOnlyEligible(LPCWSTR name) {
    if (!gReadOnlyOpen || !name || !*name || !HasReadOnlyExtension(name)) return false;
    wchar_t lower[MAX_PATH];
    size_t n = 0;
    for (; name[n] && n < MAX_PATH - 2; ++n) { const wchar_t c = name[n]; lower[n] = c == L'/' ? L'\\' : static_cast<wchar_t>(towlower(c)); }
    lower[n] = 0;
    for (const std::wstring& prefix : gReadOnlyPrefixes) {
        if (wcsncmp(lower, prefix.c_str(), prefix.size()) == 0) return true;
        const std::wstring inner = L"\\" + prefix;
        if (wcsstr(lower, inner.c_str())) return true;
    }
    return false;
}

// The game writes to a handle opened read-only: reopen the file with the
// access it asked for, at the position it expects, and serve the handle from
// that one from now on. Null when the file cannot be reopened.
HANDLE Promote(HANDLE h, Tracked* t) {
    if (t->promoted) return t->promoted;
    const DWORD reopenFlags = t->flags & (FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_NO_RECALL | FILE_FLAG_OPEN_REPARSE_POINT | FILE_FLAG_POSIX_SEMANTICS | FILE_FLAG_RANDOM_ACCESS | FILE_FLAG_SEQUENTIAL_SCAN);
    const HANDLE reopened = ReOpenFile(h, t->access, t->share | FILE_SHARE_READ | FILE_SHARE_WRITE, reopenFlags);
    if (reopened == INVALID_HANDLE_VALUE) { InterlockedIncrement64(&sRoPromoteFailed); return nullptr; }
    LARGE_INTEGER position{}, zero{};
    if (t->readAhead) position.QuadPart = static_cast<LONGLONG>(t->logical);
    else SetFilePointerEx(h, zero, &position, FILE_CURRENT);
    SetFilePointerEx(reopened, position, nullptr, FILE_BEGIN);
    t->readAhead = false; t->bufferLength = 0; t->sequentialTiny = 0;
    t->promoted = reopened;
    InterlockedIncrement64(&sRoPromoted);
    return reopened;
}

void ReportUse(LPCWSTR path, int kind) {
    if (!gOnUse || !path || tReentry != 0) return;
    ++tReentry;
    gOnUse(path, kind);
    --tReentry;
}

// The first write (or truncate) through a handle reports its file once:
// the session program never holds a file the game writes.
void ReportWrite(Tracked* t) {
    if (!t || t->reportedWrite || !t->path) return;
    t->reportedWrite = true;
    const int depth = tReentry;
    tReentry = 0;
    ReportUse(t->path, 1);
    tReentry = depth;
}

void ReportUseAnsi(LPCSTR path) {
    if (!gOnUse || !path || tReentry != 0) return;
    wchar_t wide[MAX_PATH * 2] = {};
    // OMSI passes these paths in the system code page.
    if (MultiByteToWideChar(CP_ACP, 0, path, -1, wide, MAX_PATH * 2 - 1) > 0) ReportUse(wide, 0);
}

// A file OMSI cannot replace or truncate while the session program maps it:
// ask for the release, then let the caller retry for a while.
bool AskRelease(LPCWSTR path) {
    if (!gOnRelease || !path || !*path) return false;
    const int depth = tReentry;
    tReentry = 1;
    const bool asked = gOnRelease(path);
    tReentry = depth;
    return asked;
}

} // namespace

void InitChain() {
    chain.createFileW = ::CreateFileW;
    chain.readFile = ::ReadFile;
    chain.writeFile = ::WriteFile;
    chain.closeHandle = ::CloseHandle;
    chain.setFilePointer = ::SetFilePointer;
    chain.getFileSize = ::GetFileSize;
    chain.setEndOfFile = ::SetEndOfFile;
    if (HMODULE d3dx = GetModuleHandleW(L"d3dx9.dll")) {
        chain.texExW = reinterpret_cast<TexExWFn>(GetProcAddress(d3dx, "D3DXCreateTextureFromFileExW"));
        chain.texExA = reinterpret_cast<TexExAFn>(GetProcAddress(d3dx, "D3DXCreateTextureFromFileExA"));
        chain.texW = reinterpret_cast<TexWFn>(GetProcAddress(d3dx, "D3DXCreateTextureFromFileW"));
        chain.meshXA = reinterpret_cast<MeshXAFn>(GetProcAddress(d3dx, "D3DXLoadMeshFromXA"));
    }
}

void Configure(const Options& options, UseCallback onUse, ReleaseCallback onRelease) {
    gOptions = options;
    // Nothing is shimmed until Confirm(): a handle opened while only some of
    // the hooks are in place is tracked but served natively.
    gReadAhead = gWriteBehind = gReadOnlyOpen = false;
    ParsePrefixes(options.readOnlyPrefixes && *options.readOnlyPrefixes ? options.readOnlyPrefixes : L"vehicles\\");
    gOnUse = onUse;
    gOnRelease = onRelease;
    InterlockedExchange(&gPassThrough, 0);
}

void Confirm(bool allHooks) {
    // Positions and reopened handles cannot stay coherent without every hook.
    gReadAhead = allHooks && gOptions.readAhead;
    gWriteBehind = allHooks && gOptions.writeBehind;
    gReadOnlyOpen = allHooks && gOptions.readOnlyOpen;
    MemoryBarrier();
}

void Drain() {
    // New handles get no feature from here on; every open one is handed back
    // between two of its operations.
    gReadAhead = gWriteBehind = gReadOnlyOpen = false;
    MemoryBarrier();
    for (int i = 0; i < TrackMax; ++i) {
        Tracked& t = gTrack[i];
        Lock();
        if (t.h && t.h != Tomb && !t.drained) {
            OpLock(t);
            if (t.writeBehind) { WriteLock(&t); FlushLocked(t.h, &t); WriteUnlock(&t); }
            if (t.readAhead) { SyncPointer(t.h, &t); t.readAhead = false; t.bufferLength = 0; }
            if (t.promoted) {
                // From now on the game's own (read-only) handle is used again:
                // give it the position. A write to it would now be refused;
                // that needs the game to write to an asset that is still open
                // when the module stops.
                LARGE_INTEGER position{}, zero{};
                if (SetFilePointerEx(t.promoted, zero, &position, FILE_CURRENT)) SetFilePointerEx(t.h, position, nullptr, FILE_BEGIN);
            }
            t.drained = true;
            OpUnlock(t);
        }
        Unlock();
    }
    InterlockedExchange(&gPassThrough, 1);
}

Stats GetStats() {
    return Stats{ sAppReads, sRealReads, sServed, sShimStopped, sWbAppWrites, sWbRealWrites, sWbFailed, sRoDowngraded, sRoPromoted, sRoPromoteFailed, sTrackFull, sReleaseRequests };
}

HANDLE WINAPI HookCreateFileW(LPCWSTR name, DWORD access, DWORD share, LPSECURITY_ATTRIBUTES sa, DWORD disposition, DWORD flags, HANDLE templateFile) {
    if (gPassThrough) return chain.createFileW(name, access, share, sa, disposition, flags, templateFile);
    if (gWriteBehindActive > 0 && name && tReentry == 0) { ++tReentry; FlushPath(name); --tReentry; }
    const DWORD requestedAccess = access, requestedShare = share;
    bool downgraded = false;
    if (gReadOnlyOpen && disposition == OPEN_EXISTING && (access & (GENERIC_WRITE | FILE_WRITE_DATA)) && (access & (GENERIC_READ | FILE_READ_DATA)) &&
        !(access & (GENERIC_ALL | FILE_APPEND_DATA | DELETE)) && !(flags & (FILE_FLAG_DELETE_ON_CLOSE | FILE_FLAG_WRITE_THROUGH)) && ReadOnlyEligible(name)) {
        access &= ~static_cast<DWORD>(GENERIC_WRITE | FILE_WRITE_DATA | FILE_WRITE_EA | FILE_WRITE_ATTRIBUTES);
        // Sharing writes lets the file be reopened for writing should the game write after all.
        share |= FILE_SHARE_READ | FILE_SHARE_WRITE;
        downgraded = true;
    }
    HANDLE h = chain.createFileW(name, access, share, sa, disposition, flags, templateFile);
    if (h == INVALID_HANDLE_VALUE && GetLastError() == ERROR_USER_MAPPED_FILE && tReentry == 0 && AskRelease(name)) {
        for (int i = 0; i < ReleaseRetries && h == INVALID_HANDLE_VALUE && GetLastError() == ERROR_USER_MAPPED_FILE; ++i) {
            Sleep(ReleaseRetryMs);
            h = chain.createFileW(name, access, share, sa, disposition, flags, templateFile);
        }
        InterlockedIncrement64(&sReleaseRequests);
    }
    if (downgraded && h == INVALID_HANDLE_VALUE) {
        // The wider sharing was refused (another open denies it): open as asked.
        downgraded = false;
        h = chain.createFileW(name, requestedAccess, requestedShare, sa, disposition, flags, templateFile);
    }
    DWORD error = GetLastError();
    if (tReentry == 0 && name && h != INVALID_HANDLE_VALUE) {
        ++tReentry;
        bool readAhead = EligibleForReadAhead(h, access, disposition, flags);
        uint64_t size = 0;
        if (readAhead) {
            LARGE_INTEGER length;
            if (GetFileSizeEx(h, &length)) size = static_cast<uint64_t>(length.QuadPart); else readAhead = false;
        }
        const bool writeBehind = !readAhead && EligibleForWriteBehind(h, name, access, flags);
        const Opened opened{ readAhead, size, writeBehind, downgraded, requestedAccess, requestedShare, flags };
        if (TrackAdd(h, name, opened)) {
            if (downgraded) InterlockedIncrement64(&sRoDowngraded);
        } else if (downgraded) {
            // Untracked, a later write could not be served: open it as asked.
            chain.closeHandle(h);
            h = chain.createFileW(name, requestedAccess, requestedShare, sa, disposition, flags, templateFile);
            error = GetLastError();
        }
        --tReentry;
        if (h != INVALID_HANDLE_VALUE) ReportUse(name, 0);
    }
    SetLastError(error);
    return h;
}

BOOL WINAPI HookReadFile(HANDLE h, LPVOID buffer, DWORD count, LPDWORD read, LPOVERLAPPED overlapped) {
    if (gPassThrough || tReentry != 0) return chain.readFile(h, buffer, count, read, overlapped);
    ++tReentry;
    Tracked* t = Enter(h);
    BOOL ok; DWORD error;
    if (t && t->promoted) {
        ok = chain.readFile(t->promoted, buffer, count, read, overlapped);
        error = GetLastError();
    } else {
        if (t && t->writeBehind) Flush(h, t);
        if (t && t->readAhead && !overlapped && read) {
            DWORD got = 0;
            ok = ReadAheadRead(h, t, buffer, count, &got);
            *read = got;
            error = ok ? ERROR_SUCCESS : GetLastError();
        } else {
            // Unsupported call shape: hand the kernel the true position and stop shimming.
            if (t && t->readAhead) { SyncPointer(h, t); t->readAhead = false; t->sequentialTiny = 0; }
            ok = chain.readFile(h, buffer, count, read, overlapped);
            error = GetLastError();
            InterlockedIncrement64(&sRealReads);
        }
    }
    Leave(t);
    InterlockedIncrement64(&sAppReads);
    --tReentry;
    SetLastError(error);
    return ok;
}

// While read-ahead is active the kernel pointer is not the application's
// position: seeks and position queries are answered from `logical`. The
// Delphi runtime calls SetFilePointer(current, 0) and GetFileSize on every
// Eof(), i.e. once per byte.
DWORD SetFilePointerLocked(HANDLE h, Tracked* t, LONG distance, PLONG distanceHigh, DWORD method) {
    if (t && t->promoted) return chain.setFilePointer(t->promoted, distance, distanceHigh, method);
    if (t && t->writeBehind) Flush(h, t);
    if (!t || !t->readAhead) return chain.setFilePointer(h, distance, distanceHigh, method);
    const int64_t move = distanceHigh ? ((static_cast<int64_t>(*distanceHigh) << 32) | static_cast<uint32_t>(distance)) : static_cast<int64_t>(distance);
    int64_t base;
    switch (method) {
    case FILE_BEGIN: base = 0; break;
    case FILE_CURRENT: base = static_cast<int64_t>(t->logical); break;
    case FILE_END: base = static_cast<int64_t>(t->size); break;
    default: return chain.setFilePointer(h, distance, distanceHigh, method);   // the system rejects it
    }
    const int64_t next = base + move;
    if (next < 0) { SetLastError(ERROR_NEGATIVE_SEEK); return INVALID_SET_FILE_POINTER; }
    if (!distanceHigh && next > 0x7FFFFFFF) {
        // Does not fit the 32-bit-only form: let the system answer exactly.
        if (!SyncPointer(h, t)) return chain.setFilePointer(h, distance, distanceHigh, method);
        const DWORD result = chain.setFilePointer(h, distance, distanceHigh, method);
        const DWORD error = GetLastError();
        if (result != INVALID_SET_FILE_POINTER || error == NO_ERROR) t->logical = t->real = result; else t->real = ~0ull;
        t->sequentialTiny = 0;
        SetLastError(error);
        return result;
    }
    if (static_cast<uint64_t>(next) != t->logical) t->sequentialTiny = 0;   // a position query keeps a sequential scan going
    t->logical = static_cast<uint64_t>(next);
    if (distanceHigh) *distanceHigh = static_cast<LONG>(next >> 32);
    SetLastError(NO_ERROR);
    return static_cast<DWORD>(next & 0xFFFFFFFFll);
}

DWORD WINAPI HookSetFilePointer(HANDLE h, LONG distance, PLONG distanceHigh, DWORD method) {
    if (gPassThrough) return chain.setFilePointer(h, distance, distanceHigh, method);
    Tracked* t = Enter(h);
    const DWORD result = SetFilePointerLocked(h, t, distance, distanceHigh, method);
    const DWORD error = GetLastError();
    Leave(t);
    SetLastError(error);
    return result;
}

BOOL WINAPI HookWriteFile(HANDLE h, LPCVOID buffer, DWORD count, LPDWORD written, LPOVERLAPPED overlapped) {
    if (gPassThrough || tReentry != 0) return chain.writeFile(h, buffer, count, written, overlapped);
    ++tReentry;
    Tracked* t = Enter(h);
    BOOL ok; DWORD error;
    if (t && t->readOnly && !t->promoted) Promote(h, t);
    if (t && t->promoted) {
        ok = chain.writeFile(t->promoted, buffer, count, written, overlapped);
        error = GetLastError();
    } else if (t && t->writeBehind && !overlapped && written && count < WriteBehindBlock && gWriteBehind) {
        WriteLock(t);
        if (t->writeLength + count > WriteBehindBlock) FlushLocked(h, t);
        memcpy(t->writeBuffer + t->writeLength, buffer, count);
        t->writeLength += count;
        WriteUnlock(t);
        *written = count;
        InterlockedIncrement64(&sWbAppWrites);
        ok = TRUE; error = ERROR_SUCCESS;
    } else {
        if (t && t->readAhead) StopShim(h, t);
        if (t && t->writeBehind) Flush(h, t);   // large or unusual write: keep the order, then go native
        ok = chain.writeFile(h, buffer, count, written, overlapped);
        error = GetLastError();
    }
    ReportWrite(t);
    Leave(t);
    --tReentry;
    SetLastError(error);
    return ok;
}

BOOL WINAPI HookSetEndOfFile(HANDLE h) {
    if (gPassThrough) return chain.setEndOfFile(h);
    Tracked* t = Enter(h);
    BOOL ok;
    if (t && t->readOnly && !t->promoted) Promote(h, t);
    const HANDLE target = t && t->promoted ? t->promoted : h;
    if (!(t && t->promoted)) {
        if (t && t->writeBehind) Flush(h, t);
        if (t && t->readAhead) StopShim(h, t);
    }
    ok = chain.setEndOfFile(target);
    if (!ok && GetLastError() == ERROR_USER_MAPPED_FILE && t && t->path && AskRelease(t->path)) {
        for (int i = 0; i < ReleaseRetries && !ok && GetLastError() == ERROR_USER_MAPPED_FILE; ++i) {
            Sleep(ReleaseRetryMs);
            ok = chain.setEndOfFile(target);
        }
        InterlockedIncrement64(&sReleaseRequests);
    }
    const DWORD error = GetLastError();
    ReportWrite(t);
    Leave(t);
    SetLastError(error);
    return ok;
}

DWORD WINAPI HookGetFileSize(HANDLE h, LPDWORD high) {
    if (gPassThrough) return chain.getFileSize(h, high);
    Tracked* t = Enter(h);
    DWORD result;
    if (t && t->promoted) result = chain.getFileSize(t->promoted, high);
    else if (t && t->readAhead) {
        if (high) *high = static_cast<DWORD>(t->size >> 32);
        SetLastError(NO_ERROR);
        result = static_cast<DWORD>(t->size);
    } else {
        if (t && t->writeBehind) Flush(h, t);
        result = chain.getFileSize(h, high);
    }
    const DWORD error = GetLastError();
    Leave(t);
    SetLastError(error);
    return result;
}

BOOL WINAPI HookCloseHandle(HANDLE h) {
    if (gPassThrough) return chain.closeHandle(h);
    wchar_t* path = nullptr; HANDLE promoted = nullptr;
    if (tReentry == 0) {
        if (Tracked* t = Enter(h)) { if (t->writeBehind) Flush(h, t); Leave(t); }
        TrackRemove(h, &path, &promoted);
    }
    if (promoted) chain.closeHandle(promoted);
    const BOOL ok = chain.closeHandle(h);
    const DWORD error = GetLastError();
    free(path);
    SetLastError(error);
    return ok;
}

// D3DX opens texture and mesh files itself, outside Omsi.exe's imports: the
// loads are reported here.
HRESULT WINAPI HookTexExW(void* device, LPCWSTR file, UINT width, UINT height, UINT mips, DWORD usage, DWORD format, DWORD pool, DWORD filter, DWORD mipFilter, DWORD colorKey, void* info, void* palette, void** texture) {
    const HRESULT result = chain.texExW(device, file, width, height, mips, usage, format, pool, filter, mipFilter, colorKey, info, palette, texture);
    if (!gPassThrough) ReportUse(file, 0);
    return result;
}

HRESULT WINAPI HookTexExA(void* device, LPCSTR file, UINT width, UINT height, UINT mips, DWORD usage, DWORD format, DWORD pool, DWORD filter, DWORD mipFilter, DWORD colorKey, void* info, void* palette, void** texture) {
    const HRESULT result = chain.texExA(device, file, width, height, mips, usage, format, pool, filter, mipFilter, colorKey, info, palette, texture);
    if (!gPassThrough) ReportUseAnsi(file);
    return result;
}

HRESULT WINAPI HookTexW(void* device, LPCWSTR file, void** texture) {
    const HRESULT result = chain.texW(device, file, texture);
    if (!gPassThrough) ReportUse(file, 0);
    return result;
}

HRESULT WINAPI HookMeshXA(LPCSTR file, DWORD options, void* device, void** adjacency, void** materials, void** effects, DWORD* materialCount, void** mesh) {
    const HRESULT result = chain.meshXA(file, options, device, adjacency, materials, effects, materialCount, mesh);
    if (!gPassThrough) ReportUseAnsi(file);
    return result;
}

} // namespace fileshim
