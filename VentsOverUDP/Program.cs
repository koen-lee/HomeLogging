
using System.Net.Sockets;
using System.Text;

static class Program
{
    static readonly Dictionary<string, string> Commands = new()
    {
        { "Temperatures", "01b91f20212233bbba326a" },
        { "SwitchOn", "030101" },
        { "SwitchOff", "030100" },
        { "SpeedAndTemperature", "010102070872731f223a3b3c3d3e3f40414243" }
    };

    public static async Task Main(string host, string serial, string password = "1111", string readItems = "TemperatureOutsideIntake;TemperatureOutsideExhaust")
    {
        if (host == null || serial == null)
            throw new ArgumentNullException();
        Console.WriteLine("Hello, World!");

        var addresses = readItems.Split(';').Select(name => Enum.Parse<ItemAddress>(name)).ToArray();
        var device = new Device(host, serial, password);
        var items = await device.ReadAddresses(addresses);
        foreach (var i in items)
            Console.WriteLine($"{i.Key} : {BitConverter.ToString(i.Value)}");
    }
}