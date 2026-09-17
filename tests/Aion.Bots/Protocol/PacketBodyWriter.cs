using System.Buffers;
using System.Buffers.Binary;

namespace Aion.Bots.Protocol;

internal sealed class PacketBodyWriter
{
	private readonly ArrayBufferWriter<byte> buffer = new();

	public void C(int value) => Write(stackalloc byte[] { unchecked((byte)value) });

	public void H(int value)
	{
		var bytes = buffer.GetSpan(sizeof(short));
		BinaryPrimitives.WriteInt16LittleEndian(bytes, unchecked((short)value));
		buffer.Advance(sizeof(short));
	}

	public void UH(int value)
	{
		var bytes = buffer.GetSpan(sizeof(ushort));
		BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)value));
		buffer.Advance(sizeof(ushort));
	}

	public void D(int value)
	{
		var bytes = buffer.GetSpan(sizeof(int));
		BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
		buffer.Advance(sizeof(int));
	}

	public void Q(long value)
	{
		var bytes = buffer.GetSpan(sizeof(long));
		BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
		buffer.Advance(sizeof(long));
	}

	public void F(float value) => D(BitConverter.SingleToInt32Bits(value));

	public void B(ReadOnlySpan<byte> value) => Write(value);

	public void S(string value)
	{
		foreach (var character in value)
			UH(character);
		UH(0);
	}

	public void S(string value, int fixedCharacterCount)
	{
		for (var i = 0; i < fixedCharacterCount; i++)
			UH(i < value.Length ? value[i] : 0);
		UH(0);
	}

	public byte[] ToArray() => buffer.WrittenSpan.ToArray();

	private void Write(ReadOnlySpan<byte> value)
	{
		value.CopyTo(buffer.GetSpan(value.Length));
		buffer.Advance(value.Length);
	}
}
