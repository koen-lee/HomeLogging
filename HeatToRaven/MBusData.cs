
using System.Globalization;
using System.Xml.Serialization;

namespace HeatToRaven
{
    public class MBusData
    {
        public SlaveInfo SlaveInformation;

        [XmlElement("DataRecord")]
        public DataRecord[] Records;


        public class SlaveInfo
        {
            public string Id;
            public string Manufacturer;
            public string Version;
            public string ProductName;
            public string Medium;
        }

        public class DataRecord
        {
            [XmlAttribute("id")]
            public int Id;

            public string Function;
            public string Unit;
            public string Value;


            public DateTimeOffset Timestamp;

            public double NumericValue
            {
                get { return Double.Parse(Value, CultureInfo.InvariantCulture); }
            }
        }
    }
}