
using System.Net.Sockets;
using System.Text;

static class Program
{
    public static async Task Main(string host, string serial, string password = "1111")
    {
        if (host == null || serial == null)
            throw new ArgumentNullException();
        Console.WriteLine("Hello, World!");

        var udp = new UdpClient(host, 4000);
        var request = ComposeCommand("fdfd02", serial, password, "01b91f20212233bbba326a9a07");

        udp.Send(request.ToArray());
        Console.WriteLine("Sent");
        var timeoutsource = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var result = await udp.ReceiveAsync(timeoutsource.Token);
        if (timeoutsource.IsCancellationRequested)
        {
            Console.WriteLine("Timeout");
        }
        else
        {

            Console.WriteLine($"<- {result.RemoteEndPoint}");
            Console.WriteLine($"<- {GetString(result.Buffer)}");
            Console.WriteLine($"<- {Reply.ReadFrom(result.Buffer)}");
        }
    }

    record Reply
    {
        public string Serial { get; private set; }
        public string Password { get; private set; }
        public string PayloadString => GetString(Payload).Replace("-", "").ToLowerInvariant();
        public byte[] Command { get; private set; }
        public byte[] Payload { get; private set; }

        public static Reply ReadFrom(ReadOnlySpan<byte> buffer)
        {
            int i = 0;
            Eat(ref buffer, 3, out var command);
            Eat(ref buffer, 1, out var serialLength);
            Eat(ref buffer, serialLength[0], out var serialBytes);
            Eat(ref buffer, 1, out var passwordLength);
            Eat(ref buffer, passwordLength[0], out var passwordBytes);

            switch (GetString(command.ToArray()))
            {
                case TemperatureReply.CommandBytes:
                    return new TemperatureReply
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
    }

    record TemperatureReply : Reply
    {
        public const string CommandBytes = "FD-FD-02";
        public double OutsideTemperature => BitConverter.ToInt16(Payload,9) / 10.0;
        public double FanOutTemperature => BitConverter.ToInt16(Payload,24) / 10.0;
    }

    static void Eat<T>(ref ReadOnlySpan<T> buffer, int length, out ReadOnlySpan<T> eaten)
    {
        eaten = buffer.Slice(0, length);
        buffer = buffer[length..];
    }

    static byte[] ComposeCommand(string hexprefix, string serial, string password, string hexpostfix)
    {
        var serialBytes = Encoding.UTF8.GetBytes(serial);
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        return GetBytes(hexprefix)
            .Append((byte)serialBytes.Length).Concat(serialBytes)
            .Append((byte)password.Length).Concat(passwordBytes)
            .Concat(GetBytes(hexpostfix))
            .ToArray();
    }

    static byte[] GetBytes(ReadOnlySpan<char> s)
    {
        var result = new byte[s.Length / 2];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = byte.Parse(s.Slice(2 * i, 2), System.Globalization.NumberStyles.AllowHexSpecifier);
        }
        return result;
    }


    static string GetString(byte[] buffer)
    {
        return BitConverter.ToString(buffer);
    }
}