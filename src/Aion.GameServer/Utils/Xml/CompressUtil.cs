using System.IO;
using System.IO.Compression;

namespace Aion.GameServer.Utils.Xml;

/// <summary>
/// Java parity: utils/xml/CompressUtil (Rolandas). Java java.util.zip.Deflater/Inflater produce/consume zlib (RFC1950)
/// format → C# System.IO.Compression.ZLibStream (zlib-compatible, unlike raw DeflateStream).
/// </summary>
public static class CompressUtil
{
    /// <summary>Requires one complete zlib stream, no remaining bytes and the exact inflated size.</summary>
    public static bool InflatesTo(byte[] bytes, int size)
    {
        using var input = new InflationInput(bytes);
        try
        {
            using var decompressor = new ZLibStream(input, CompressionMode.Decompress);
            byte[] buffer = new byte[1024];
            long inflatedSize = 0;
            int count;
            while ((count = decompressor.Read(buffer, 0, buffer.Length)) != 0)
            {
                inflatedSize += count;
                if (inflatedSize > size)
                    return false;
            }
            // Java's zero-count/needsInput check also rejects an encoded empty stream.
            // PlayerScripts separately accepts null/empty compressed bytes with declared size zero.
            return inflatedSize > 0 && inflatedSize == size && input.BytesRead == bytes.Length;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (IOException)
        {
            // The in-memory source cannot fail I/O; zlib reports dictionary requests this way.
            return false;
        }
    }

    // ZLibStream hides the inflater's Finished/Remaining state and permits EOF on a truncated stream.
    // Feed one byte at a time to prevent read-ahead, followed by a sentinel. A complete stream stops
    // before consuming the sentinel; an incomplete one consumes it. Trailing input remains unread.
    private sealed class InflationInput(byte[] bytes) : Stream
    {
        public int BytesRead { get; private set; }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count == 0 || BytesRead > bytes.Length) return 0;
            buffer[offset] = BytesRead < bytes.Length ? bytes[BytesRead] : (byte)0;
            BytesRead++;
            return 1;
        }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => bytes.Length + 1L;
        public override long Position { get => BytesRead; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    public static byte[] Compress(byte[] bytes)
    {
        using MemoryStream bos = new();
        using (ZLibStream compressor = new(bos, CompressionMode.Compress, true))
        {
            compressor.Write(bytes, 0, bytes.Length);
        }
        return bos.ToArray();
    }
}
