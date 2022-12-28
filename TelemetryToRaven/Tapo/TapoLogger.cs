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
using System.Text.Json.Nodes;

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
            await DiscoverMeters(meters);
            var tasks = meters.Select(meter => RegisterPlug(cancellationToken, meter)).ToList();
            await Task.WhenAll(tasks);
            await session.SaveChangesAsync(cancellationToken);
        }

        private async Task DiscoverMeters(List<TapoDevice> meters)
        {
            var unavailableMeter = meters.Where(m => !m.LastPollSuccessful).FirstOrDefault();
            if (unavailableMeter != null)
            {
                var discoveredMeters = await BroadcastWithPrototype(unavailableMeter);
                foreach (var newmeter in discoveredMeters)
                    foreach (var meter in meters)
                    {
                        if (MacEqual(meter.Mac, newmeter.Mac))
                            meter.IpAddress = newmeter.Ip;
                    }
            }
        }

        private async Task RegisterPlug(CancellationToken cancellationToken, TapoDevice meter)
        {
            using var session = _store.OpenAsyncSession();
            var plug = await GetPlug(cancellationToken, meter);
            if (plug == null)
            {
                _logger.LogWarning($"Plug {meter.Id} {meter.Mac} not found");
            }
            else
            {
                var currentEnergyReading = plug.MonthEnergy / 1000.0;
                await GetOrUpdateEnergyOffset(cancellationToken, meter, currentEnergyReading, session);
                await session.StoreAsync(meter, cancellationToken);
                session.TimeSeriesFor(meter, PowerEnergyTsName).Append(DateTimeOffset.Now.UtcDateTime, new[] {
                    plug.CurrentPower / 1000.0,
                    currentEnergyReading + meter.EnergyOffset,
                    currentEnergyReading
                }, "W;kWh");
            }
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
                return;
            }
            var lastEnergyReading = Math.Round(lastItem.Results[0].Last[1], 3);
            _logger.LogDebug($"Last reading was {lastEnergyReading}, current is {currentEnergyInkWh}");
            if (Math.Round(currentEnergyInkWh, 1) < Math.Round(lastEnergyReading, 1))
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
                if (!MacEqual(meter.Mac, info.Mac))
                {
                    throw new InvalidDataException($"Got MAC {info.Mac}, expected {meter.Mac}");
                }
                meter.LastPollSuccessful = true;

                return info;
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Unexpected response, starting discovery next round");
                meter.LastPollSuccessful = false;
                return null;
            }
        }

        private async Task<IList<TapoUtilResponse>> BroadcastWithPrototype(TapoDevice device)
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

            return results.ToList();
        }

        private bool MacEqual(string mac1, string mac2)
        {
            return mac1.Replace('-', ':').Equals(mac2.Replace('-', ':'), StringComparison.InvariantCultureIgnoreCase);
        }

        private Task<TapoUtilResponse> GetInfo(TapoDevice device)
        {
            return Task.Run(() =>
            {
                var result = RunScript("poll_tapo.py", $"{device.IpAddress} {device.UserName} {device.Password}");
                var parsed = JsonSerializer.Deserialize<JsonObject>(result);
                var response = new TapoUtilResponse(parsed);
                _logger.LogInformation($"Got {response.Model} {response.Nick}" +
                    $" {response.Ip} {response.Mac}");
                return response;
            });
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
                Id = "TestTapoMeter",
                Mac = "AA:BB:12:34:45",
                IpAddress = "192.168.2.4",
                VendorInfo = TapoMeter,
                Medium = "Electricity for fridge"
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
        public bool LastPollSuccessful { get; internal set; }
    }

    public class TapoUtilResponse
    {
        public TapoUtilResponse(JsonNode parsed)
        {
            Model = parsed!["device_info"]!["result"]!["model"]?.GetValue<string>();
            Ip = parsed!["device_info"]!["result"]!["ip"]?.GetValue<string>();
            Mac = parsed!["device_info"]!["result"]!["mac"]?.GetValue<string>();
            NickRaw = parsed!["device_info"]!["result"]!["nickname"]?.GetValue<string>();

            CurrentPower = parsed!["energy_usage"]!["result"]!["current_power"]?.GetValue<double>() ?? double.NaN;
            MonthEnergy = parsed!["energy_usage"]!["result"]!["month_energy"]?.GetValue<double>() ?? double.NaN;
        }

        public readonly string Model;
        public readonly string Ip;
        public readonly string Mac;
        public readonly string NickRaw;
        public string Nick
        {
            get
            {
                byte[] buffer = new byte[1024];
                Base64.DecodeFromUtf8(Encoding.UTF8.GetBytes(NickRaw), buffer, out _, out var length);
                return Encoding.UTF8.GetString(buffer[..length]);
            }
        }
        public readonly double CurrentPower;
        public readonly double MonthEnergy;
    }
}
