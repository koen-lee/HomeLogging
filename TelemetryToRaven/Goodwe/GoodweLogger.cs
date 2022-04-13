using Raven.Client.Documents;
using System.Text.Json;

namespace TelemetryToRaven.Goodwe
{
    public class GoodweLogger : LoggerService
    {
        public GoodweLogger(ILogger<GoodweLogger> logger, IDocumentStore database) : base(logger, database)
        {
        }

        protected override async Task DoWork(CancellationToken cancellationToken)
        {
            string host = Environment.GetEnvironmentVariable("GOODWE_HOST_BROADCAST") ?? "192.168.2.255";
            var listenTimeout = TimeSpan.FromMilliseconds(1000);
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

            var response = await poller.QueryInverter(toQuery.ResponseIp, cancellationToken);
            WriteObject(response);
            await PostToRavendb(response, toQuery);
        }

        private async Task PostToRavendb(InverterTelemetry inverterStatus, Inverter inverter)
        {
            var session = _store.OpenAsyncSession();
            string documentId = "meters/" + inverter.Mac;
            var doc = await session.LoadAsync<Meter>(documentId);
            if (doc == null) doc = new Meter();
            doc.VendorInfo = "Goodwe";
            doc.Medium = "SolarPower";
            await session.StoreAsync(doc, documentId);
            session.TimeSeriesFor(doc, "Power").Append(inverterStatus.Timestamp.UtcDateTime, inverterStatus.Power, "W");
            session.TimeSeriesFor(doc, "MPPT1").Append(inverterStatus.Timestamp.UtcDateTime, new[] { inverterStatus.Ipv, inverterStatus.Vpv }, "A,V");
            session.TimeSeriesFor(doc, "Vac").Append(inverterStatus.Timestamp.UtcDateTime, inverterStatus.Vac, "V");
            session.TimeSeriesFor(doc, "GridFrequency").Append(inverterStatus.Timestamp.UtcDateTime, inverterStatus.GridFrequency, "V");
            session.TimeSeriesFor(doc, "InternalTemperature").Append(inverterStatus.Timestamp.UtcDateTime, inverterStatus.Temperature, "°C");
            session.TimeSeriesFor(doc, "EnergyLifetime").Append(inverterStatus.Timestamp.UtcDateTime, inverterStatus.EnergyLifetime, "kWh");
            session.TimeSeriesFor(doc, "EnergyToday").Append(inverterStatus.Timestamp.UtcDateTime, inverterStatus.EnergyToday, "kWh");

            await session.SaveChangesAsync();

        }


        private void WriteObject(object toWrite)
        {
            var serialized = JsonSerializer.Serialize(toWrite, new JsonSerializerOptions { WriteIndented = true });
            _logger.LogDebug(serialized);
        }
    }
}
