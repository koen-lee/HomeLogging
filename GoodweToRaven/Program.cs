using Raven.Client.Documents;
using System.Text.Json;

namespace GoodweToRaven
{
    class Program
    {
        public static async Task Main(
            string host = "192.168.2.255",
            int timeout = 1000,
            string serverurl = "http://energylogger:8080", string database = "Eiland17Logging")
        {
            var listenTimeout = TimeSpan.FromMilliseconds(timeout);
            var poller = new GoodwePoller(listenTimeout);
            Inverter toQuery = null;
            await foreach (var foundInverter in poller.DiscoverInvertersAsync(host))
            {
                if (foundInverter.Ssid == null /*== not a Goodwe inverter*/)
                    continue;

                WriteObject(foundInverter);
                toQuery = foundInverter;
            }

            if (toQuery == null)
                throw new ArgumentException("No inverter discovered, nothing to do. Either make sure your router doesn't block broadcasts or discover the IP with, for example, the Goodwe app");

            var response = await poller.QueryInverter(toQuery.ResponseIp);
            WriteObject(response);
            await PostToRavendb(response, toQuery, serverurl, database);
        }

        private static async Task PostToRavendb(InverterTelemetry inverterStatus, Inverter inverter, string serverUrl, string database)
        {
            Console.WriteLine("Opening store");
            var store = new DocumentStore()
            {
                Database = database,
                Urls = new[] { serverUrl }
            };
            store.Initialize();
            var session = store.OpenSession();
            string documentId = "meters/" + inverter.Mac;
            var doc = session.Load<Meter>(documentId);
            if (doc == null) doc = new Meter();
            doc.VendorInfo = "Goodwe";
            doc.Medium = "SolarPower";
            session.Store(doc, documentId);
            session.TimeSeriesFor(doc, "Power").Append(inverterStatus.Timestamp.UtcDateTime, inverterStatus.Power, "W");
            session.TimeSeriesFor(doc, "MPPT1").Append(inverterStatus.Timestamp.UtcDateTime, new[] { inverterStatus.Ipv, inverterStatus.Vpv }, "A,V");
            session.TimeSeriesFor(doc, "Vac").Append(inverterStatus.Timestamp.UtcDateTime, inverterStatus.Vac, "V");
            session.TimeSeriesFor(doc, "GridFrequency").Append(inverterStatus.Timestamp.UtcDateTime, inverterStatus.GridFrequency, "V");
            session.TimeSeriesFor(doc, "InternalTemperature").Append(inverterStatus.Timestamp.UtcDateTime, inverterStatus.Temperature, "°C");
            session.TimeSeriesFor(doc, "EnergyLifetime").Append(inverterStatus.Timestamp.UtcDateTime, inverterStatus.EnergyLifetime, "kWh");
            session.TimeSeriesFor(doc, "EnergyToday").Append(inverterStatus.Timestamp.UtcDateTime, inverterStatus.EnergyToday, "kWh");

            session.SaveChanges();
        }

        private static void WriteObject(object toWrite)
        {
            var serialized = JsonSerializer.Serialize(toWrite, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(serialized);
        }
    }
}
