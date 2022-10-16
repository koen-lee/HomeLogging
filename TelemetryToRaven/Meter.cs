namespace TelemetryToRaven
{
    public record Meter
    {
        public string Id { get; set; }
        public string VendorInfo { get; set; }
        public string Medium { get; set; }
    }
}