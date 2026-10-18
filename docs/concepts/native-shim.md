# The native shim: read-ahead, write-behind and read-only open

`prefetch-native.dll` is the module's component inside OMSI. It asks the OmsiLaunch bridge for hooks on imports of `Omsi.exe` and uses them for three features (read-ahead, write-behind, read-only open) and for the use reports that feed the [session program](session-program.md). This page describes what each feature does, when it applies, what it guarantees, how it ends, and what it counts. The features are separate switches ([settings](../reference/settings.md#the-native-features)); the use reports are always on.

Related pages: [how prefetching works](how-it-works.md), [safety](safety.md), [errors and degradation](../reference/errors-and-degradation.md), [events](../reference/events-and-notices.md), [known limitations](../reference/known-limitations.md).

## Why a shim

OMSI's runtime reads files byte by byte: for every byte it calls `ReadFile` and, to test for the end of the file, `SetFilePointer(current, 0)` and `GetFileSize`. On a model of a few megabytes that is millions of system calls. The shim answers these calls from memory it keeps for the handle, so the operating system is asked once per block instead of once per byte, and everything OMSI sees stays exactly what an unhooked call would have returned.

## Hooks the component requests

The component asks the bridge for hooks by symbolic target only (`iat:<dll>!<function>`), so it carries no OMSI address and works on any OMSI 2 build whose `Omsi.exe` imports these functions the usual way:

| Target | Used for |
| --- | --- |
| `kernel32.dll!CreateFileW`, `ReadFile`, `CloseHandle` | Required: the module does nothing useful without them. If any is refused, the component's start fails and the bridge removes whatever was installed. |
| `kernel32.dll!WriteFile` | Write reports, write-behind, serving a reopened handle. Always requested. |
| `kernel32.dll!SetFilePointer`, `GetFileSize`, `SetEndOfFile` | Requested when read-ahead, write-behind or read-only open is on. Keep positions and sizes coherent. |
| `d3dx9.dll!D3DXCreateTextureFromFileExW`, `...ExA`, `...FromFileW`, `D3DXLoadMeshFromXA` | Report textures and meshes that Direct3D loads by itself, outside the imports of `Omsi.exe`. Requested only for the functions found in an already-loaded `d3dx9.dll`. |

The three features start only when **all seven** kernel32 hooks are installed. With a position hook missing, nothing is shimmed and OMSI keeps exact behavior; the use reports still work. The result of every request is in the `hooks` event ([events](../reference/events-and-notices.md#the-hooks-event)).

## Read-ahead

A handle is eligible when OMSI opens an existing disk file with read access and without the flags that change the semantics of reads (overlapped, no buffering, delete on close, write through, random access). OMSI's Delphi runtime opens most assets with write access although they are only read; such handles are eligible too.

- Buffering starts after eight consecutive tiny reads (256 bytes or less) on the handle. Until then, and for any read larger than 256 bytes, the call goes to the system as usual.
- Then the shim reads 64 KiB blocks and answers the small reads from the block.
- `SetFilePointer` and `GetFileSize` are answered from the shim's state while it is active (the size is read once, at open).
- Exactness: reads, positions, sizes and end-of-file behavior are what the kernel would have returned. A read near the end returns the bytes left, then zero bytes.
- The first write or truncate through the handle, or an unusual call shape (overlapped, for instance), hands the kernel the true position and ends the shim for that handle. The handle keeps working natively.

Memory: 64 KiB per active handle, allocated when buffering starts and freed at close.

## Write-behind

OMSI writes its terrain cache, `tile_X_Y.map.terrain_*.rdy`, a few bytes at a time. A handle is eligible when its name ends in `.rdy`, it is opened with write access (not append, not delete, not read-ahead eligible), without overlapped, no-buffering, delete-on-close or write-through flags, and it is a disk file.

- Writes smaller than 256 KiB are collected in a 256 KiB buffer; a larger write flushes the buffer first and goes straight to the system.
- The buffer is flushed before a read, a seek, a size query, an end-of-file change or a close through the same handle, before any new open of the same path, and when the buffer is full. Order is preserved.
- A failed flush is retried once; a second failure turns write-behind off for new handles and is counted (`write_behind_failed`). The bytes of that block are lost and OMSI has already been told they were written. The `.rdy` file is a regenerable cache, which is why only it is buffered.
- When the module stops (a normal exit of OMSI), every buffer is flushed first. When OMSI is terminated, the bytes still buffered are lost, up to 256 KiB per open `.rdy` ([safety](safety.md#what-happens-when-something-goes-wrong)).

Memory: 256 KiB per active handle.

## Read-only open

Closing a handle that was opened with write access costs a synchronous metadata update, and OMSI's runtime opens assets with write access by default. For assets that OMSI only reads, the shim opens the file without write access.

A call is downgraded when **all** of these hold:

- the open is of an existing file (`OPEN_EXISTING`) with both read and write access, without generic-all, append or delete access and without delete-on-close or write-through;
- the path is below one of the `read_only_folders` (default `vehicles\`): the path starts with the folder, or contains it as a path segment (so `D:\OMSI 2\Vehicles\Bus\x.o3d` matches `vehicles\`);
- the extension is one of `.o3d .sco .bus .ovh .hof .osc .cti .org .cfg .txt .wav .ogg .bmp .png .tga .jpg .dds .x`.

The shim also widens the share mode to include read and write sharing, so that a file can be reopened. If the wider sharing is refused, or the handle cannot be tracked, the file is opened exactly as OMSI asked.

If OMSI writes to a handle that was opened read-only after all, the shim reopens the file with the access OMSI asked for, at the position OMSI expects, and serves that handle from then on (counted as `read_only_reopened`; a failed reopen is `read_only_reopen_failed`). When the module stops, the handle is given the position back and the game's own read-only handle is used again; a write through it after that would be refused. That needs a write to a vehicle asset that is open at the very moment the module stops.

## Use reports

For the learning of the session program, the component reports to the session pipe (batches every 60 ms, UTF-8, one line each):

| Line | Meaning | Sent |
| --- | --- | --- |
| `U<TAB><path>` | First use of a file: an open through `CreateFileW`, or a load through the hooked D3DX functions | Once per file (the first time) |
| `T<TAB><path>` | An open of a `tile_X_Y.map`, position information | Every time |
| `W<TAB><path>` | The game wrote to the file or truncated it | The first write through each handle |
| `X<TAB><path>` | OMSI cannot replace or truncate a file because the session program holds it | At once, ahead of the queue |

`X` works as follows. A held file is mapped, and Windows refuses to truncate or replace a mapped file (`ERROR_USER_MAPPED_FILE`). When OMSI's `CreateFileW` or `SetEndOfFile` is refused for that reason, the shim sends an `X` line and retries the call every 25 ms for up to 1.5 s (60 attempts). The session program releases the file on receiving it and never holds it again; OMSI's call then succeeds. If no session program is connected, the call fails as it would without the module. Releases are counted as `release_requests`.

The feed keeps at most 400,000 queued lines and remembers up to 500,000 distinct paths; beyond that, lines are dropped and counted (`feed_dropped`), and files first used after 500,000 distinct paths are no longer reported. If the pipe cannot be reached for ten seconds in a row (for instance, because the session program died), the feed gives up and queues nothing more; the three features keep working.

## Limits

| Limit | Value |
| --- | --- |
| Tracked handles | 4096 at the same time; beyond that a handle is served natively and counted as `track_full` |
| Read-ahead block | 64 KiB |
| Read-ahead threshold | eight consecutive reads of 256 bytes or less |
| Write-behind block | 256 KiB |
| Retries for a held file | 60 times 25 ms (1.5 s) |
| Feed batch | every 60 ms, in chunks of about 30 KB |

## Stop

When the module stops, the shim writes every buffered byte, hands every handle back with the position OMSI expects, and turns every hook into a plain pass-through before the bridge removes the hooks. The DLL stays loaded, inert, until OMSI exits. The statistics are emitted in the `stats` event.

## Next

[Learned data](learned-data.md) · [Memory and budget](memory-and-budget.md) · [Safety](safety.md)
