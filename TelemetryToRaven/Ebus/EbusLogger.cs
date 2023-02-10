using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace TelemetryToRaven
{
    public class EbusLogger : LoggerService
    {
        public EbusLogger(ILogger<EbusLogger> logger, IDocumentStore database) : base(logger, database)
        {
        }

        protected override async Task DoWork(CancellationToken cancellationToken)
        {
            JsonNode parsed = GetEbusJson();
            var session = _store.OpenAsyncSession();
            string documentId = "meters/" + "ebus";
            var doc = await session.LoadAsync<Meter>(documentId);
            if (doc == null)
            {
                doc = new Meter();

                await _store.TimeSeries.RegisterAsync<Meter>("OutsideTemp", new[] { "Outside temperature [°C]" });
                await _store.TimeSeries.RegisterAsync<Meter>("DesiredFlowTemperature", new[] { "Flow setpoint [°C]" });
                await _store.TimeSeries.RegisterAsync<Meter>("RoomTemperature", new[] { "Room temperature [°C]" });
                await _store.TimeSeries.RegisterAsync<Meter>("DesiredRoomTemperature", new[] { "Room setpoint [°C]" });
                await _store.TimeSeries.RegisterAsync<Meter>("Modulation", new[] { "Modulation [%]" });
            }
            doc.VendorInfo = "Vaillant";
            doc.Medium = "ebus";
            await session.StoreAsync(doc, documentId);

            _logger.LogInformation("Adding telemetry");
            var appendSerie = (string path, string name, string childpath, string tag)
           =>
            {
                try
                {
                    JsonNode record = GetChild(parsed, path);
                    DateTime timestamp = GetTimestamp(record);
                    double value = (double)GetChild(record, childpath);
                    _logger.LogDebug($"{timestamp}\t {name}:\t {value}");
                    session.TimeSeriesFor(doc, name)
                      .Append(timestamp, value, tag);
                }
                catch (Exception e)
                {
                    _logger.LogError($"{path} : {e}");
                };
            };


            appendSerie("broadcast.messages.outsidetemp", "OutsideTemp", "fields.temp2.value", "°C");
            //appendSerie("hmu.messages.Status01", "FlowTemperature", "fields.0.value", "°C");
            appendSerie("hmu.messages.FlowTemp", "FlowTemperature", "fields.0.value", "°C");
            //appendSerie("hmu.messages.Status01", "ReturnTemperature", "fields.1.value", "°C"); //less accurate, but does not require a request
            appendSerie("hmu.messages.ReturnTemp", "ReturnTemperature", "fields.0.value", "°C");
            appendSerie("hmu.messages.SetMode", "DesiredFlowTemperature", "fields.flowtempdesired.value", "°C");

            appendSerie("hmu.messages.CircuitBuildingWaterPressure", "CircuitPressure", "fields.0.value", "bar");
            appendSerie("hmu.messages.CompressorSpeed", "CompressorSpeed", "fields.0.value", "Hz");
            appendSerie("hmu.messages.EnergyIntegral", "EnergyIntegral", "fields.energyintegral.value", "°Cmin");

            appendSerie("hmu.messages.State", "Modulation", "fields.0.value", "%");
            appendSerie("hmu.messages.State", "ThermalEnergyToday", "fields.1.value", "*100W");
            appendSerie("hmu.messages.State", "onoff", "fields.2.value", null);
            appendSerie("hmu.messages.State", "State", "fields.3.value", null);
            appendSerie("720.messages.z1RoomTemp", "RoomTemperature", "fields.tempv.value", "°C");
            appendSerie("720.messages.z1ActualRoomTempDesired", "DesiredRoomTemperature", "fields.tempv.value", "°C");
            appendSerie("720.messages.Hc1MinFlowTempDesired", "MinimumFlowTemp", "fields.tempv.value", "°C");
            appendSerie("720.messages.HwcStorageTemp", "DHWBoilerTemperature", "fields.tempv.value", "°C");

            await session.SaveChangesAsync();
        }

        private JsonNode GetEbusJson()
        {
            return JsonNode.Parse(RunScript("ebus.sh"));
        }

        public static JsonNode GetChild(JsonNode node, string path)
        {
            var splitter = path.IndexOf(".");
            if (splitter > 0)
                return GetChild(node[path.Substring(0, splitter)], path.Substring(splitter + 1));
            return node[path];
        }

        private static DateTime GetTimestamp(JsonNode someJson)
        {
            return DateTime.UnixEpoch.AddSeconds((double)someJson["lastup"]);
        }
    }
}