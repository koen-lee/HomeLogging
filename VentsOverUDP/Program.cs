static class Program
{
    /// <summary>
    /// Communicates with Blauberg/Vents/... decentralized heat recovery fans
    /// </summary>
    /// <param name="serial">Fan serial number, as found on the sticker</param>
    /// <param name="password"></param>
    /// <param name="host">IP address or hostname, default is broadcast</param>
    /// <param name="readItems">;-separated list of items to read</param>
    /// <param name="writeItems">;-separated list of name=hexvalue pairs to write (eg OnOff=01) </param>
    /// <param name="listItems">Print the known items that may be read or sometimes written</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static async Task Main(string serial,
        string password = "1111",
        string host = "255.255.255.255",
        string readItems = "",
        string writeItems = "",
        bool listItems = false)
    {
        if (listItems)
        {
            foreach (var item in Enum.GetNames<ItemAddress>())
                Console.WriteLine(item);
            return;
        }
        if (host == null || serial == null)
            throw new ArgumentNullException();

        var device = new Device(host, serial, password);
        await DoWrite(writeItems, device);
        await DoRead(readItems, device);
    }

    private static async Task DoWrite(string readItems, Device device)
    {
        if (string.IsNullOrWhiteSpace(readItems)) return;
        var addresses = readItems.Split(';')
            .Select(item => item.Split('=')).ToDictionary(
            item => Enum.Parse<ItemAddress>(item[0]),
            item => Convert.FromHexString(item[1])
        );
        await device.WriteAddresses(addresses);
    }

    private static async Task DoRead(string readItems, Device device)
    {
        if (string.IsNullOrWhiteSpace(readItems)) return;
        var addresses = readItems.Split(';').Select(name => Enum.Parse<ItemAddress>(name)).ToArray();
        var items = await device.ReadAddresses(addresses);
        foreach (var i in items)
            Console.WriteLine($"{i.Key} : {BitConverter.ToString(i.Value)}");
    }
}