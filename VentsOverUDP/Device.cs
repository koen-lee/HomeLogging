using System.Net.Sockets;
using System.Text;

class Device
{
    private const byte Cmd_Page = 0xFF;
    private const byte Cmd_Function = 0xFC;
    private const byte Cmd_Size = 0xFE;
    private const byte Cmd_Not_Supported = 0xFD;
    private const byte Function_Read = 0x01;
    private const byte Function_ReadWrite = 0x03;

    private const ushort Packet_Header = 0xfdfd;

    public string Hostname { get; }
    public string Serial { get; }
    public string Password { get; }

    public Device(string hostname, string serial, string password)
    {
        Hostname = hostname;
        Serial = serial;
        Password = password;
    }

    public async Task<Dictionary<ItemAddress, byte[]>> ReadAddresses(params ItemAddress[] addresses)
    {
        List<byte> command = new();
        command.Add(Function_Read);
        byte page = 0;
        foreach (int addr in addresses)
        {
            var thispage = (byte)(addr >> 8);
            if (thispage != page)
            {
                command.Add(Cmd_Page);
                command.Add(thispage);
                page = thispage;
            }
            command.Add((byte)(addr & 0xff));
        }
        var request = ComposeCommand(Serial, Password, command);

        var udp = new UdpClient(Hostname, 4000);
        udp.Send(request.ToArray());
        Console.WriteLine("Sent");
        var timeoutsource = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var result = await udp.ReceiveAsync(timeoutsource.Token);
        if (timeoutsource.IsCancellationRequested)
        {
            throw new TimeoutException();
        }
        else
        {
            Console.WriteLine($"<- from [{result.RemoteEndPoint}]");
            Console.WriteLine($"<- {BitConverter.ToString(result.Buffer)}]");
            return ReadReply(result.Buffer);
        }
    }


    static byte[] ComposeCommand(string serial, string password, IEnumerable<byte> payload)
    {
        var serialBytes = Encoding.UTF8.GetBytes(serial);
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var command = BitConverter.GetBytes(Packet_Header)
            .Append((byte)0x02)
            .Append((byte)serialBytes.Length).Concat(serialBytes)
            .Append((byte)password.Length).Concat(passwordBytes)
            .Concat(payload)
            .ToArray();
        ushort calculatedSum = 0;
        foreach (var b in command[2..])
            calculatedSum += b;
        return command.Concat(BitConverter.GetBytes(calculatedSum)).ToArray();
    }

    public static Dictionary<ItemAddress, byte[]> ReadReply(ReadOnlySpan<byte> buffer)
    {
        VerifyChecksum(buffer);
        Eat(ref buffer, 2);
        var version = Eat(ref buffer);
        if (version != 0x02) throw new InvalidDataException($"Reply version mismatch: got {version}");
        var serialLength = Eat(ref buffer);
        var serialBytes = Eat(ref buffer, serialLength);
        var passwordLength = Eat(ref buffer);
        var passwordBytes = Eat(ref buffer, passwordLength);
        var function = Eat(ref buffer);
        if (function != 0x06) throw new InvalidDataException($"Reply function mismatch: got {function}");
        return ParseItems(buffer);
    }

    private static Dictionary<ItemAddress, byte[]> ParseItems(ReadOnlySpan<byte> buffer)
    {
        Dictionary<ItemAddress, byte[]> result = new();
        byte page = 0;
        byte size = 1;
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
                    Console.WriteLine($"{(ItemAddress)address} = {BitConverter.ToString(data.ToArray())}");
                    result[(ItemAddress)address] = data.ToArray();
                    size = 1;
                    break;
            }
        }
        return result;
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