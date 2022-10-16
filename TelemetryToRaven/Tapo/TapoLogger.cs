using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Raven.Client.Documents.Session;
using System.Buffers.Text;
using System.Text;
using Raven.Client.Documents.Queries;
using System.Text.Json;

namespace TelemetryToRaven.Tapo
{
    public class TapoLogger : LoggerService
    {
        const string PowerEnergyTsName = "PowerEnergy";

        public TapoLogger(ILogger<TapoLogger> logger, IDocumentStore database) : base(logger, database)
        {
        }

        protected override async Task DoWork(CancellationToken cancellationToken)
        {
            using var session = _store.OpenAsyncSession();
            var meters = await GetTapoMeters(cancellationToken, session);
            var tasks = meters.Select(meter => RegisterPlug(cancellationToken, meter)).ToList();
            await Task.WhenAll(tasks);
        }

        private async Task RegisterPlug(CancellationToken cancellationToken, TapoDevice meter)
        {
            using var session = _store.OpenAsyncSession();
            var plug = await GetPlug(cancellationToken, meter);
            if (plug == null)
            {
                _logger.LogWarning($"Plug {meter.Id} {meter.Mac} not found");
                return;
            }

            var currentEnergyReading = plug.energy_usage.month_energy / 1000.0;
            await GetOrUpdateEnergyOffset(cancellationToken, meter, currentEnergyReading, session);
            await session.StoreAsync(meter, cancellationToken);
            session.TimeSeriesFor(meter, PowerEnergyTsName).Append(DateTimeOffset.Now.UtcDateTime,
            new[] { plug.energy_usage.current_power, plug.energy_usage.month_energy + meter.EnergyOffset, plug.energy_usage.month_energy }, "W;kWh");
            await session.SaveChangesAsync(cancellationToken);
        }
        private async Task GetOrUpdateEnergyOffset(CancellationToken cancellationToken, TapoDevice meter,
        double currentEnergyInkWh,
            IAsyncDocumentSession session)
        {
            var lastItem = await session.Query<Meter>().Where(m => m.Id == meter.Id)
                .Select(c => RavenQuery.TimeSeries(c, PowerEnergyTsName)
                    .Select(ts => ts.Last()).ToList())
                .SingleOrDefaultAsync(token: cancellationToken);
            if (lastItem == default || lastItem.Results.Length == 0)
            {
                _logger.LogDebug("No historical data");
            }
            var lastEnergyReading = Math.Round(lastItem.Results[0].Last[1], 3);
            _logger.LogDebug($"Last reading was {lastEnergyReading}, current is {currentEnergyInkWh}");
            if (Math.Round(currentEnergyInkWh, 3) < lastEnergyReading)
            {
                _logger.LogInformation($"New offset: {lastEnergyReading}");
                meter.EnergyOffset = lastEnergyReading;
            }
        }

        private async Task<TapoUtilResponse> GetPlug(CancellationToken cancellationToken, TapoDevice meter)
        {
            try
            {
                var info = await GetInfo(meter);
                if (!MacEqual(meter.Mac, info.device_info.Mac))
                {
                    throw new InvalidDataException($"Got MAC {info.device_info.Mac}, expected {meter.Mac}");
                }
                return info;
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Unexpected response, starting discovery");
                var foundResponse = await BroadcastAndGetByMac(meter);
                if (foundResponse == null)
                    return null;
                meter.IpAddress = foundResponse.device_info.Ip;
                return foundResponse;
            }
        }

        private async Task<TapoUtilResponse> BroadcastAndGetByMac(TapoDevice device)
        {
            // Tapo has Windows style mac, hyphen separated
            var mac = device.Mac.Replace(":", "-").ToUpper();
            var ipMatch = Regex.Match(device.IpAddress, "(.+[.])\\d+");
            var pings = new List<Task<TapoUtilResponse>>();
            if (ipMatch.Success)
            {
                var subnet = ipMatch.Groups[1].Value;
                for (var i = 2; i < 255; i++)
                {
                    var newIp = subnet + i;
                    var candidateDevice = device with { IpAddress = newIp };
                    pings.Add(GetInfo(candidateDevice));
                }
            }
            await Task.WhenAll(pings);

            var results = from ping in pings
                          where ping.IsCompletedSuccessfully
                          select ping.Result;

            return results.SingleOrDefault(r => MacEqual(mac, r.device_info.Mac));
        }

        private bool MacEqual(string mac1, string mac2)
        {
            return mac1.Replace('-', ':').Equals(mac2.Replace('-', ':'), StringComparison.InvariantCultureIgnoreCase);
        }

        private async Task<TapoUtilResponse> GetInfo(TapoDevice device)
        {
            var result = RunScript("poll_tapo.py", $"{device.IpAddress} {device.UserName} {device.Password}");
            return JsonSerializer.Deserialize<TapoUtilResponse>(result);
        }

        private async Task<List<TapoDevice>> GetTapoMeters(CancellationToken cancellationToken,
            IAsyncDocumentSession session)
        {
            var meters = await session.Query<TapoDevice>(collectionName: "Meters")
                .Where(m => m.VendorInfo == TapoMeter)
                .ToListAsync(cancellationToken);

            if (!meters.Any())
            {
                var doc = CreateNewDocument();
                await session.StoreAsync(doc, cancellationToken);
                await session.SaveChangesAsync(cancellationToken);
                meters.Add(doc);
            }

            return meters;
        }

        private TapoDevice CreateNewDocument()
        {
            var doc = new TapoDevice
            {
                Id = "TestKasaMeter",
                Mac = "AA:BB:12:34:45",
                IpAddress = "192.168.2.4",
                VendorInfo = TapoMeter,
                Medium = "Electricity for heat pump water heater"
            };

            return doc;
        }

        private const string TapoMeter = "TP Link Tapo P115 Plug";
    }

    public record TapoDevice : Meter
    {
        public string IpAddress { get; set; }
        public string Mac { get; set; }
        public double EnergyOffset { get; internal set; }
        public object UserName { get; internal set; }
        public object Password { get; internal set; }
    }

    public class TapoUtilResponse
    {
        public DeviceInfo device_info;
        public EnergyUsage energy_usage;

        public class DeviceInfo
        {
            public string Model;
            public string Ip;
            public string Mac;
            public string NickName;
            public string NickDecoded
            {
                get
                {
                    byte[] buffer = new byte[1024];
                    Base64.DecodeFromUtf8(Encoding.UTF8.GetBytes(NickName), buffer, out _, out var length);
                    return Encoding.UTF8.GetString(buffer[..length]);
                }
            }
        }
        public class EnergyUsage
        {
            public double current_power;
            public double month_energy;
        }
    }
}
