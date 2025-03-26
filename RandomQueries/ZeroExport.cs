using Raven.Client.Documents;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Queries;
using Raven.Client.Documents.Session.TimeSeries;
using System.Runtime.CompilerServices;
using TelemetryToRaven;
using static Raven.Client.Constants;

public class ZeroExport
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Hello " + nameof(ZeroExport));

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


        var powers = GetTs(session, "meters/ISK5\\2M550T-1013", "Power");
        double BatteryCapacity = 2.700; /*kWh*/
        double eff = 0.9;
        double maxPower = 0.800;
        double maxEnergy = maxPower / 60; // datapoint per minute
        List<Item> items = new();
        BatteryState state = new BatteryState(DateTime.MinValue, BatteryCapacity);
        BatteryState lastState = state;
        foreach (var pr in powers)
        {
            if (pr.Key.Date != state.Timestamp)
            {
                Console.WriteLine($"{state.Timestamp}\t{state.Cycles - lastState.Cycles}");
                lastState = state;
                state = state with { Timestamp = pr.Key.Date };
            }
            var gridEnergy = pr.Value / 1000 / 60; /*wattminute to kwatthour*/
            if (gridEnergy > 0) /*try discharge*/
            {
                var discharge = Min(maxEnergy / eff, gridEnergy / eff, state.SoC);
                state = state with
                {
                    totalDischarged = state.totalDischarged + discharge,
                    SoC = state.SoC - discharge
                };
            }
            else /*try charge*/
            {
                var charge = Min(maxEnergy * eff, -gridEnergy * eff, BatteryCapacity - state.SoC);

                state = state with
                {
                    totalCharged = state.totalCharged + charge,
                    SoC = state.SoC + charge
                };
            }
        }

        Print(state.totalCharged, "kWh");
        Print(state.totalDischarged, "kWh");
        Print(state.Cycles, "");

        static void Print(object value, string unit, [CallerArgumentExpression(nameof(value))] string name = default)
        {
            Console.WriteLine($"{name}: {value:0.0000} {unit}");
        }

        static IEnumerable<KeyValuePair<DateTime, double>> GetTs(Raven.Client.Documents.Session.IDocumentSession session, string documentId, string tsName, string? collection = default)
        {
            var from = new DateTime(2024, 4, 1).ToUniversalTime().AddHours(2);
            var to = new DateTime(2024, 6, 1).ToUniversalTime().AddHours(2);
            Console.WriteLine($"Fetching {documentId}");

            var ts = session.TimeSeriesFor(documentId, tsName);
            List<TimeSeriesEntry> groupedByMinute = new();
            var minuteTicks = TimeSpan.FromMinutes(1).Ticks;
            DateTime lastMinute = DateTime.MinValue;
            using (var s = ts.Stream(from, to))
            {
                while (s.MoveNext())
                {
                    var thisMinute = new DateTime(s.Current.Timestamp.Ticks / minuteTicks * minuteTicks, DateTimeKind.Utc);
                    if (thisMinute != lastMinute)
                    {
                        if (groupedByMinute.Count > 0)
                            yield return new KeyValuePair<DateTime, double>(lastMinute, groupedByMinute.Average(x => x.Value));
                        groupedByMinute.Clear();
                        lastMinute = thisMinute;
                    }
                    groupedByMinute.Add(s.Current);
                }
            }
        }

        static double Min(params double[] doubles)
        {
            return doubles.Min();
        }
    }

    public record BatteryState(DateTime Timestamp, double batteryCapacity, double SoC = 0, double totalCharged = 0, double totalDischarged = 0)
    {
        public double Cycles => (totalCharged + totalDischarged) / 2 / batteryCapacity;
    }
}
