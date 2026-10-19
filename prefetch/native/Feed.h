#pragma once
// Use reports to the module's session program, over its session pipe.
// Producers (any game thread) only append to a queue under a short spin lock;
// one feed thread sends batches every 60 ms. Lines (UTF-8):
//   U<TAB><path>  first use of a file (open, or a D3DX load)
//   T<TAB><path>  every open of a tile_X_Y.map (position information)
//   W<TAB><path>  the game wrote to the file
//   X<TAB><path>  release the file now (sent ahead of the queue)
//   V<TAB><text>  the OMSI version the bridge reports (for the log only)
#include <windows.h>

namespace feed {

void Start(const wchar_t* channel);
void OnUse(const wchar_t* path, int kind);
// X<TAB><path>: OMSI cannot replace a file the session program maps. Sent at
// once; false when no session program is connected.
bool RequestRelease(const wchar_t* path);
// V<TAB><version>: informs the session program of the OMSI version (bridge
// protocol 3). Sent with the first batch; it never counts as a use report.
void AnnounceVersion(const wchar_t* version);
// Sends what is queued, closes the pipe and ends the feed thread.
void Stop();
long long Dropped();

} // namespace feed
