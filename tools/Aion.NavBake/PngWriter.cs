using System.Buffers.Binary;
using System.IO.Compression;
using System.IO.Hashing;

namespace Aion.NavBake;

/// <summary>Minimal RGB PNG encoder (no image library dependency).</summary>
internal static class PngWriter
{
	public static void Write(string path, int width, int height, byte[] rgb)
	{
		using var file = File.Create(path);
		file.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
		var header = new byte[13];
		BinaryPrimitives.WriteInt32BigEndian(header, width);
		BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
		header[8] = 8; header[9] = 2;
		Chunk(file, "IHDR", header);
		using var raw = new MemoryStream();
		using (var z = new ZLibStream(raw, CompressionLevel.Fastest, true))
			for (int y = 0; y < height; y++)
			{
				z.WriteByte(0);
				z.Write(rgb, y * width * 3, width * 3);
			}
		Chunk(file, "IDAT", raw.ToArray());
		Chunk(file, "IEND", []);
	}

	private static void Chunk(Stream stream, string type, byte[] data)
	{
		Span<byte> length = stackalloc byte[4];
		BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
		stream.Write(length);
		byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
		stream.Write(typeBytes);
		stream.Write(data);
		var crc = new Crc32();
		crc.Append(typeBytes);
		crc.Append(data);
		Span<byte> sum = stackalloc byte[4];
		BinaryPrimitives.WriteUInt32BigEndian(sum, crc.GetCurrentHashAsUInt32());
		stream.Write(sum);
	}
}
