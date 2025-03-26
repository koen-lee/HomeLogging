using Raven.Client.Documents;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Session.TimeSeries;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using TelemetryToRaven;
using static Raven.Client.Constants;
using static System.Net.WebRequestMethods;

public class TestJson
{

    public static async Task Main(string[] args)
    {
        Console.WriteLine("Hello " + nameof(TestJson));
        HttpClient httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(50) };
        var baseURL = args.FirstOrDefault() ?? "http://raspberrypi:8889/data/";
        var result = await httpClient.GetStringAsync(baseURL);
        var parsed = JsonNode.Parse(result);
    }
}
