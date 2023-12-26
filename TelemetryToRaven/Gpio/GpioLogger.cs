﻿using AngleSharp;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Queries.TimeSeries;
using Raven.Client.Documents.Queries;
using Raven.Client.Documents.Session;
using System;
using System.Collections.Generic;
using System.Device.Gpio;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using TelemetryToRaven.Sdm;

namespace TelemetryToRaven.Gpio
{
    public class GpioLogger : LoggerService
    {
        public string GpioMeter = "Gpio Pulse counter";
        public GpioLogger(ILogger<GpioLogger> logger, IDocumentStore database) : base(logger, database)
        {
        }

        protected override async Task DoWork(CancellationToken cancellationToken)
        {
            using var session = _store.OpenAsyncSession();
            var meters = await GetGpioMeters(cancellationToken, session);
            var tasks = meters.Select(m => ReadGpio(m, session, cancellationToken));
            await Task.WhenAll(tasks);
        }

        private async Task ReadGpio(GpioMeter m, IAsyncDocumentSession session, CancellationToken cancellationToken)
        {
            var pin = m.GpioPin;
            using var controller = new GpioController();
            var controlPin = controller.OpenPin(pin, PinMode.Input);
            Console.WriteLine($" Pin {pin} is {controlPin.GetPinMode()} value {controlPin.Read()} ");
            WaitForEventResult result;
            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                result = await controller.WaitForEventAsync(pin, PinEventTypes.Rising, cancellationToken).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested)
                    return;
                _logger.LogInformation($"{m.TimeseriesName} pin {m.GpioPin} Rise after {stopwatch.Elapsed}");
                stopwatch.Restart();
                await IncrementTimeseriesAsync(m, session);
                result = await controller.WaitForEventAsync(pin, PinEventTypes.Falling, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation($"{m.TimeseriesName} pin {m.GpioPin} Fall after {stopwatch.Elapsed}");
                stopwatch.Restart();
            }
        }

        private async Task IncrementTimeseriesAsync(GpioMeter meter, IAsyncDocumentSession session)
        {
            var timestamp = DateTime.UtcNow.TruncateTo(TimeSpan.FromMilliseconds(10));
            var series = session.IncrementalTimeSeriesFor(meter.Id, "inc:" + meter.TimeseriesName);
            series.Increment(timestamp, meter.QuantityPerPulse);
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
                var doc = await CreateNewDocument();
                await session.StoreAsync(doc, cancellationToken);
                meters.Add(doc);
            }

            return meters;
        }


        private async Task<GpioMeter> CreateNewDocument()
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
