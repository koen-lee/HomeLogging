using System;

namespace TelemetryToRaven
{
    public record Meter
    {
        public string Id { get; set; }
        public string VendorInfo { get; set; }
        public string Medium { get; set; }
    }

    public static class DateTimeExtensions
    {
        public static DateTime TruncateToSeconds(this DateTime dateTime)
        {
            return new DateTime(dateTime.Ticks - (dateTime.Ticks % 10_000_000));
        }
        public static DateTime TruncateTo(this DateTime dateTime, TimeSpan interval)
        {
            return new DateTime(dateTime.Ticks - (dateTime.Ticks % interval.Ticks));
        }
    }
}