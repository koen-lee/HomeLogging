using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Raven.Client.Documents.Session;
using Raven.Client.Documents.Session.TimeSeries;
using static System.Collections.Specialized.BitVector32;

namespace TelemetryToRaven.Mbus
{
    public class MbusLogger : LoggerService
    {
        private double _interpolatedEnergy;
        private EnergyReading _latestReading;

        public MbusLogger(ILogger<MbusLogger> logger, IDocumentStore database) : base(logger, database)
        {
        }

        private void LoadLastValues(IAsyncDocumentSession session, string documentId)
        {
            bool GetLast(string timeseries, out TimeSeriesEntry value)
            {
                var entries = session.TimeSeriesFor(documentId, timeseries).GetAsync(DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(10))).Result;
                value = entries.LastOrDefault();
                if (value != null)
                { _logger.LogDebug($"Using {timeseries} {value}"); }
                else
                { _logger.LogWarning($"Missing {timeseries}"); }
                return value == null;
            }

            if (GetLast("Power", out var power)
                && GetLast("HeatEnergy", out var energy))
            {
                _latestReading = new EnergyReading { Energy = energy.Value, Power = power.Value, Timestamp = energy.Timestamp };
                _interpolatedEnergy = energy.Value - Math.Round(energy.Value);
            }
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
            appendSerie(records[1], "HeatEnergyRaw", "kWh", 1);
            appendSerie(flowtemperature, "FlowTemperature", "°C", 0.01);
            appendSerie(returntemperature, "ReturnTemperature", "°C", 0.01);
            appendSerie(volumeflow, "VolumeFlow", "m³/h", 1);
            appendSerie(records[12], "Power", "W", 100);

            // Q = Cw * dT * flow * time
            var dT = (flowtemperature.NumericValue - returntemperature.NumericValue) * 0.01;
            var power = 4186 * dT * (volumeflow.NumericValue / 3600 /* m³/h -> kg/s */);
            session.TimeSeriesFor(doc, "CalculatedPower")
                 .Append(volumeflow.Timestamp.UtcDateTime, new[] { Math.Round(power, 0), dT }, "W;K");

            var newReading = new EnergyReading { Energy = records[1].NumericValue, Power = power, Timestamp = records[1].Timestamp };

            if (_latestReading == null)
                LoadLastValues(session, documentId);
            InterpolateEnergy(newReading);
            _logger.LogDebug($"HeatEnergy: {newReading.Energy}");
            session.TimeSeriesFor(doc, "HeatEnergy")
                 .Append(newReading.Timestamp.UtcDateTime, Math.Round(newReading.Energy, 3), "kWh");

            await session.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Done");
        }

        private static MBusData Deserialize(string stream)
        {
            var serializer = new XmlSerializer(typeof(MBusData), "");
            return (MBusData)serializer.Deserialize(new StringReader(stream));
        }

        /// <summary>
        /// The Zenner meter only register whole kWh. This is OKish for our purposes, but it would be nice to have more resolution.
        /// Try to interpolate the fractional energy by keeping track of a Riemann sum of the power.
        /// newreading is updated.
        /// </summary>
        /// <param name="newreading"></param>
        private void InterpolateEnergy(EnergyReading newreading)
        {
            if (_latestReading == null || _latestReading.Timestamp >= newreading.Timestamp)
            {
                _logger.LogInformation($"Not interpolating {_latestReading?.Timestamp}");
            }
            else if (newreading.Energy > _latestReading.Energy) // kWh counter updated, reset fraction
            {

                _logger.LogInformation($"Not interpolating; counter rollover");
                _interpolatedEnergy = 0;
            }
            else
            {
                // Assume average power over time, result in Wh
                double delta = ((newreading.Power + _latestReading.Power) / 2) *
                               (newreading.Timestamp - _latestReading.Timestamp).TotalHours;
                if (delta > 0) // monotonic counter, bidirectional measurements (defrosts?) mess up the logic
                    _interpolatedEnergy += delta / 1000.0; // Wh -> kWh
            }

            if (_interpolatedEnergy > 0.99) // the fraction must be < 1
                _interpolatedEnergy = 0.99;
            _latestReading = newreading;

            _logger.LogDebug($"Fractional part {_interpolatedEnergy}");
            newreading.Energy += _interpolatedEnergy;
        }

        public class EnergyReading
        {
            public double Energy { get; set; }
            public double Power { get; init; }
            public DateTimeOffset Timestamp { get; init; }
        }
    }
}
