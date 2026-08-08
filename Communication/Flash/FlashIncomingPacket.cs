using System.Runtime.InteropServices;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Flash;

public class FlashIncomingPacket : IIncomingPacket
{
    public Memory<byte> Buffer { get; set; }
    public int MessageId { get; set; }

    public byte ReadByte()
    {
        var span = Buffer.Span;
        var result = MemoryMarshal.Read<byte>(span);
        Buffer = Buffer.Slice(sizeof(byte));
        return result;
    }

    public short ReadShort()
    {
        var span = Buffer.Span.Slice(0, sizeof(short));
        span.Reverse();
        var result = MemoryMarshal.Read<short>(span);
        Buffer = Buffer.Slice(sizeof(short));
        return result;
    }
    public ushort ReadUShort()
    {
        var span = Buffer.Span.Slice(0, sizeof(ushort));
        span.Reverse();
        var result = MemoryMarshal.Read<ushort>(span);
        Buffer = Buffer.Slice(sizeof(ushort));
        return result;
    }

    public int ReadInt()
    {
        var span = Buffer.Span.Slice(0, sizeof(int));
        span.Reverse();
        var result = MemoryMarshal.Read<int>(span);
        Buffer = Buffer.Slice(sizeof(int));
        return result;
    }
    public uint ReadUInt()
    {
        var span = Buffer.Span.Slice(0, 4);
        span.Reverse();
        var result = MemoryMarshal.Read<uint>(span);
        Buffer = Buffer.Slice(sizeof(uint));
        return result;
    }

    public bool ReadBool() => ReadByte() == 1;

    public string ReadString()
    {
        var length = ReadUShort();
        var value = System.Text.Encoding.UTF8.GetString(Buffer.Span.Slice(0, length));
        Buffer = Buffer.Slice(length);
        return value;
    }

    public bool HasDataRemaining() => !Buffer.IsEmpty;
    public byte[] ReadFixedValue()
    {
        var length = ReadUShort();
        var span = Buffer.Slice(0, length);
        Buffer = Buffer.Slice(length);
        return span.ToArray();
    }

    public void ReadBytes(Span<byte> destination)
    {
        Buffer.Span.Slice(0, destination.Length).CopyTo(destination);
        Buffer = Buffer.Slice(destination.Length);
    }
}