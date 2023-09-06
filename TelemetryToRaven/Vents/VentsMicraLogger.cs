using Microsoft.Extensions.Logging;
using NModbus;
using NModbus.Serial;
using Raven.Client.Documents;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NModbus.Device;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;
using System.Net.Sockets;

namespace TelemetryToRaven.Vents
{
    public class VentsMicraLogger : LoggerService
    {
        private const string Ventilator = "Vents Micra 100 series";

        public VentsMicraLogger(ILogger<VentsMicraLogger> logger, IDocumentStore database) : base(logger, database)
        {
        }

        protected override async Task DoWork(CancellationToken cancellationToken)
        {

            using var session = _store.OpenAsyncSession();
            var meters = await GetVentilators(cancellationToken, session);

            foreach (var doc in meters)
            {
                try
                {
                    await ReadMeter(cancellationToken, doc, session);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Failure while reading device {doc.Id}");
                }
            }

            _logger.LogInformation("Done");
        }

        private async Task<List<Ventilator>> GetVentilators(CancellationToken cancellationToken,
            IAsyncDocumentSession session)
        {
            var meters = await session.Query<Ventilator>(collectionName: "Meters")
                .Where(m => m.VendorInfo == Ventilator)
                .ToListAsync(cancellationToken);

            return meters;
        }

        private async Task ReadMeter(CancellationToken cancellationToken, Ventilator doc,
            IAsyncDocumentSession session)
        {
            _logger.LogInformation(
                $"Reading meter: {doc.Id} address: {doc.Hostname} serial: {doc.Serial}");
            var timestamp = DateTime.UtcNow;

            var temperatures = await GetTemperatures(doc);
            session.TimeSeriesFor(doc, nameof(temperatures.ExhaustTemperature)).Append(timestamp, temperatures.ExhaustTemperature);
            session.TimeSeriesFor(doc, nameof(temperatures.OutsideTemperature)).Append(timestamp, temperatures.OutsideTemperature);

            await session.SaveChangesAsync(cancellationToken);
        }



        public async Task<TemperatureReply?> GetTemperatures(Ventilator device)
        {

            var udp = new UdpClient(device.Hostname, 4000);
            var request = Communication.ComposeCommand(device.Serial, device.Password, Communication.Temperatures).ToArray();

            udp.Send(request);
            _logger.LogDebug($"-> {BitConverter.ToString(request)}");
            var timeoutsource = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var result = await udp.ReceiveAsync(timeoutsource.Token);
            if (timeoutsource.IsCancellationRequested)
            {
                _logger.LogWarning("Timeout");
                return null;
            }
            else
            {

                _logger.LogDebug($"<- from [{result.RemoteEndPoint}]");
                _logger.LogDebug($"<- {BitConverter.ToString(result.Buffer)}");
                var temperatures = Reply.ReadFrom<TemperatureReply>(result.Buffer);
                return temperatures;
            }
        }
    }


    public record Ventilator : Meter
    {
        public string Hostname;
        public string Serial;
        public string Password = "1111";
    }
}
