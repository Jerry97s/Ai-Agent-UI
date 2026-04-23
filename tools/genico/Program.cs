// logo.png → 다중 해상도 PNG 기반 app.ico (EXE/작업 표시줄용 Win32 리소스)
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// bin/Release/net8.0-windows → 저장소 루트까지 5단계 상위
var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var pngPath = Path.Combine(repoRoot, "AiAgentUi", "Assets", "logo.png");
var icoPath = Path.Combine(repoRoot, "AiAgentUi", "Assets", "app.ico");

if (!File.Exists(pngPath))
{
    Console.Error.WriteLine("Missing: " + pngPath);
    return 1;
}

using var src = new Bitmap(pngPath);
var sizes = new[] { 16, 24, 32, 48, 64, 128, 256 };
var pngChunks = new List<byte[]>(sizes.Length);

foreach (var sz in sizes)
{
    using var bmp = new Bitmap(sz, sz, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(bmp))
    {
        g.Clear(Color.Transparent);
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(src, 0, 0, sz, sz);
    }

    using var ms = new MemoryStream();
    bmp.Save(ms, ImageFormat.Png);
    pngChunks.Add(ms.ToArray());
}

await File.WriteAllBytesAsync(icoPath, BuildIcoFromPngImages(pngChunks, sizes));
Console.WriteLine("Wrote " + icoPath);
return 0;

static byte[] BuildIcoFromPngImages(IReadOnlyList<byte[]> pngImages, int[] dims)
{
    ushort count = (ushort)pngImages.Count;
    const int iconDirSize = 6;
    const int entrySize = 16;
    var headerSize = iconDirSize + count * entrySize;

    using var ms = new MemoryStream();
    WriteUInt16LE(ms, 0);
    WriteUInt16LE(ms, 1);
    WriteUInt16LE(ms, count);

    var nextOffset = headerSize;
    for (var i = 0; i < count; i++)
    {
        var png = pngImages[i];
        var dim = dims[i];
        // ICO: 0 means 256 for width/height
        byte wb = dim >= 256 ? (byte)0 : (byte)dim;
        byte hb = dim >= 256 ? (byte)0 : (byte)dim;

        ms.WriteByte(wb);
        ms.WriteByte(hb);
        ms.WriteByte(0);
        ms.WriteByte(0);
        WriteUInt16LE(ms, 1);
        WriteUInt16LE(ms, 32);
        WriteUInt32LE(ms, (uint)png.Length);
        WriteUInt32LE(ms, (uint)nextOffset);
        nextOffset += png.Length;
    }

    foreach (var png in pngImages)
        ms.Write(png);

    return ms.ToArray();
}

static void WriteUInt16LE(Stream s, ushort v)
{
    s.WriteByte((byte)(v & 0xff));
    s.WriteByte((byte)(v >> 8));
}

static void WriteUInt32LE(Stream s, uint v)
{
    s.WriteByte((byte)(v & 0xff));
    s.WriteByte((byte)((v >> 8) & 0xff));
    s.WriteByte((byte)((v >> 16) & 0xff));
    s.WriteByte((byte)((v >> 24) & 0xff));
}
