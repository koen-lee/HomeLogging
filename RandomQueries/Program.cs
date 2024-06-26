using Raven.Client.Documents;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Queries;
using Raven.Client.Documents.Queries.TimeSeries;
using System.Runtime.CompilerServices;
using TelemetryToRaven;
using TelemetryToRaven.Gpio;

internal class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");

        var store = new DocumentStore()
        {
            Database = "Eiland17Logging",
            Urls = new[] { "http://tinkerboard:8080" }
        };

        store.Conventions.FindCollectionName = type =>
        {
            if (typeof(Meter).IsAssignableFrom(type))
                return "Meters";

            return DocumentConventions.DefaultGetCollectionName(type);
        };
        store.Initialize();
        var session = store.OpenSession();


        var prices = GetTs(session, "APX", "Apx", "Documents");
        var powers = GetTs(session, "meters/ISK5\\2M550T-1013", "Power");
        var solar1 = GetTs(session, "Meters/SolarRoofFront", "Power");
        var solar2 = GetTs(session, "meters/98D863613CD2", "Power");

        List<Item> items = new();
        foreach (var pr in powers)
        {
            prices.TryGetValue(pr.Key, out var price);
            solar1.TryGetValue(pr.Key, out var pv1); /* modbus kWh meter: negative for solar yield, positive for inverter vampire power*/
            solar2.TryGetValue(pr.Key, out var pv2); /* self reported by inverter: positive for solar yield, no vampire power reported.*/
            items.Add(new Item(pr.Key, pr.Value / 1000, (pr.Value - pv1 + pv2) / 1000, price));
        }

        Console.WriteLine($"Total {items.Count} items ({100.0 * items.Count / prices.Count:0.00}%)");
        var consumption = items.Sum(i => i.usage);
        Print(consumption, "kWh");
        var consumptionWithoutSolar = items.Sum(i => i.usageWithoutSolar);
        Print(consumptionWithoutSolar, "kWh");
        var costWithSolar = items.Sum(i => i.usage * i.cost);
        Print(costWithSolar, "euro");
        var costWithoutSolar = items.Sum(i => i.usageWithoutSolar * i.cost);
        Print(costWithoutSolar, "euro");

        Print(costWithSolar / consumption, "euro/kWh");
        Print(costWithoutSolar / consumptionWithoutSolar, "euro/kWh");


        static void Print(object value, string unit, [CallerArgumentExpression(nameof(value))] string name = default)
        {
            Console.WriteLine($"{name}: {value:0.0000} {unit}");
        }

        static IDictionary<DateTime, double> GetTs(Raven.Client.Documents.Session.IDocumentSession session, string documentId, string tsName, string? collection = default)
        {
            var from = new DateTime(2023, 1, 1).ToUniversalTime();
            var to = from.AddYears(1);
            Console.WriteLine($"Fetching {documentId}");
            var result = session.Query<Meter>(collectionName: collection)
                .Where(c => c.Id == documentId)
                .Select(q => RavenQuery.TimeSeries(q, tsName, from, to).GroupBy(x => x.Hours(1)).Select(x => x.Average()).ToList())
                .Single().Results
                .ToDictionary(x => x.From, x => x.Average[0]);
            Console.WriteLine(result.Count);
            return result;
        }
    }
}

record Item(DateTime timestamp, double usage, double usageWithoutSolar, double cost);
