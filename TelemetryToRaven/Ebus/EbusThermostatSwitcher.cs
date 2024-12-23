using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Session.TimeSeries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TelemetryToRaven
{
    public class EbusThermostatSwitcher : LoggerService
    {
        public EbusThermostatSwitcher(ILogger<EbusThermostatSwitcher> logger, IDocumentStore database) : base(logger, database)
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
            if (!doc.SwitchThermostat)
            {
                _logger.LogInformation("SwitchThermostat disabled, dropping out.");
                return;
            }

            _logger.LogInformation("Reading telemetry");

            // rough logic:
            /* if outside > 5C then switch to "thermostat"
             * if outside < 4C && offtime > 1h then switch to "modulating" = weather dependent with room compensation
             * if outside < 4C && ontime > 1h then switch to "thermostat" = weather dependent with on/off
             */

            // By using the average outside temperature, we don't need a hysteresis: it is updated once per hour.
            var outsideTemperature = GetFromThermostat<double>("OutsideTempAvg");

            var now = DateTime.UtcNow;
            if (outsideTemperature > doc.PermanentSwitchTemperature && InOffPeriod(now.TimeOfDay, doc.SwitchTimePeriods))
                SwitchTo("thermostat");
            else
            {
                var period = Max(doc.MinimumOnPeriod, doc.MinimumOffPeriod);
                var setpoints = (await session.TimeSeriesFor(documentId, "DesiredFlowTemperature")
                    .GetAsync(now.Subtract(period), token: cancellationToken)
                    ).ToList();
                if (setpoints.Count < 10)
                {
                    _logger.LogWarning("Not enough data, do nothing.");
                    return;
                }
                var onCount = setpoints.Count(e => e.Value > 0);
                _logger.LogDebug($"Datapoint count: {setpoints.Count} On count: {onCount}");
                if (setpoints.YoungerThan(now.Subtract(doc.MinimumOnPeriod)).All(e => e.Value > 0))
                {
                    _logger.LogInformation("Long runtime reached. Prevent overshoot.");
                    SwitchTo("thermostat");
                }
                else if (setpoints.YoungerThan(now.Subtract(doc.MinimumOffPeriod)).All(e => e.Value <= 0))
                {
                    _logger.LogInformation("Long offtime reached. Prevent cold floors.");
                    SwitchTo("modulating");
                }
            }
        }

        private bool InOffPeriod(TimeSpan timeOfDay, EbusMeter.TimeRange[] switchTimePeriods)
        {
            foreach (var period in switchTimePeriods)
            {
                if (period.On <= timeOfDay && timeOfDay <= period.Off)
                    return false;
            }
            return true;
        }

        private void SwitchTo(string desiredSetting)
        {
            var actualSetting = GetFromThermostat<string>("Hc1RoomTempSwitchOn");
            if (desiredSetting == actualSetting)
            {
                _logger.LogDebug($"Nothing to do, thermostat already set to '{actualSetting}'");
                return;
            }
            _logger.LogInformation($"Switching from {actualSetting} to {desiredSetting}");
            RunCommand("ebusctl", "write -c 720 Hc1RoomTempSwitchOn " + desiredSetting);
        }

        private T GetFromThermostat<T>(string setting)
        {
            var result = RunCommand("ebusctl", "read -m 50 -c 720 " + setting);
            return (T)Convert.ChangeType(result.Trim(), typeof(T));
        }

        public static TimeSpan Max(TimeSpan ts1, TimeSpan ts2)
        {
            return TimeSpan.FromTicks(Math.Max(ts1.Ticks, ts2.Ticks));
        }

    }

    static class Extensions
    {
        public static IEnumerable<TimeSeriesEntry> YoungerThan(this IEnumerable<TimeSeriesEntry> entries, DateTime cutoff)
        {
            return entries.Where(e => e.Timestamp > cutoff);
        }
    }
}