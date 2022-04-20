using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using TelemetryToRaven.Sdm;

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
                _logger.LogDebug($"{item.Id}\t {item.Unit} {item.Value}");
            }

            var records = parsed.Records.ToDictionary(x => x.Id);

            using var session = _store.OpenAsyncSession();
            string documentId = "meters/" + parsed.SlaveInformation.Id;
            var doc = await session.LoadAsync<Meter>(documentId);
            if (doc == null)
            {
                doc = new Meter();

                await _store.TimeSeries.RegisterAsync<Meter>("HeatEnergy", new[] { "HeatEnergy [kWh]" });
                await _store.TimeSeries.RegisterAsync<Meter>("FlowTemperature", new[] { "Flow temperature [°C]" });
                await _store.TimeSeries.RegisterAsync<Meter>("ReturnTemperature", new[] { "Return temperature [°C]" });
                await _store.TimeSeries.RegisterAsync<Meter>("VolumeFlow", new[] { "Volume flow [m³/h]" });
                await _store.TimeSeries.RegisterAsync<Meter>("Power", new[] { "Power [W]" });
                await _store.TimeSeries.RegisterAsync<Meter>("CalculatedPower", new[] { "Calculated Power [W]", "Temperature difference [K]" });
            }
            doc.VendorInfo = parsed.SlaveInformation.Manufacturer;
            doc.Medium = parsed.SlaveInformation.Medium;
            await session.StoreAsync(doc, documentId);

            var appendSerie = (MBusData.DataRecord record, string name, string tag, double factor)
                =>
            {
                session.TimeSeriesFor(doc, name)
                  .Append(record.Timestamp.UtcDateTime, record.NumericValue * factor, tag);
            };

            var returntemperature = records[10];
            var flowtemperature = records[9];
            var volumeflow = records[13];
            appendSerie(records[1], "HeatEnergy", "kWh", 1);
            appendSerie(flowtemperature, "FlowTemperature", "°C", 0.01);
            appendSerie(returntemperature, "ReturnTemperature", "°C", 0.01);
            appendSerie(volumeflow, "VolumeFlow", "m³/h", 1);
            appendSerie(records[12], "Power", "W", 100);


            // Q = Cw * dT * flow * time
            var dT = records[11].NumericValue * 0.01;
            var power = 4186 * dT * (volumeflow.NumericValue / 3600 /* m³/h -> kg/s */);
            session.TimeSeriesFor(doc, "CalculatedPower")
                 .Append(volumeflow.Timestamp.UtcDateTime, new[] { Math.Round(power, 0), dT }, "W;K");
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
