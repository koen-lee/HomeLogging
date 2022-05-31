using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using System;
using System.Linq;
using System.Text.Json.Nodes;
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
                 var entries = session.TimeSeriesFor(documentId, timeseries).GetAsync(DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(2)), token: cancellationToken).Result;
                 if (entries.Any())
                 {
                     value = entries.Average(e => e.Value);
                     return true;
                 }
                 _logger.LogWarning($"Missing {timeseries}");
                 value = double.NaN;
                 return false;
             };


            _logger.LogInformation("Reading telemetry");
            // matches timeseries as saved by EbusLogger
            if (!GetLast("MinimumFlowTemp", out var minimumFlowTemp)) return;
            if (!GetLast("FlowTemperature", out var actualFlowTemp)) return;
            if (!GetLast("DesiredFlowTemperature", out var desiredFlowTemp)) return;
            if (!GetLast("Modulation", out var modulation)) return;

            if (desiredFlowTemp < minimumFlowTemp && minimumFlowTemp > doc.MinimumFlowTemperature)
            {
                // reset
                SetMinimumFlowTemp(doc.MinimumFlowTemperature);
            }
            else if (actualFlowTemp > desiredFlowTemp && modulation < 2 && actualFlowTemp < doc.MaximumFlowTemperature)
            {
                // extend the run by setting the minimum to the actual flow temp, so the heatpump controls think all is well.
                SetMinimumFlowTemp(actualFlowTemp);
            }
        }

        private void SetMinimumFlowTemp(double minimumFlowTemperature)
        {
            _logger.LogInformation($"Setting new minimum flow temperature to {minimumFlowTemperature}");
            RunScript("writeminflowtemp.sh", $"{minimumFlowTemperature:0.0}");
        }
    }

    public class EbusMeter : Meter
    {
        public bool ExtendRuns { get; set; }
        public double MinimumFlowTemperature { get; set; }
        public double MaximumFlowTemperature { get; set; }
    }
}