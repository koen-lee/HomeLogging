using System.Text;

record Reply
{
    public string Serial { get; private set; }
    public string Password { get; private set; }
    public string PayloadString => GetString(Payload).Replace("-", "").ToLowerInvariant();
    public byte[] Command { get; private set; }
    public byte[] Payload { get; private set; }

    public static Reply ReadFrom(ReadOnlySpan<byte> buffer)
    {
        VerifyChecksum(buffer);
        Eat(ref buffer, 3, out var command);
        Eat(ref buffer, 1, out var serialLength);
        Eat(ref buffer, serialLength[0], out var serialBytes);
        Eat(ref buffer, 1, out var passwordLength);
        Eat(ref buffer, passwordLength[0], out var passwordBytes);

        switch (BitConverter.ToUInt16(buffer))
        {
            case TemperatureReply.CommandBytes:
                return new TemperatureReply
                {
                    Command = command.ToArray(),
                    Serial = Encoding.UTF8.GetString(serialBytes),
                    Password = Encoding.UTF8.GetString(passwordBytes),
                    Payload = buffer.ToArray(),
                };
            case OnOffReply.CommandBytes:
                return new OnOffReply
                {
                    Command = command.ToArray(),
                    Serial = Encoding.UTF8.GetString(serialBytes),
                    Password = Encoding.UTF8.GetString(passwordBytes),
                    Payload = buffer.ToArray(),
                };
            default:

                return new Reply
                {
                    Command = command.ToArray(),
                    Serial = Encoding.UTF8.GetString(serialBytes),
                    Password = Encoding.UTF8.GetString(passwordBytes),
                    Payload = buffer.ToArray(),
                };
        }
    }

    private static void VerifyChecksum(ReadOnlySpan<byte> buffer)
    {
        if (BitConverter.ToUInt16(buffer) != 0xfdfd)
            throw new InvalidDataException("header mismatch");
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

    static void Eat<T>(ref ReadOnlySpan<T> buffer, int length, out ReadOnlySpan<T> eaten)
    {
        eaten = buffer.Slice(0, length);
        buffer = buffer[length..];
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
