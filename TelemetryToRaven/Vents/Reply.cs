using System;
using System.IO;
using System.Text;

public abstract record Reply
{
    public string Serial { get; private set; }
    public string Password { get; private set; }
    public string PayloadString => GetString(Payload).Replace("-", "").ToLowerInvariant();
    public byte[] Header { get; private set; }
    public byte[] Payload { get; private set; }

    protected abstract ushort CommandBytes { get; }
    public static T ReadFrom<T>(ReadOnlySpan<byte> buffer) where T : Reply, new()
    {
        VerifyChecksum(buffer);
        Eat(ref buffer, 3, out var header);
        Eat(ref buffer, 1, out var serialLength);
        Eat(ref buffer, serialLength[0], out var serialBytes);
        Eat(ref buffer, 1, out var passwordLength);
        Eat(ref buffer, passwordLength[0], out var passwordBytes);

        var result = new T()
        {
            Header = header.ToArray(),
            Serial = Encoding.UTF8.GetString(serialBytes),
            Password = Encoding.UTF8.GetString(passwordBytes),
            Payload = buffer.ToArray(),
        };

        if (BitConverter.ToUInt16(buffer) != result.CommandBytes)
            throw new InvalidDataException();
        return result;
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


public record OnOffReply : Reply
{
    protected override ushort CommandBytes { get; } = 0x0106;
}

public record TemperatureReply : Reply
{
    protected override ushort CommandBytes { get; } = 0xFE06;
    public double OutsideTemperature => BitConverter.ToInt16(Payload, 9) / 10.0;
    public double ExhaustTemperature => BitConverter.ToInt16(Payload, 24) / 10.0;
}
