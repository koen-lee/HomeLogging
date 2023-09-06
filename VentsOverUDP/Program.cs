
using System.Net.Sockets;
using System.Text;

static class Program
{
    static readonly Dictionary<string, string> Commands = new()
    {
        { "Temperatures", "01b91f20212233bbba326a" },
        { "SwitchOn", "030101" },
        { "SwitchOff", "030100" },
    };

    public static async Task Main(string host, string serial, string password = "1111", string command = "Temperatures")
    {
        if (host == null || serial == null)
            throw new ArgumentNullException();
        Console.WriteLine("Hello, World!");

        Commands.TryGetValue(command, out var resolvedcommand);
        var udp = new UdpClient(host, 4000);
        var request = ComposeCommand(Prefix, serial, password, resolvedcommand ?? command);

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
            Console.WriteLine($"<- from [{result.RemoteEndPoint}]");
            Console.WriteLine($"<- {BitConverter.ToString( result.Buffer)}]");
            Reply.ReadFrom(result.Buffer);
        }
    }

    static readonly string Prefix = "fdfd02";

    static byte[] ComposeCommand(string hexprefix, string serial, string password, string hexpostfix)
    {
        var serialBytes = Encoding.UTF8.GetBytes(serial);
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var command = GetBytes(hexprefix)
            .Append((byte)serialBytes.Length).Concat(serialBytes)
            .Append((byte)password.Length).Concat(passwordBytes)
            .Concat(GetBytes(hexpostfix))
            .ToArray();
        ushort calculatedSum = 0;
        foreach (var b in command[2..])
            calculatedSum += b;
        return command.Concat(BitConverter.GetBytes(calculatedSum)).ToArray();
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
}