using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace TelemetryToRaven.Kasa;

/// <summary>
/// https://github.com/softScheck/tplink-smartplug/blob/master/tplink-smarthome-commands.txt
/// </summary>
public static class HS110Requests
{
    public static readonly dynamic Power = new { emeter = new { get_realtime = new { } } };

    // Get System Info(Software & Hardware Versions, MAC, deviceID, hwID etc.)
    // { "system":{ "get_sysinfo":null} }

    public static readonly dynamic Info = new { system = new { get_sysinfo = new { } } };

    // {"system":{ "set_relay_state":{ "state":1}}} 
    // {"system":{ "set_relay_state":{ "state":0} } }
    public static readonly dynamic TurnOff = new { system = new { set_relay_state = new { state = 0 } } };
    public static readonly dynamic TurnOn = new { system = new { set_relay_state = new { state = 1 } } };
}

public class HS110Device
{
    private readonly string _hostname;
    private readonly int _port;

    public HS110Device(string hostname, int port = 9999)
    {
        _hostname = hostname;
        _port = port;
    }

    public async Task<PowerReading> GetPowerReading(CancellationToken cancellationToken = default)
    {
        var result = await DoRequest(HS110Requests.Power, cancellationToken);
        return new PowerReading(result);
    }

    public async Task<InfoReading> GetInfoReading(CancellationToken cancellationToken = default)
    {
        var result = await DoRequest(HS110Requests.Info, cancellationToken);
        return new InfoReading(result);
    }

    public async Task<dynamic> DoRequest(object req, CancellationToken cancellationToken = default)
    {
        dynamic deserialized;
        using (var client = new TcpClient())
        {
            client.ReceiveTimeout = 30_000; /*ms*/  
            client.SendTimeout = 30_000; /*ms*/
            await client.ConnectAsync(_hostname, _port);
            await using (var stream = client.GetStream())
            {
                await stream.WriteRequestAsync(req, cancellationToken);
                deserialized = await stream.GetResponseAsync(cancellationToken);
            }
        }

        return deserialized;
    }
}