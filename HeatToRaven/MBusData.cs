
using System.Xml.Serialization;

namespace HeatToRaven
{
    public class MBusData
    {
        [XmlElement("DataRecord")]
        public DataRecord[] Records;
    }
}