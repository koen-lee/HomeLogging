
namespace TelemetryToRaven.ZyCO2
{
    internal record Telemetry
    {
        public DateTimeOffset Timestamp { get; set; }
        public double Measurement { get; set; }
        public string Unit { get; set; }
        public string SensorName { get; set; }
    }
}