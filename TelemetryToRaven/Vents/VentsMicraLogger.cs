using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

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
            var timestamp = DateTime.UtcNow.TruncateToSeconds();

            var device = new Device(doc.Hostname, doc.Serial, doc.Id);

            var items = await device.ReadAddresses(
                    ItemAddress.TemperatureOutsideIntake,
                    ItemAddress.TemperatureOutsideExhaust,

                    ItemAddress.OnOff,
                    ItemAddress.SpeedMode,
                    ItemAddress.Boost,
                    ItemAddress.Timer,
                    ItemAddress.TimerSpeed,
                    ItemAddress.WeeklyScheduleEnabled,
                    ItemAddress.WeeklyScheduleSpeed,

                    ItemAddress.SupplySpeed1,
                    ItemAddress.ExtractSpeed1,
                    ItemAddress.SupplySpeed2,
                    ItemAddress.ExtractSpeed2,
                    ItemAddress.SupplySpeed3,
                    ItemAddress.ExtractSpeed3,
                    ItemAddress.SupplySpeed4,
                    ItemAddress.ExtractSpeed4,
                    ItemAddress.SupplySpeed5,
                    ItemAddress.ExtractSpeed5,
                    ItemAddress.SupplySpeedBoost,
                    ItemAddress.ExtractSpeedBoost
            );

            session.TimeSeriesFor(doc, "ExhaustTemperature").Append(timestamp, items.Temperature(ItemAddress.TemperatureOutsideExhaust));
            session.TimeSeriesFor(doc, "OutsideTemperature").Append(timestamp, items.Temperature(ItemAddress.TemperatureOutsideIntake));

            GetSpeed(items, out var speed, out var speedtag);
            session.TimeSeriesFor(doc, "Speed").Append(timestamp, speed, speedtag);
            var speeds = GetSpeedPercentage(items, speed);

            session.TimeSeriesFor(doc, "FanSpeedPercentages").Append(timestamp, new[] { speeds.supplySpeed, speeds.extractSpeed }, "supply;extract");
            await session.SaveChangesAsync(cancellationToken);
        }

        private static void GetSpeed(Dictionary<ItemAddress, byte[]> items, out int speed, out string speedtag)
        {
            speed = 0;
            speedtag = "off";
            if (items.Bool(ItemAddress.OnOff))
            {
                speed = items.Byte(ItemAddress.SpeedMode);
                speedtag = "on";
                if (items.Bool(ItemAddress.WeeklyScheduleEnabled))
                {
                    speed = items.Byte(ItemAddress.WeeklyScheduleSpeed);
                    speedtag = "schedule";
                }
                if (items.Bool(ItemAddress.Timer))
                {
                    speed = items.Byte(ItemAddress.TimerSpeed);
                    speedtag = "timer";
                }
                if (items.Bool(ItemAddress.Boost))
                {
                    speed = 6;
                    speedtag = "boost";
                }
            }
        }

        private static (double supplySpeed, double extractSpeed) GetSpeedPercentage(Dictionary<ItemAddress, byte[]> items, int speed)
        {
            var supplySpeed = 0.0;
            var extractSpeed = 0.0;

            switch (speed)
            {
                case 0:
                    break;
                case 1:
                    supplySpeed = items.Byte(ItemAddress.SupplySpeed1);
                    extractSpeed = items.Byte(ItemAddress.ExtractSpeed1);
                    break;
                case 2:
                    supplySpeed = items.Byte(ItemAddress.SupplySpeed2);
                    extractSpeed = items.Byte(ItemAddress.ExtractSpeed2);
                    break;
                case 3:
                    supplySpeed = items.Byte(ItemAddress.SupplySpeed3);
                    extractSpeed = items.Byte(ItemAddress.ExtractSpeed3);
                    break;
                case 4:
                    supplySpeed = items.Byte(ItemAddress.SupplySpeed4);
                    extractSpeed = items.Byte(ItemAddress.ExtractSpeed4);
                    break;
                case 5:
                    supplySpeed = items.Byte(ItemAddress.SupplySpeed5);
                    extractSpeed = items.Byte(ItemAddress.ExtractSpeed5);
                    break;
                case 6:
                    supplySpeed = items.Byte(ItemAddress.SupplySpeedBoost);
                    extractSpeed = items.Byte(ItemAddress.ExtractSpeedBoost);
                    break;
                default:
                    throw new NotImplementedException();
            }
            return (supplySpeed, extractSpeed);
        }


    }


    public record Ventilator : Meter
    {
        public string Hostname;
        public string Serial;
        public string Password = "1111";
    }
}
