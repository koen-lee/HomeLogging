using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using System;
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
            /* if outside > 5 then switch to "thermostat"
             * if outside < 4 && offtime > 1h then switch to "modulating" = weather dependent with room compensation
             * if outside < 4 && ontime > 1h then switch to "thermostat" = weather dependent with on/off
             */
            var thermostatSetting = GetFromThermostat<string>("Hc1RoomTempSwitchOn");
            var outsideTemperature = GetFromThermostat<double>("OutsideTempAvg");

            if (outsideTemperature > doc.PermanentSwitchTemperature + 0.5)
                SwitchTo("thermostat", thermostatSetting);
            else if (outsideTemperature < doc.PermanentSwitchTemperature - 0.5)
            {
                var now = DateTime.UtcNow;
                var period = Math.Max(doc.MinimumOnPeriod.TotalMinutes, doc.MinimumOffPeriod.TotalMinutes);
                var entries = (await session.TimeSeriesFor(documentId, "DesiredFlowTemperature")
                    .GetAsync(now.Subtract(TimeSpan.FromMinutes(period)), token: cancellationToken)
                    ).ToList();
                if (entries.Count < 10)
                {
                    _logger.LogWarning("Not enough data, do nothing.");
                    return;
                }
                if (entries.Where(e => e.Timestamp > now.Subtract(doc.MinimumOnPeriod)).All(e => e.Value > 0))
                {
                    _logger.LogInformation("Long runtime reached. Prevent overshoot.");
                    SwitchTo("thermostat", thermostatSetting);
                } else if (entries.Where(e => e.Timestamp > now.Subtract(doc.MinimumOffPeriod)).All(e => e.Value <= 0))
                {
                    _logger.LogInformation("Long offtime reached. Prevent cold floors.");
                    SwitchTo("modulating", thermostatSetting);
                }
            }
        }

        private void SwitchTo(string desiredSetting, string actualSetting)
        {
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
    }
}