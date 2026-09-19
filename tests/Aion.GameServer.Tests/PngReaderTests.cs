using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Aion.GameServer.GeoEngine;

namespace Aion.GameServer.Tests;

public sealed class PngReaderTests
{
    [Theory]
    [InlineData(16, 0)]
    [InlineData(8, 0)]
    [InlineData(8, 3)]
    public void EveryFilterPreservesUnsignedSamplesAndPaletteIndices(int depth, int color)
    {
        ushort[] samples = Enumerable.Range(0, 15).Select(i => (ushort)(depth == 16 ? i * 4111 + 7 : i * 17)).ToArray();
        samples[7] = depth == 16 ? ushort.MaxValue : byte.MaxValue;
        var image = PngReader.Read(Encode(3, 5, samples, depth, color, [0, 1, 2, 3, 4]));
        Assert.Equal(3, image.Width); Assert.Equal(5, image.Height);
        if (depth == 16)
        {
            Assert.Null(image.Materials);
            Assert.Equal(samples.Select(s => unchecked((short)s)), image.Heights!);
        }
        else
        {
            Assert.Null(image.Heights);
            Assert.Equal(samples.Select(s => (byte)s), image.Materials!);
        }
    }

    [Fact]
    public void InvalidAndUnsupportedImagesFailInsteadOfProducingEmptyTerrain()
    {
        Assert.Throws<InvalidDataException>(() => PngReader.Read([]));
        byte[] valid = Encode(1, 1, [320], 16, 0, [0]);
        Assert.Throws<InvalidDataException>(() => PngReader.Read(valid[..^12]));
        var interlaced = valid.ToArray(); interlaced[28] = 1;
        Assert.Throws<InvalidDataException>(() => PngReader.Read(interlaced));
        var rgb = valid.ToArray(); rgb[25] = 2;
        Assert.Throws<InvalidDataException>(() => PngReader.Read(rgb));
        var badLength = valid.ToArray(); BinaryPrimitives.WriteInt32BigEndian(badLength.AsSpan(8), int.MaxValue);
        Assert.Throws<InvalidDataException>(() => PngReader.Read(badLength));
    }

    // Independent PNG encoder derived from ProjectObelisk bd00c3c's spec-based test writer.
    internal static byte[] Encode(int width, int height, ushort[] samples, int depth, int color, byte[] filters)
    {
        int bpp = depth / 8, stride = width * bpp;
        using var raw = new MemoryStream();
        byte[] previous = new byte[stride];
        for (int y = 0; y < height; y++)
        {
            var current = new byte[stride];
            for (int x = 0; x < width; x++)
                if (depth == 16) BinaryPrimitives.WriteUInt16BigEndian(current.AsSpan(x * 2), samples[y * width + x]);
                else current[x] = (byte)samples[y * width + x];
            byte filter = filters[y % filters.Length]; raw.WriteByte(filter);
            for (int i = 0; i < stride; i++)
            {
                int a = i >= bpp ? current[i - bpp] : 0, b = previous[i], c = i >= bpp ? previous[i - bpp] : 0;
                int p = a + b - c;
                int paeth = new (int Value, int Distance)[] { (a, Math.Abs(p - a)), (b, Math.Abs(p - b)), (c, Math.Abs(p - c)) }
                    .OrderBy(v => v.Distance).First().Value;
                int predictor = filter switch { 0 => 0, 1 => a, 2 => b, 3 => (a + b) >> 1, _ => paeth };
                raw.WriteByte(unchecked((byte)(current[i] - predictor)));
            }
            previous = current;
        }
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true)) zlib.Write(raw.ToArray());
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width); BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = (byte)depth; header[9] = (byte)color;
        using var png = new MemoryStream();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        Chunk("IHDR", header);
        if (color == 3) Chunk("PLTE", Enumerable.Range(0, 768).Select(i => (byte)(255 - i % 256)).ToArray());
        byte[] data = compressed.ToArray();
        Chunk("IDAT", data[..(data.Length / 2)]); Chunk("IDAT", data[(data.Length / 2)..]);
        Chunk("IEND", []);
        return png.ToArray();

        void Chunk(string type, byte[] content)
        {
            var chunk = new byte[12 + content.Length];
            BinaryPrimitives.WriteInt32BigEndian(chunk, content.Length);
            Encoding.ASCII.GetBytes(type).CopyTo(chunk, 4); content.CopyTo(chunk, 8);
            uint crc = uint.MaxValue;
            foreach (byte b in chunk.AsSpan(4, content.Length + 4))
            {
                crc ^= b;
                for (int bit = 0; bit < 8; bit++) crc = (crc & 1) == 0 ? crc >> 1 : (crc >> 1) ^ 0xedb88320;
            }
            BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(content.Length + 8), ~crc);
            png.Write(chunk);
        }
    }
}
