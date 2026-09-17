using System.Buffers.Binary;
using System.Text;

namespace Aion.Bots.Protocol;

/// <summary>Bounds-checked little-endian reader for a decoded Aion packet body.</summary>
public ref struct PacketBodyReader
{
	private readonly ReadOnlySpan<byte> body;
	private int position;

	public PacketBodyReader(ReadOnlySpan<byte> body) => this.body = body;

	public readonly int Position => position;

	public readonly int Remaining => body.Length - position;

	public byte ReadByte()
	{
		Require(sizeof(byte));
		return body[position++];
	}

	public ushort ReadUInt16()
	{
		Require(sizeof(ushort));
		var value = BinaryPrimitives.ReadUInt16LittleEndian(body[position..]);
		position += sizeof(ushort);
		return value;
	}

	public short ReadInt16() => unchecked((short)ReadUInt16());

	public int ReadInt32()
	{
		Require(sizeof(int));
		var value = BinaryPrimitives.ReadInt32LittleEndian(body[position..]);
		position += sizeof(int);
		return value;
	}

	public long ReadInt64()
	{
		Require(sizeof(long));
		var value = BinaryPrimitives.ReadInt64LittleEndian(body[position..]);
		position += sizeof(long);
		return value;
	}

	public float ReadSingle() => BitConverter.Int32BitsToSingle(ReadInt32());

	public byte[] ReadBytes(int length)
	{
		Require(length);
		var value = body.Slice(position, length).ToArray();
		position += length;
		return value;
	}

	public byte[] ReadLengthPrefixedBlob() => ReadBytes(ReadUInt16());

	public string ReadString()
	{
		var start = position;
		while (true)
		{
			Require(sizeof(char));
			if (BinaryPrimitives.ReadUInt16LittleEndian(body[position..]) == 0)
			{
				var value = Encoding.Unicode.GetString(body[start..position]);
				position += sizeof(char);
				return value;
			}
			position += sizeof(char);
		}
	}

	public void Skip(int length)
	{
		Require(length);
		position += length;
	}

	private readonly void Require(int length)
	{
		if (length < 0 || length > Remaining)
			throw new InvalidDataException(
				$"Packet body ended at offset {position}; requested {length} byte(s), {Remaining} remain.");
	}
}
