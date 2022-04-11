using Raven.Client.Documents;
using System.Xml.Serialization;

namespace TelemetryToRaven.Mbus
{
    public class MbusLogger : LoggerService
    {
        public MbusLogger(ILogger<MbusLogger> logger, IDocumentStore database) : base(logger, database)
        {
        }

        protected override async Task DoWork(CancellationToken cancellationToken)
        {
            var parsed = Deserialize(RunScript("mbus.sh"));

            foreach (var item in parsed.Records)
            {
                Console.WriteLine($"{item.Id}\t {item.Unit} {item.Value}");
            }

            var records = parsed.Records.ToDictionary(x => x.Id);

            using var session = store.OpenAsyncSession();
            string documentId = "meters/" + parsed.SlaveInformation.Id;
            var doc = await session.LoadAsync<Meter>(documentId);
            if (doc == null) doc = new Meter();
            doc.VendorInfo = parsed.SlaveInformation.Manufacturer;
            doc.Medium = parsed.SlaveInformation.Medium;
            await session.StoreAsync(doc, documentId);

            var appendSerie = (MBusData.DataRecord record, string name, string tag, double factor)
                =>
            {
                session.TimeSeriesFor(doc, name)
                  .Append(record.Timestamp.UtcDateTime, record.NumericValue * factor, tag);
            };

            appendSerie(records[1], "HeatEnergy", "kWh", 1);
            appendSerie(records[9], "FlowTemperature", "°C", 0.01);
            appendSerie(records[10], "ReturnTemperature", "°C", 0.01);
            appendSerie(records[13], "VolumeFlow", "m³/h", 1);
            appendSerie(records[12], "Power", "W", 100);

            await session.SaveChangesAsync();
            _logger.LogInformation("Done");
        }

        private static MBusData Deserialize(string stream)
        {
            var serializer = new XmlSerializer(typeof(MBusData), "");
            return (MBusData)serializer.Deserialize(new StringReader(stream));
        }
    }
}
