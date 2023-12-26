using TelemetryToRaven.Sdm;

namespace TelemetryToRaven.Gpio
{
    internal record GpioMeter : Meter
    {
        public int GpioPin { get; set; }
        public double QuantityPerPulse { get; set; }
        public string Unit { get; set; }
        public string TimeseriesName { get; internal set; }
    }
}