using Microsoft.Extensions.Logging;
using NModbus;
using NModbus.Serial;
using Raven.Client.Documents;
using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace TelemetryToRaven.Sdm
{
    public class SdmLogger : LoggerService
    {
        public SdmLogger(ILogger<SdmLogger> logger, IDocumentStore database) : base(logger, database)
        {
        }

        protected override async Task DoWork(CancellationToken cancellationToken)
        {
            var port = Environment.GetEnvironmentVariable("SDM_PORT_PATH") ?? "/dev/ttyUSB1";
            _logger.LogInformation($"Using {port}");

            using var session = _store.OpenAsyncSession();

            string documentId = "meters/ElectricityHeatpump";
            var doc = await session.LoadAsync<Meter>(documentId);
            if (doc == null)
            {
                doc = await CreateNewDocument();
            }

            doc.VendorInfo = "Sdm series electricity meter";
            doc.Medium = "Electricity for heat pump";
            await session.StoreAsync(doc, documentId);

            using var master = new ModbusFactory().CreateRtuMaster(
                new SerialPort(port) { BaudRate = 2400, Parity = Parity.None });
            var timestamp = DateTime.UtcNow;
            var registers = await master.ReadInputRegistersAsync(1, 0, 80);

            var appendSerie = (int offset, string name, string tag)
                =>
            {
                float value = BitConverter.Int32BitsToSingle(registers[offset] << 16 | registers[offset + 1]);
                double rounded = Math.Round(value, 3);
                _logger.LogDebug("Got {value} {tag} for {SeriesName}", rounded, tag, name);
                session.TimeSeriesFor(doc, name)
                  .Append(timestamp, rounded, tag);
            };

            appendSerie(0, "Voltage", "V");
            appendSerie(6, "Current", "A");
            appendSerie(12, "Power", "W");
            appendSerie(18, "Apparent Power", "VAr");
            appendSerie(30, "Power factor", null);
            appendSerie(70, "GridFrequency", "Hz");
            appendSerie(72, "Energy", "kWh");
            await session.SaveChangesAsync();
            _logger.LogInformation("Done");
        }

        private async Task<Meter> CreateNewDocument()
        {
            Meter doc = new Meter();
            await _store.TimeSeries.RegisterAsync<Meter>("Voltage", new[] {
                    "Voltage [V]" });
            return doc;
        }
    }
}
