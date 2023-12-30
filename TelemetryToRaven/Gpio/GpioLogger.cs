using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Queries;
using Raven.Client.Documents.Session;
using System;
using System.Collections.Generic;
using System.Device.Gpio;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace TelemetryToRaven.Gpio
{
    public class GpioLogger : LoggerService
    {
        public string GpioMeter = "Gpio Pulse counter";
        private CancellationToken _stoppingToken;

        public GpioLogger(ILogger<GpioLogger> logger, IDocumentStore database) : base(logger, database)
        {
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _stoppingToken = stoppingToken;
            return base.ExecuteAsync(stoppingToken);
        }
        protected override async Task DoWork(CancellationToken cancellationToken)
        {
            using var session = _store.OpenAsyncSession();
            var meters = await GetGpioMeters(_stoppingToken, session);
            var tasks = meters.Select(m => ReadGpio(m, session, _stoppingToken));
            await Task.WhenAll(tasks);
        }

        private async Task ReadGpio(GpioMeter m, IAsyncDocumentSession session, CancellationToken cancellationToken)
        {
            var pin = m.GpioPin;
            using var controller = new GpioController();
            var controlPin = controller.OpenPin(pin, PinMode.Input);
            _logger.LogInformation($" Pin {pin} is {controlPin.GetPinMode()} value {controlPin.Read()} ");
            WaitForEventResult result;
            var stopwatch = Stopwatch.StartNew();
            TimeSpan lowTime = TimeSpan.FromMinutes(1);
            var debounce = TimeSpan.FromMilliseconds(200);
            while (true)
            {
                result = await controller.WaitForEventAsync(pin, PinEventTypes.Rising, cancellationToken).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Done");
                    return;
                }
                _logger.LogInformation($"{m.TimeseriesName} pin {m.GpioPin} Rise after {stopwatch.Elapsed}");
                if (stopwatch.Elapsed > debounce && lowTime > debounce)
                    await IncrementTimeseriesAsync(m, session);
                else
                    _logger.LogWarning("Bounce detected?");
                stopwatch.Restart();
                await controller.WaitForEventAsync(pin, PinEventTypes.Falling, cancellationToken).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Done");
                    return;
                }
                _logger.LogInformation($"{m.TimeseriesName} pin {m.GpioPin} Fall after {stopwatch.Elapsed}");
                lowTime = stopwatch.Elapsed;
                stopwatch.Restart();
            }
        }

        private async Task IncrementTimeseriesAsync(GpioMeter meter, IAsyncDocumentSession session)
        {
            var timestamp = DateTime.UtcNow.TruncateTo(TimeSpan.FromMilliseconds(10));
            var series = session.TimeSeriesFor(meter.Id, meter.TimeseriesName);
            var lastValues = await session.Query<Meter>()
                .Where(c => c.Id == meter.Id)
                .Select(q => RavenQuery.TimeSeries(q, meter.TimeseriesName)
                    .Select(x => x.Count()).ToList()
                ).ToListAsync();

            var count = (int)lastValues.Single().Results.Single().Count[0];
            if (count > 0)
            {
                var rawLast = await series.GetAsync(start: count - 1);
                var last = rawLast.Last();
                _logger.LogInformation($"Counted {last.Values[0]} at {last.Timestamp} for {meter.Id} so far");
                var rate = meter.QuantityPerPulse / (timestamp - last.Timestamp).TotalSeconds;
                series.Append(timestamp.AddMilliseconds(-10), new[] { last.Values[0], rate });
                series.Append(timestamp, new[] { last.Values[0] + meter.QuantityPerPulse, rate });
            }
            else
            {
                series.Append(timestamp, new[] { meter.QuantityPerPulse, 0 });
            }
            await session.SaveChangesAsync();
        }

        private async Task<List<GpioMeter>> GetGpioMeters(CancellationToken cancellationToken,
           IAsyncDocumentSession session)
        {
            var meters = await session.Query<GpioMeter>(collectionName: "Meters")
                .Where(m => m.VendorInfo == GpioMeter)
                .ToListAsync(cancellationToken);

            if (!meters.Any())
            {
                var doc = CreateNewDocument();
                await session.StoreAsync(doc, cancellationToken);
                meters.Add(doc);
            }

            return meters;
        }


        private GpioMeter CreateNewDocument()
        {
            var doc = new GpioMeter
            {
                Id = "WaterMeter",
                VendorInfo = GpioMeter,
                Medium = "Domestic cold water",
                TimeseriesName = "Water",
                GpioPin = 184,
                QuantityPerPulse = 1.0,
                Unit = "Liter"
            };

            return doc;
        }
    }
}
