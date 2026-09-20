using System.Buffers;
using System.Text;

namespace Aion.LogWatch;

internal sealed class FileTail
{
	private readonly string path;
	private readonly MemoryStream remainder = new();
	private long offset;

	public FileTail(string path) => this.path = Path.GetFullPath(path);

	public IEnumerable<string> ReadNewLines()
	{
		if (!File.Exists(path)) yield break;
		using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		long end = stream.Length; // A finite snapshot, even while a producer continues appending.
		if (end < offset) { offset = 0; remainder.SetLength(0); }
		stream.Position = offset;
		byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
		try
		{
			while (stream.Position < end)
			{
				int read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, end - stream.Position));
				if (read == 0) break;
				int start = 0;
				for (int index = 0; index < read; index++)
				{
					if (buffer[index] != (byte)'\n') continue;
					remainder.Write(buffer, start, index - start);
					int length = checked((int)remainder.Length);
					byte[] bytes = remainder.GetBuffer();
					if (length > 0 && bytes[length - 1] == (byte)'\r') length--;
					string line = Encoding.UTF8.GetString(bytes, 0, length);
					remainder.SetLength(0);
					offset += index - start + 1;
					start = index + 1;
					yield return line;
				}
				remainder.Write(buffer, start, read - start);
				offset += read - start;
			}
		}
		finally { ArrayPool<byte>.Shared.Return(buffer); }
	}
}
