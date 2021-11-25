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

            var appendSerie = (JsonNode record, string name, string childpath)
           =>
            {
                DateTime timestamp = GetTimestamp(record);
                double value = (double)record.GetChild(childpath);
                Console.WriteLine($"{timestamp}\t {name}:\t {value}");
                session.TimeSeriesFor(doc, name)
                  .Append(timestamp, value, "°C");
            };


            appendSerie(parsed.GetChild("broadcast.messages.outsidetemp"), "OutsideTemp", "fields.temp2.value");
            appendSerie(parsed.GetChild("hmu.messages.Status01"), "FlowTemperature", "fields.0.value");
            appendSerie(parsed.GetChild("hmu.messages.Status01"), "ReturnTemperature", "fields.1.value");
            appendSerie(parsed.GetChild("hmu.messages.SetMode"), "DesiredFlowTemperature", "fields.flowtempdesired.value");

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