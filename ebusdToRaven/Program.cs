using Raven.Client.Documents;
using System.Text.Json.Nodes;

namespace ebusdToRaven
{
    public static class Program
    {
        public static void Main(string serverurl = "http://energylogger:8080", string database = "Eiland17Logging")
        {
            Console.WriteLine("Awaiting ebusd json input on stdin");
            JsonNode parsed = JsonNode.Parse(Console.OpenStandardInput());

            Persist(serverurl, database, parsed);
            Console.WriteLine("Done");
        }

        private static void Persist(string serverurl, string database, JsonNode parsed)
        {

            Console.WriteLine("Opening store");
            var store = new DocumentStore()
            {
                Database = database,
                Urls = new[] { serverurl }
            };
            store.Initialize();
            var session = store.OpenSession();
            string documentId = "meters/" + "ebus";
            var doc = session.Load<Meter>(documentId);
            if (doc == null) doc = new Meter();
            doc.VendorInfo = "Vaillant";
            doc.Medium = "ebus";
            session.Store(doc, documentId);

            Console.WriteLine("Adding telemetry");
            var appendSerie = (string path, string name, string childpath, string tag)
           =>
            {
                try
                {
                    JsonNode record = parsed.GetChild(path);
                    DateTime timestamp = GetTimestamp(record);
                    double value = (double)record.GetChild(childpath);
                    Console.WriteLine($"{timestamp}\t {name}:\t {value}");
                    session.TimeSeriesFor(doc, name)
                      .Append(timestamp, value, tag);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"{path} : {e}");
                };
            };


            appendSerie("broadcast.messages.outsidetemp", "OutsideTemp", "fields.temp2.value", "°C");
            appendSerie("hmu.messages.Status01", "FlowTemperature", "fields.0.value", "°C");
            appendSerie("hmu.messages.Status01", "ReturnTemperature", "fields.1.value", "°C");
            appendSerie("hmu.messages.SetMode", "DesiredFlowTemperature", "fields.flowtempdesired.value", "°C");

            appendSerie("hmu.messages.State", "Modulation", "fields.0.value", "%");
            appendSerie("hmu.messages.State", "ThermalEnergyToday", "fields.1.value", "*100W");
            appendSerie("hmu.messages.State", "onoff", "fields.2.value", null);
            appendSerie("hmu.messages.State", "State", "fields.3.value", null);
            appendSerie("720.messages.z1RoomTemp", "RoomTemperature", "fields.tempv.value", "°C");
            appendSerie("720.messages.z1ActualRoomTempDesired", "DesiredRoomTemperature", "fields.tempv.value", "°C");

            session.SaveChanges();
        }

        public static JsonNode GetChild(this JsonNode node, string path)
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