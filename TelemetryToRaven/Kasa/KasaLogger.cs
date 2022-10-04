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

namespace TelemetryToRaven.Kasa
{
    public class KasaLogger : LoggerService
    {
        public KasaLogger(ILogger<KasaLogger> logger, IDocumentStore database) : base(logger, database)
        {
        }

        protected override async Task DoWork(CancellationToken cancellationToken)
        {
            using var session = _store.OpenAsyncSession();
            var meters = await GetKasaMeters(cancellationToken, session);
            var tasks = meters.Select(meter => RegisterPlug(cancellationToken, meter)).ToList();
            await Task.WhenAll(tasks);
        }

        private async Task RegisterPlug(CancellationToken cancellationToken, KasaDevice meter)
        {
            using var session = _store.OpenAsyncSession();
            var plug = await GetPlug(cancellationToken, meter);
            await session.StoreAsync(meter, cancellationToken);
            var response = await plug.GetPowerReading(cancellationToken);
            session.TimeSeriesFor(meter, "PowerEnergy").Append(DateTimeOffset.Now.UtcDateTime,
                new[] { response.CurrentPowerInW, response.CumulativeEnergyInkWh }, "W;kWh");
            await session.SaveChangesAsync(cancellationToken);
        }

        private async Task<HS110Device> GetPlug(CancellationToken cancellationToken, KasaDevice meter)
        {
            var plug = new HS110Device(meter.IpAddress);
            try
            {
                var info = await plug.GetInfoReading(cancellationToken);
                if (info.Mac != meter.Mac)
                {
                    throw new InvalidDataException($"Got MAC {info.Mac}, expected {meter.Mac}");
                }
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Unexpected response, starting discovery");
                meter.IpAddress = await BroadcastAndGetByMac(meter.Id, meter.IpAddress);
                plug = new HS110Device(meter.IpAddress);
            }
            return plug;
        }

        private async Task<string> BroadcastAndGetByMac(string mac, string host)
        {
            var ipMatch = Regex.Match(host, "(.+[.])\\d+");
            var pings = new List<Task<(string host, string mac)>>();
            if (ipMatch.Success)
            {
                var subnet = ipMatch.Groups[1].Value;
                for (var i = 2; i < 255; i++)
                {
                    pings.Add(GetInfo(subnet + i, TimeSpan.FromSeconds(1)));
                }
            }
            await Task.WhenAll(pings);

            var results = from ping in pings
                          where ping.IsCompletedSuccessfully
                          select ping.Result;

            return results.Single(r =>
                    mac.Equals(r.mac, StringComparison.InvariantCultureIgnoreCase))
                .host;
        }


        private async Task<(string host, string mac)> GetInfo(string host, TimeSpan timeout)
        {
            try
            {
                var device = new HS110Device(host);
                var cancelOnTimeout = new CancellationTokenSource(timeout);
                var info = await device.GetInfoReading(cancelOnTimeout.Token);
                _logger.LogInformation($"Host {host} : {info}");
                return (host, info.Mac);
            }
            catch (Exception)
            {
                return (host, null);
            }
        }

        private async Task<List<KasaDevice>> GetKasaMeters(CancellationToken cancellationToken,
            IAsyncDocumentSession session)
        {
            var meters = await session.Query<KasaDevice>(collectionName: "Meters")
                .Where(m => m.VendorInfo == KasaMeter)
                .ToListAsync(cancellationToken);

            if (!meters.Any())
            {
                var doc = await CreateNewDocument();
                await session.StoreAsync(doc, cancellationToken);
                await session.SaveChangesAsync(cancellationToken);
                meters.Add(doc);
            }

            return meters;
        }

        private async Task<KasaDevice> CreateNewDocument()
        {
            var doc = new KasaDevice
            {
                Id = "TestKasaMeter",
                Mac = "AA:BB:12:34:45",
                IpAddress = "192.168.2.4",
                VendorInfo = KasaMeter,
                Medium = "Electricity for heat pump water heater"
            };

            return doc;
        }

        private const string KasaMeter = "TP Link Kasa HS110 plug";
    }

    public class KasaDevice : Meter
    {
        public string IpAddress { get; set; }
        public string Mac { get; set; }
    }
}
