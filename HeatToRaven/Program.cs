using System.Xml.Serialization;

namespace HeatToRaven
{
    class Program
    {
        public static void Main(string invocation, string database)
        {
            var contents = File.ReadAllText("mbus_sample.xml");
            var parsed = Deserialize(contents);

            foreach (var item in parsed.Records)
            {
                Console.WriteLine($"{item.Id}\t {item.Unit} {item.Value}");
            }


        }

        private static MBusData Deserialize(string contents)
        {
            var serializer = new XmlSerializer(typeof(MBusData), "");
            var stream = new StringReader(contents);
            return (MBusData)serializer.Deserialize(stream);
        }
    }
}