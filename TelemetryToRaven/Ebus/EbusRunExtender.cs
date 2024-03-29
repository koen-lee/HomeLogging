﻿using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TelemetryToRaven
{
    public class EbusRunExtender : LoggerService
    {
        public EbusRunExtender(ILogger<EbusRunExtender> logger, IDocumentStore database) : base(logger, database)
        {
        }

        protected override async Task DoWork(CancellationToken cancellationToken)
        {
            var session = _store.OpenAsyncSession();
            string documentId = "meters/" + "ebus";
            var doc = await session.LoadAsync<EbusMeter>(documentId);

            if (doc == null)
            {
                _logger.LogInformation("No config, dropping out.");
                return;
            }
            if (!doc.ExtendRuns)
            {
                _logger.LogInformation("ExtendRuns disabled, dropping out.");
                return;
            }
            if (doc.MinimumFlowTemperature >= doc.MaximumFlowTemperature)
            {
                _logger.LogError($"Min {doc.MinimumFlowTemperature} >= Max {doc.MaximumFlowTemperature} ");
                return;
            }

            var GetLast = (string timeseries, out double value) =>
            {
                var entries = session.TimeSeriesFor(documentId, timeseries).GetAsync(DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(1.5)), token: cancellationToken).Result;
                if (entries.Any())
                {
                    value = entries.Last().Value;
                    _logger.LogDebug($"Using {timeseries} {value}");
                    return true;
                }
                _logger.LogWarning($"Missing {timeseries}");
                value = double.NaN;
                return false;
            };

            _logger.LogInformation("Reading telemetry");
            // matches timeseries as saved by EbusLogger
            if (!GetLast("MinimumFlowTemp", out var currentMinimum)) return;
            if (!GetLast("FlowTemperature", out var actualFlowTemp)) return;
            if (!GetLast("DesiredFlowTemperature", out var desiredFlowTemp)) return;
            if (!GetLast("CompressorSpeed", out var speed)) return;

            UpdateMinimumFlowTemp(doc, currentMinimum, actualFlowTemp, desiredFlowTemp, speed);
        }

        public void UpdateMinimumFlowTemp(EbusMeter settings, double currentMinimum, double actualFlowTemp, double desiredFlowTemp, double speed)
        {
            if (currentMinimum < settings.MinimumFlowTemperature)
            {
                _logger.LogInformation("Reset temperature, it was lower than the configured minimum.");
                SetMinimumFlowTemp(settings.MinimumFlowTemperature, currentMinimum, settings); ;
            }
            else if (desiredFlowTemp < 1 && currentMinimum > settings.MinimumFlowTemperature)
            {
                _logger.LogInformation("Reset temperature, it was higher and there is no heat requested.");
                SetMinimumFlowTemp(settings.MinimumFlowTemperature, currentMinimum, settings); ;
            }
            else if (speed < settings.DesiredModulation &&
                     actualFlowTemp < settings.MaximumFlowTemperature &&
                     actualFlowTemp >= desiredFlowTemp &&
                     desiredFlowTemp >= 1)
            {
                // increase modulation by increasing the flow temp, so the heatpump controls think all is well.
                _logger.LogInformation("Increase modulation");
                SetMinimumFlowTemp(desiredFlowTemp + 0.5, currentMinimum, settings); ;
            }
            else if (speed > settings.DesiredModulation + 5 &&
                     actualFlowTemp > settings.MinimumFlowTemperature
                     && actualFlowTemp <= desiredFlowTemp
                     && desiredFlowTemp <= currentMinimum)
            {
                _logger.LogInformation("Decrease modulation");
                SetMinimumFlowTemp(currentMinimum - 0.5, currentMinimum, settings);
            }
            else
            {
                _logger.LogDebug($"Nothing to do, all is well");
            }
        }

        protected virtual void SetMinimumFlowTemp(double minimumFlowTemperature, double currentMinimum, EbusMeter settings)
        {
            if (minimumFlowTemperature < settings.MinimumFlowTemperature)
                return;
            if (minimumFlowTemperature > settings.MaximumFlowTemperature)
                return;
            if (Math.Abs(minimumFlowTemperature - currentMinimum) < 0.4)
                return;
            _logger.LogInformation($"Setting new minimum flow temperature to {minimumFlowTemperature}");
            RunScript("writeminflowtemp.sh", $"{minimumFlowTemperature:0.0}");
        }
    }
}