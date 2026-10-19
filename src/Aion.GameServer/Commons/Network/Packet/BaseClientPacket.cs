using System;
using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using Aion.Commons.Lang;
using Aion.Commons.Nio;
using Aion.GameServer.Commons.Utils;

namespace Aion.GameServer.Commons.Network.Packet;

/// <summary>
/// Java parity: commons/network/packet/BaseClientPacket (-Nemesiss-). Base for every client packet.
/// Connection owner T is unconstrained, supporting native transports. implements
/// Runnable -> Runnable (Run() left abstract for AionClientPacket). Nio BufferUnderflow becomes one failed Read,
/// with no default values or partial strings returned from helpers. ConcurrentHashMap.newKeySet
/// -> ConcurrentDictionary used as a once-set.
/// </summary>
public abstract class BaseClientPacket<T> : BasePacket, Runnable
{
    private static readonly ILogger log = AionLog.For("BaseClientPacket");
    private static readonly ConcurrentDictionary<int, bool> partiallyReadPackets = new ConcurrentDictionary<int, bool>();

    private T client = default!;
    private ByteBuffer buf = null!;

    public BaseClientPacket(int opcode) : this(null!, opcode)
    {
    }

    public BaseClientPacket(ByteBuffer buf, int opcode) : base(opcode)
    {
        this.buf = buf;
    }

    public void SetBuffer(ByteBuffer buf) => this.buf = buf;

    public void SetConnection(T client) => this.client = client;

    /// <summary>Clears process-wide warning suppression between isolated simulation scenarios.</summary>
    internal static void ResetPartiallyReadPacketWarnings() => partiallyReadPackets.Clear();

    /// <summary>Reads data from the packet buffer. On error the connection is closed.</summary>
    public bool Read()
    {
        int startPos = buf.Position();
        try
        {
            ReadImpl();

            if (GetRemainingBytes() > 0 && partiallyReadPackets.TryAdd(GetOpCode(), true))
                log.LogWarning("{This} was not fully read! Last {GetRemainingBytes} bytes were not read from buffer:\n{NetworkUtilsToHexBufStartPosBufLimit}", this, GetRemainingBytes(), NetworkUtils.ToHex(buf, startPos, buf.Limit()));

            return true;
        }
        catch (InvalidOperationException ex) when (ex.Message == "BufferUnderflow")
        {
            // The shared Nio shim uses this exception/message for Java BufferUnderflowException.
            log.LogWarning("{Packet} from {Client} is shorter than expected:\n{Content}", this, client,
                NetworkUtils.ToHex(buf, startPos, buf.Limit()));
            return false;
        }
        catch (Exception ex)
        {
            string msg = "Reading failed for packet " + this + ". Buffer Info";
            if (GetRemainingBytes() > 0)
                msg += " (last " + GetRemainingBytes() + " bytes were not read)";
            msg += ":\n" + NetworkUtils.ToHex(buf, startPos, buf.Limit());
            log.LogError(ex, msg);
            return false;
        }
    }

    /// <summary>Data reading implementation.</summary>
    protected abstract void ReadImpl();

    public int GetRemainingBytes() => buf.Remaining();

    protected int ReadD()
    {
        return buf.GetInt();
    }

    protected byte ReadC()
    {
        return buf.Get();
    }

    protected int ReadUC()
    {
        return buf.Get() & 0xFF;
    }

    protected short ReadH()
    {
        return buf.GetShort();
    }

    protected int ReadUH()
    {
        return buf.GetShort() & 0xFFFF;
    }

    protected double ReadDF()
    {
        return buf.GetDouble();
    }

    protected float ReadF()
    {
        return buf.GetFloat();
    }

    protected long ReadQ()
    {
        return buf.GetLong();
    }

    protected string ReadS()
    {
        StringBuilder sb = new StringBuilder();
        char ch;
        while ((ch = buf.GetChar()) != 0)
            sb.Append(ch);
        return sb.ToString();
    }

    protected byte[] ReadB(int length)
    {
        if (length < 0 || length > buf.Remaining())
            throw new InvalidOperationException("BufferUnderflow");
        byte[] result = new byte[length];
        buf.Get(result);
        return result;
    }

    /// <summary>Execute this packet's action.</summary>
    protected abstract void RunImpl();

    /// <summary>java.lang.Runnable.run() — provided by AionClientPacket.</summary>
    public abstract void Run();

    public T GetConnection() => client;
}
