using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Aion.GameServer.GeoEngine;

/// <summary>ImageIO raster replacement for shipped non-interlaced 16-bit gray heights and
/// 8-bit gray/indexed materials. Palette indices are material IDs, never converted to RGB.
/// Filter decoding adapted from ProjectObelisk bd00c3c tools/Obelisk.Import/Png16.cs.</summary>
internal static class PngReader
{
    internal sealed record Image(int Width, int Height, short[]? Heights, byte[]? Materials);

    internal static Image Read(byte[] png)
    {
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (png.Length < 8 || !png.AsSpan(0, 8).SequenceEqual(signature)) throw new InvalidDataException("Not a PNG file.");
        int width = 0, height = 0, depth = 0, color = 0;
        bool ended = false;
        using var compressed = new MemoryStream();
        for (int offset = 8; offset < png.Length;)
        {
            if (png.Length - offset < 12) throw new InvalidDataException("Truncated PNG chunk header.");
            int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset));
            if (length < 0 || length > png.Length - offset - 12) throw new InvalidDataException("Truncated PNG chunk.");
            string type = Encoding.ASCII.GetString(png, offset + 4, 4);
            var data = png.AsSpan(offset + 8, length);
            if (type == "IHDR")
            {
                if (width != 0 || length != 13) throw new InvalidDataException("Invalid PNG IHDR.");
                width = BinaryPrimitives.ReadInt32BigEndian(data);
                height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                depth = data[8]; color = data[9];
                if (width <= 0 || height <= 0 || data[10] != 0 || data[11] != 0 || data[12] != 0)
                    throw new InvalidDataException("Invalid or interlaced PNG terrain.");
                if (!(color == 0 && depth is 8 or 16 || color == 3 && depth == 8))
                    throw new InvalidDataException($"Unsupported terrain PNG depth {depth}, color type {color}.");
            }
            else if (type == "IDAT")
            {
                if (width == 0) throw new InvalidDataException("PNG IDAT precedes IHDR.");
                compressed.Write(data);
            }
            else if (type == "IEND") { ended = true; break; }
            offset += length + 12; // CRC is not part of the decoded raster (as in ImageIO).
        }
        if (!ended || width == 0 || compressed.Length == 0) throw new InvalidDataException("Incomplete PNG terrain.");
        int bpp = depth / 8, stride = checked(width * bpp), pixels = checked(width * height);
        short[]? heights = depth == 16 ? new short[pixels] : null;
        byte[]? materials = depth == 8 ? new byte[pixels] : null;
        byte[] previous = new byte[stride], current = new byte[stride];
        compressed.Position = 0;
        using var inflate = new ZLibStream(compressed, CompressionMode.Decompress);
        for (int y = 0; y < height; y++)
        {
            int filter = inflate.ReadByte();
            inflate.ReadExactly(current);
            Unfilter(filter, current, previous, bpp);
            if (heights != null)
                for (int x = 0; x < width; x++) heights[y * width + x] = BinaryPrimitives.ReadInt16BigEndian(current.AsSpan(x * 2));
            else current.CopyTo(materials!, y * width);
            (previous, current) = (current, previous);
        }
        if (inflate.ReadByte() != -1) throw new InvalidDataException("PNG terrain has excess pixel data.");
        return new Image(width, height, heights, materials);
    }

    private static void Unfilter(int filter, byte[] row, byte[] previous, int bpp)
    {
        if (filter is < 0 or > 4) throw new InvalidDataException($"Unknown PNG filter {filter}.");
        for (int i = 0; i < row.Length; i++)
        {
            int a = i >= bpp ? row[i - bpp] : 0, b = previous[i], c = i >= bpp ? previous[i - bpp] : 0;
            int predictor = filter switch { 0 => 0, 1 => a, 2 => b, 3 => (a + b) / 2, _ => Paeth(a, b, c) };
            row[i] = unchecked((byte)(row[i] + predictor));
        }
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = System.Math.Abs(p - a), pb = System.Math.Abs(p - b), pc = System.Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}
