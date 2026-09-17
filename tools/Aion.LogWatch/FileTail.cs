using System.Text;

namespace Aion.LogWatch;

internal sealed class FileTail
{
	private readonly string path;
	private byte[] remainder = [];
	private long offset;

	public FileTail(string path)
	{
		this.path = Path.GetFullPath(path);
	}

	public IReadOnlyList<string> ReadNewLines()
	{
		if (!File.Exists(path))
			return [];
		using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		if (stream.Length < offset)
		{
			offset = 0;
			remainder = [];
		}
		stream.Position = offset;
		var addedLength = checked((int)(stream.Length - offset));
		if (addedLength == 0)
			return [];
		var bytes = new byte[remainder.Length + addedLength];
		remainder.CopyTo(bytes, 0);
		stream.ReadExactly(bytes.AsSpan(remainder.Length));
		offset = stream.Position;

		var lines = new List<string>();
		var start = 0;
		for (var index = 0; index < bytes.Length; index++)
		{
			if (bytes[index] != (byte)'\n')
				continue;
			var length = index - start;
			if (length > 0 && bytes[index - 1] == (byte)'\r')
				length--;
			lines.Add(Encoding.UTF8.GetString(bytes, start, length));
			start = index + 1;
		}
		remainder = bytes[start..];
		return lines;
	}
}
