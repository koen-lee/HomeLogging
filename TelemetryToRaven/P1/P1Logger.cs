using DSMRParser;
using DSMRParser.Models;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TelemetryToRaven.P1
{
    public class P1Logger : LoggerService
    {
        public P1Logger(ILogger<P1Logger> logger, IDocumentStore database) : base(logger, database)
        {
        }

        protected override async Task DoWork(CancellationToken cancellationToken)
        {
            var port = Environment.GetEnvironmentVariable("P1_PORT_PATH") ?? "/dev/ttyUSB1";
            _logger.LogInformation($"Using {port}");

            var parser = new DSMRTelegramParser();

            using (var serial = new SerialPort(port))
            {
                serial.ReadTimeout = 1500; // more than a second
                serial.BaudRate = 115200;
                serial.Open();
                Telegram telegram = Extract(parser, serial, cancellationToken);
                if (telegram == null) return;
                await PostToRavendb(telegram);
            }
        }

        private Telegram Extract(DSMRTelegramParser parser, SerialPort serial, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var data = new StringBuilder();
                string line;
                // Wait for start
                do
                {
                    line = serial.ReadLine();
                } while (line == null || !line.StartsWith("/"));
                data.AppendLine(line);
                // Wait for end
                while (line != null && !line.StartsWith("!"))
                {
                    line = serial.ReadLine();
                    data.AppendLine(line);
                }
                data.Replace("\0", ""); // there is a hardware bug somewhere
                _logger.LogDebug($"Got telegram of length {data.Length}");
                _logger.LogDebug(data.ToString());

                if (parser.TryParse(data.ToString(), out var telegram))
                    return telegram;
                _logger.LogWarning("Invalid telegram, trying again");
            }
            return null;
        }

        private async Task PostToRavendb(Telegram telegram)
        {
            var session = _store.OpenAsyncSession();
            string documentId = "meters/" + telegram.Identification;
            var doc = await session.LoadAsync<Meter>(documentId);
            if (doc == null)
            {
                doc = new Meter();
                await _store.TimeSeries.RegisterAsync<Meter>("EnergyCounters", new[] {
                    "EnergyDeliveredTariff1",
                    "EnergyDeliveredTariff2",
                    "EnergyReturnedTariff1",
                    "EnergyReturnedTariff2",
                });
                await _store.TimeSeries.RegisterAsync<Meter>("PowerPerPhase", new[] {
                    "Power L1",
                    "Power L2",
                    "Power L3",
                });

                await _store.TimeSeries.RegisterAsync<Meter>("VacPerPhase", new[] {
                    "Voltage L1",
                    "Voltage L2",
                    "Voltage L3",
                }); ;

                await _store.TimeSeries.RegisterAsync<Meter>("IacPerPhase", new[] {
                    "Current L1",
                    "Current L2",
                    "Current L3",
                });
            }
            doc.VendorInfo = "DSMR5";
            doc.Medium = "Electricity";
            await session.StoreAsync(doc, documentId);
            var timestamp = (telegram.TimeStamp ?? DateTimeOffset.UtcNow).UtcDateTime;
            session.TimeSeriesFor(doc, "Power").Append(timestamp, (double)(telegram.PowerDelivered.Value - telegram.PowerReturned.Value), telegram.PowerDelivered.Unit.ToString());
            session.TimeSeriesFor(doc, "PowerPerPhase").Append(timestamp, new[] {
                (double)(telegram.PowerDeliveredL1.Value - telegram.PowerReturnedL1.Value),
                (double)(telegram.PowerDeliveredL2.Value - telegram.PowerReturnedL2.Value),
                (double)(telegram.PowerDeliveredL3.Value - telegram.PowerReturnedL3.Value),
            }, telegram.PowerDeliveredL1.Unit.ToString());
            session.TimeSeriesFor(doc, "VacPerPhase").Append(timestamp, new[] {
                (double)telegram.VoltageL1.Value,
                (double)telegram.VoltageL2.Value,
                (double)telegram.VoltageL3.Value,
            }, telegram.VoltageL1.Unit.ToString());
            session.TimeSeriesFor(doc, "IacPerPhase").Append(timestamp, new[] {
                (double)telegram.CurrentL1.Value,
                (double)telegram.CurrentL2.Value,
                (double)telegram.CurrentL3.Value,
            }, telegram.CurrentL1.Unit.ToString());
            session.TimeSeriesFor(doc, "EnergyCounters").Append(timestamp, new[] {
                (double)telegram.EnergyDeliveredTariff1.Value,
                (double)telegram.EnergyDeliveredTariff2.Value,
                (double)telegram.EnergyReturnedTariff1.Value,
                (double)telegram.EnergyReturnedTariff2.Value,
            }, telegram.EnergyDeliveredTariff1.Unit.ToString());

            await session.SaveChangesAsync();

        }


    }
}
