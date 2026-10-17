using System.Runtime.InteropServices;
using System.Text;

namespace OmsiLaunch.Modules.Prefetch.Tests;

// A small OMSI installation in a temporary folder: one map with three tiles,
// two entrypoints, ground textures, an AI list; scenery objects with models
// and textures; an AI bus and a user bus.
internal sealed class Fixture : IDisposable
{
    public Fixture()
    {
        Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "OmsiLaunchLoadingTests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Root);
        var global = new StringBuilder();
        global.Append("[friendlyname]\r\nTest Map\r\n\r\n[groundtex]\r\ntexture\\ground.bmp\r\ntexture\\ground_det.bmp\r\n0\r\n1\r\n60\r\n\r\n");
        global.Append("[entrypoints]\r\n2\r\n");
        // object, id, 0, position, rotation, tile index, label
        global.Append("4\r\n1\r\n0\r\n10.0\r\n0.0\r\n10.0\r\n0\r\n0\r\n0\r\n1\r\n0\r\nZeta Stop\r\n");
        global.Append("5\r\n2\r\n0\r\n20.0\r\n0.0\r\n20.0\r\n0\r\n0\r\n0\r\n1\r\n1\r\nAlpha Stop\r\n\r\n");
        foreach (var (x, y) in new[] { (0, 0), (1, 0), (5, 5) }) global.Append("[map]\r\n" + x + "\r\n" + y + "\r\ntile_" + x + "_" + y + ".map\r\n\r\n");
        Write("maps\\Test\\global.cfg", Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(global.ToString())).ToArray());
        WriteText("maps\\Test\\tile_0_0.map", "[object]\r\n0\r\nSceneryobjects\\Obj\\a.sco\r\n1\r\n\r\n[spline]\r\n0\r\nSplines\\road.sli\r\n2\r\n");
        Write("maps\\Test\\tile_0_0.map.terrain", Bytes(4000, 1));
        Write("maps\\Test\\tile_0_0.map.terrain_0.rdy", Bytes(4000, 2));
        Write("maps\\Test\\texture\\map\\tile_0_0.map.LM.bmp", Bytes(3000, 3));
        WriteText("maps\\Test\\tile_1_0.map", "[object]\r\n0\r\nSceneryobjects\\Obj\\b.sco\r\n1\r\n");
        WriteText("maps\\Test\\tile_5_5.map", "[object]\r\n0\r\nSceneryobjects\\Far\\far.sco\r\n1\r\n");
        Write("maps\\Test\\texture\\ground.bmp", Bytes(5000, 4));
        Write("maps\\Test\\texture\\ground_det.bmp", Bytes(5000, 5));
        WriteText("maps\\Test\\ailists.cfg", "[aigroup_2]\r\nBusse\r\n\r\nVehicles\\Bus\\bus.bus\t10\r\n[end]\r\n");
        Write("maps\\Test\\laststn.osn", Bytes(2000, 6));
        Write("maps\\Test\\TTData\\line.ttl", Bytes(1000, 7));
        Write("maps\\Test\\written.txt", Bytes(100, 8));
        WriteText("Sceneryobjects\\Obj\\a.sco", "[mesh]\r\na.o3d\r\n\r\n[matl]\r\ntex.bmp\r\n0\r\n");
        Write("Sceneryobjects\\Obj\\model\\a.o3d", O3d(6000, "tex.bmp"));
        Write("Sceneryobjects\\Obj\\texture\\tex.bmp", Bytes(7000, 9));
        WriteText("Sceneryobjects\\Obj\\texture\\tex.bmp.cfg", "[terrainmapping]\r\n");
        WriteText("Sceneryobjects\\Obj\\b.sco", "[mesh]\r\nb.o3d\r\n");
        Write("Sceneryobjects\\Obj\\model\\b.o3d", O3d(3000, null));
        WriteText("Sceneryobjects\\Far\\far.sco", "[mesh]\r\nfar.o3d\r\n");
        Write("Sceneryobjects\\Far\\model\\far.o3d", O3d(3000, null));
        WriteText("Splines\\road.sli", "[texture]\r\nroad.bmp\r\n");
        Write("Splines\\texture\\road.bmp", Bytes(4000, 10));
        WriteText("Vehicles\\Bus\\bus.bus", "[friendlyname]\r\nBus\r\n\r\n[model]\r\nmodel\\model.cfg\r\n\r\n[sound]\r\nsound\\sound.cfg\r\n");
        WriteText("Vehicles\\Bus\\model\\model.cfg", "[mesh]\r\nbody.o3d\r\n\r\n[matl]\r\nbody.bmp\r\n1\r\n\r\n[mesh]\r\nwheel.o3d\r\n");
        Write("Vehicles\\Bus\\model\\body.o3d", O3d(20000, "body.bmp"));
        Write("Vehicles\\Bus\\model\\wheel.o3d", O3d(2000, null));
        Write("Vehicles\\Bus\\texture\\body.bmp", Bytes(9000, 11));
        WriteText("Vehicles\\Bus\\sound\\sound.cfg", "[loop]\r\nengine.wav\r\n");
        Write("Vehicles\\Bus\\sound\\engine.wav", Bytes(8000, 12));
        WriteText("Vehicles\\Bus\\readme.txt", "notes");
        Write("Vehicles\\Bus\\source.psd", Bytes(1000, 13));
        WriteText("Vehicles\\User1\\u.bus", "[model]\r\nmodel\\m.cfg\r\n");
        WriteText("Vehicles\\User1\\model\\m.cfg", "[mesh]\r\nu.o3d\r\n");
        Write("Vehicles\\User1\\model\\u.o3d", O3d(4000, null));
        Write("Vehicles\\User1\\texture\\skin.bmp", Bytes(4000, 14));
        WriteText("options.cfg", "[options]\r\n");
    }

    public string Root { get; }

    public string Path(string relative) => System.IO.Path.Combine(Root, relative);

    public void Write(string relative, byte[] bytes)
    {
        var full = Path(relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, bytes);
    }

    public void WriteText(string relative, string text) => Write(relative, Encoding.Latin1.GetBytes(text));

    public static byte[] Bytes(int size, int seed)
    {
        var data = new byte[size];
        new Random(seed).NextBytes(data);
        return data;
    }

    // A model with a texture name stored the way OMSI does: a length byte, then the name.
    public static byte[] O3d(int size, string? texture)
    {
        var data = Bytes(size, size);
        for (var i = 0; i < data.Length; i++) if (data[i] == (byte)'.') data[i] = 0;
        if (texture is not null)
        {
            var name = Encoding.ASCII.GetBytes(texture);
            data[100] = (byte)name.Length;
            name.CopyTo(data, 101);
        }
        return data;
    }

    // True when another process maps the file: it cannot be truncated. When
    // it can, the content is written back.
    public static bool IsMapped(string path)
    {
        var content = File.ReadAllBytes(path);
        using var handle = CreateFileW(path, 0x40000000, 7, 0, 3, 0x80, 0);
        if (handle.IsInvalid) throw new IOException("cannot open " + path + " (" + Marshal.GetLastWin32Error() + ")");
        if (!SetEndOfFile(handle))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == 1224) return true;   // ERROR_USER_MAPPED_FILE
            throw new IOException("cannot truncate " + path + " (" + error + ")");
        }
        handle.Dispose();
        File.WriteAllBytes(path, content);
        return false;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFileW(string name, uint access, uint share, nint security, uint disposition, uint flags, nint template);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetEndOfFile(Microsoft.Win32.SafeHandles.SafeFileHandle file);

    public void Dispose()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try { Directory.Delete(Root, true); return; }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { Thread.Sleep(200); }
        }
    }
}
