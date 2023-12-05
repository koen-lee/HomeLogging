using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace TelemetryToRaven
{
    public class EbusThermostatSwitcher : LoggerService
    {
        public EbusThermostatSwitcher(ILogger<EbusRunExtender> logger, IDocumentStore database) : base(logger, database)
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
             
            /*
             * 
             *  Kamer
   "Hc1RoomTempSwitchOn": {
    "name": "Hc1RoomTempSwitchOn",
    "passive": false,
    "write": false,
    "lastup": 1701611886,
    "zz": 21,
    "fields": {
     "rcmode": {"value": "modulating"}
    }
   },
            
   "Hc1RoomTempSwitchOn": {
    "name": "Hc1RoomTempSwitchOn",
    "passive": false,
    "write": false,
    "lastup": 1701612143,
    "zz": 21,
    "fields": {
     "rcmode": {"value": "thermostat"}
    }
   },
             */

            _logger.LogInformation("Reading telemetry");
            // Get runtime from telemetry
            // Get offtime from telemetry
            // Get current thermostat setting from ebus
            // Get current outside temperature from ebus
            /* if outside > 5 then switch to "thermostat"
             * if outside < 4 && offtime > 1h then switch to "modulating"
             * if outside < 4 && ontime > 1h then switch to "thermostat"
             */
        }
    }
}