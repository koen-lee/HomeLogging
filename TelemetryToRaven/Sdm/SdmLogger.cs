using Raven.Client.Documents;
using System.Xml.Serialization;
using TelemetryToRaven.Mbus;

namespace TelemetryToRaven.Sdm
{
    public class SdmLogger : LoggerService
    {
        public SdmLogger(ILogger<SdmLogger> logger, IDocumentStore database) : base(logger, database)
        {
        }

        protected override async Task DoWork(CancellationToken cancellationToken)
        {
            var port = Environment.GetEnvironmentVariable("SDM_PORT_PATH") ?? "/dev/ttyUSB0";
            _logger.LogInformation($"Using {port}");
            
            using var session = _store.OpenAsyncSession();
            /*
            string documentId = "meters/" + parsed.SlaveInformation.Id;
            var doc = await session.LoadAsync<Meter>(documentId);
            if (doc == null) doc = new Meter();
            doc.VendorInfo = "Sdm series electricity meter";
            doc.Medium = "Electricity for heat pump";
            await session.StoreAsync(doc, documentId);


            var appendSerie = (double value, string name, string tag, double factor)
                =>
            {
                session.TimeSeriesFor(doc, name)
                  .Append(record.Timestamp.UtcDateTime, value * factor, tag);
            };

            appendSerie(records[1], "HeatEnergy", "kWh", 1);
            appendSerie(records[9], "FlowTemperature", "°C", 0.01);
            appendSerie(records[10], "ReturnTemperature", "°C", 0.01);
            appendSerie(records[13], "VolumeFlow", "m³/h", 1);
            appendSerie(records[12], "Power", "W", 100);
            */
            await session.SaveChangesAsync();
            _logger.LogInformation("Done");
        }
    }
}
