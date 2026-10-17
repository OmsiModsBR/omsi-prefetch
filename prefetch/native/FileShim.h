#pragma once
// File I/O shim of the prefetch module (inside OMSI): the detours for the
// imports of Omsi.exe that the bridge installs for this module.
//
// - Read-ahead: a handle that reads a disk file in tiny pieces (OMSI's Delphi
//   runtime reads byte by byte) is served from 64 KiB blocks; positions and
//   sizes are answered from the shim's own state, so the kernel is asked once
//   per block instead of once per byte.
// - Write-behind: the terrain cache (tile_X_Y.map.terrain_*.rdy), which OMSI
//   writes byte by byte, is collected in 256 KiB blocks.
// - Read-only open: asset files under the configured folders (vehicles\) are
//   opened without write access, although the Delphi runtime asks for it
//   (closing a handle with write access costs a synchronous metadata update).
//   Should the game write to such a handle after all, the file is reopened
//   with the access it asked for and the handle is served from that one.
// - Use reports: the first open of each file, every open of a tile_X_Y.map
//   and every file written, for the module's session program.
#include <windows.h>
#include <stdint.h>

namespace fileshim {

struct Options {
    bool readAhead = true;
    bool writeBehind = true;
    bool readOnlyOpen = true;
    // Folders relative to the OMSI root, ';' separated.
    const wchar_t* readOnlyPrefixes = L"vehicles\\";
};

// kind 0: a file was opened or loaded (first use), 1: a file was written to.
using UseCallback = void (*)(const wchar_t* path, int kind);
// The game cannot replace or truncate a file because a section maps it (the
// session program holds it in memory): asks for its release. True when the
// request reached someone who can release it (then the call is retried).
using ReleaseCallback = bool (*)(const wchar_t* path);

using CreateFileWFn = HANDLE (WINAPI*)(LPCWSTR, DWORD, DWORD, LPSECURITY_ATTRIBUTES, DWORD, DWORD, HANDLE);
using ReadFileFn = BOOL (WINAPI*)(HANDLE, LPVOID, DWORD, LPDWORD, LPOVERLAPPED);
using WriteFileFn = BOOL (WINAPI*)(HANDLE, LPCVOID, DWORD, LPDWORD, LPOVERLAPPED);
using CloseHandleFn = BOOL (WINAPI*)(HANDLE);
using SetFilePointerFn = DWORD (WINAPI*)(HANDLE, LONG, PLONG, DWORD);
using GetFileSizeFn = DWORD (WINAPI*)(HANDLE, LPDWORD);
using SetEndOfFileFn = BOOL (WINAPI*)(HANDLE);
using TexExWFn = HRESULT (WINAPI*)(void*, LPCWSTR, UINT, UINT, UINT, DWORD, DWORD, DWORD, DWORD, DWORD, DWORD, void*, void*, void**);
using TexExAFn = HRESULT (WINAPI*)(void*, LPCSTR, UINT, UINT, UINT, DWORD, DWORD, DWORD, DWORD, DWORD, DWORD, void*, void*, void**);
using TexWFn = HRESULT (WINAPI*)(void*, LPCWSTR, void**);
using MeshXAFn = HRESULT (WINAPI*)(LPCSTR, DWORD, void*, void**, void**, void**, DWORD*, void**);

// The functions each detour continues to. They start as the real functions
// (so a detour that runs before its hook request returned is still correct)
// and the bridge replaces each with the previous entry of its chain.
struct Chain {
    CreateFileWFn createFileW;
    ReadFileFn readFile;
    WriteFileFn writeFile;
    CloseHandleFn closeHandle;
    SetFilePointerFn setFilePointer;
    GetFileSizeFn getFileSize;
    SetEndOfFileFn setEndOfFile;
    TexExWFn texExW;
    TexExAFn texExA;
    TexWFn texW;
    MeshXAFn meshXA;
};
extern Chain chain;

// Fills chain with the real functions. Call before any hook is requested.
void InitChain();
// Use reports start here; the features stay off until Confirm().
void Configure(const Options& options, UseCallback onUse, ReleaseCallback onRelease);
// Once every hook request returned: with all seven kernel32 hooks in place
// the configured features start, otherwise they stay off (positions and
// reopened handles cannot be kept coherent with a hook missing).
void Confirm(bool allHooks);

// Before the hooks are removed: writes every buffered byte, gives every
// handle the kernel position the game expects, and makes every detour a plain
// pass-through from then on.
void Drain();

struct Stats {
    long long appReads, realReads, servedFromBuffer, shimStopped;
    long long wbAppWrites, wbRealWrites, wbFailedFlushes;
    long long roDowngraded, roPromoted, roPromoteFailed, trackFull, releaseRequests;
};
Stats GetStats();

HANDLE WINAPI HookCreateFileW(LPCWSTR name, DWORD access, DWORD share, LPSECURITY_ATTRIBUTES sa, DWORD disposition, DWORD flags, HANDLE templateFile);
BOOL WINAPI HookReadFile(HANDLE h, LPVOID buffer, DWORD count, LPDWORD read, LPOVERLAPPED overlapped);
BOOL WINAPI HookWriteFile(HANDLE h, LPCVOID buffer, DWORD count, LPDWORD written, LPOVERLAPPED overlapped);
BOOL WINAPI HookCloseHandle(HANDLE h);
DWORD WINAPI HookSetFilePointer(HANDLE h, LONG distance, PLONG distanceHigh, DWORD method);
DWORD WINAPI HookGetFileSize(HANDLE h, LPDWORD high);
BOOL WINAPI HookSetEndOfFile(HANDLE h);
HRESULT WINAPI HookTexExW(void* device, LPCWSTR file, UINT width, UINT height, UINT mips, DWORD usage, DWORD format, DWORD pool, DWORD filter, DWORD mipFilter, DWORD colorKey, void* info, void* palette, void** texture);
HRESULT WINAPI HookTexExA(void* device, LPCSTR file, UINT width, UINT height, UINT mips, DWORD usage, DWORD format, DWORD pool, DWORD filter, DWORD mipFilter, DWORD colorKey, void* info, void* palette, void** texture);
HRESULT WINAPI HookTexW(void* device, LPCWSTR file, void** texture);
HRESULT WINAPI HookMeshXA(LPCSTR file, DWORD options, void* device, void** adjacency, void** materials, void** effects, DWORD* materialCount, void** mesh);

} // namespace fileshim
