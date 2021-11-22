using Raven.Client.Documents;
using System.Xml.Serialization;

namespace HeatToRaven
{
    class Program
    {
        public static void Main(string serverurl = "http://energylogger:8080", string database = "Eiland17Logging")
        {
            Console.WriteLine("Awaiting input on stdin");
            var parsed = Deserialize(Console.OpenStandardInput());

            foreach (var item in parsed.Records)
            {
                Console.WriteLine($"{item.Id}\t {item.Unit} {item.Value}");
            }

            var records = parsed.Records.ToDictionary(x => x.Id);

            var store = new DocumentStore()
            {
                Database = database,
                Urls = new[] { serverurl }
            };
            store.Initialize();

            using var session = store.OpenSession();
            string documentId = "meters/" + parsed.SlaveInformation.Id;
            var doc = session.Load<Meter>(documentId);
            if (doc == null) doc = new Meter();
            doc.VendorInfo = parsed.SlaveInformation.Manufacturer;
            doc.Medium = parsed.SlaveInformation.Medium;
            session.Store(doc, documentId);

            var appendSerie = (MBusData.DataRecord record, string name, string tag, double factor)
                =>
            {
                session.TimeSeriesFor(doc, name)
                  .Append(record.Timestamp.UtcDateTime, record.NumericValue * factor, tag);
            };

            appendSerie(records[1], "HeatEnergy", "kWh", 1);
            appendSerie(records[9], "FlowTemperature", "°C", 0.01);
            appendSerie(records[10], "ReturnTemperature", "°C", 0.01);
            appendSerie(records[13], "VolumeFlow", "m³/h", 1);
            appendSerie(records[12], "Power", "W", 100);

            session.SaveChanges();
            Console.WriteLine("Done");
        }

        private static MBusData Deserialize(Stream stream)
        {
            var serializer = new XmlSerializer(typeof(MBusData), "");
            return (MBusData)serializer.Deserialize(stream);
        }
    }
}