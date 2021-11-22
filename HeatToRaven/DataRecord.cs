using System.Xml.Serialization;

namespace HeatToRaven
{
    public class DataRecord
    {
        [XmlAttribute("id")]
        public int Id;

        public string Function;
        public string Unit;
        public string Value;


        public DateTimeOffset Timestamp;
    }
}