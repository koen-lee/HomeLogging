using System.Text;

record Reply
{
    public string Serial { get; private set; }
    public string Password { get; private set; }
    public string PayloadString => GetString(Payload).Replace("-", "").ToLowerInvariant();
    public byte[] Command { get; private set; }
    public byte[] Payload { get; private set; }

    private const byte Cmd_Page = 0xFF;
    private const byte Cmd_Function = 0xFC;
    private const byte Cmd_Size = 0xFE;
    private const byte Cmd_Not_Supported = 0xFD;

    private const ushort Packet_Header = 0xfdfd;

    public static void ReadFrom(ReadOnlySpan<byte> buffer)
    {
        VerifyChecksum(buffer);
        Eat(ref buffer, 2);

        var version = Eat(ref buffer);
        if (version != 0x02) throw new InvalidDataException($"Reply version mismatch: got {version}");
        var serialLength = Eat(ref buffer);
        var serialBytes = Eat(ref buffer, serialLength);
        var passwordLength = Eat(ref buffer);
        var passwordBytes = Eat(ref buffer, passwordLength);
        byte page = 0;
        byte size = 1;
        var function = Eat(ref buffer);
        if (function != 0x06) throw new InvalidDataException($"Reply function mismatch: got {function}");
        while (buffer.Length > 2)
        {
            var next = Eat(ref buffer);
            switch (next)
            {
                case Cmd_Page:
                    page = Eat(ref buffer);
                    break;
                case Cmd_Size:
                    size = Eat(ref buffer);
                    break;
                case Cmd_Not_Supported:
                    Eat(ref buffer); // skip the not supported address
                    break;
                default:
                    var address = page << 8 | next;
                    var data = Eat(ref buffer, size);
                    Console.WriteLine($"{address:x4} ({address:0000}) = {BitConverter.ToString(data.ToArray())}");
                    size = 1;
                    break;
            }
        }
    }

    private static void VerifyChecksum(ReadOnlySpan<byte> buffer)
    {
        ushort recieved = BitConverter.ToUInt16(buffer);
        if (recieved != Packet_Header)
            throw new InvalidDataException($"header mismatch, got {recieved}");
        ushort calculatedSum = 0;
        foreach (var b in buffer[2..^2])
            calculatedSum += b;

        var checksumFromReply = BitConverter.ToUInt16(buffer[^2..]);
        if (checksumFromReply != calculatedSum)
            throw new InvalidDataException("checksum mismatch");
    }

    static string GetString(byte[] buffer)
    {
        return BitConverter.ToString(buffer);
    }

    static ReadOnlySpan<T> Eat<T>(ref ReadOnlySpan<T> buffer, int length)
    {
        var eaten = buffer.Slice(0, length);
        buffer = buffer[length..];
        return eaten;
    }
    static T Eat<T>(ref ReadOnlySpan<T> buffer)
    {
        var result = buffer[0];
        buffer = buffer[1..];
        return result;
    }
}


record OnOffReply : Reply
{
    public const ushort CommandBytes = 0x0106;
}

record TemperatureReply : Reply
{
    public const ushort CommandBytes = 0xFE06;
    public double OutsideTemperature => BitConverter.ToInt16(Payload, 9) / 10.0;
    public double FanOutTemperature => BitConverter.ToInt16(Payload, 24) / 10.0;
}
