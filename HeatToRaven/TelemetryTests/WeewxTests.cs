using TelemetryToRaven.Weewx;
using Xunit;

namespace TelemetryTests
{
    public class WeewxTests
    {
        [Theory]
        [InlineData("Wind", "0.9 m/s ESE (110°)", "Wind", new[] { 0.9, 110 }, "m/s;°")]
        [InlineData("Wind", "0.0 m/s N/A (N/A)", "Wind", new[] { 0.0 }, "m/s")]
        [InlineData("Barometer", "1008.6 mbar (-1.7)", "Barometer", new[] { 1008.6, -1.7 }, "mbar;")]
        [InlineData("Radiation", "57 W/m²", "Radiation", new[] { 57.0 }, "W/m²")]
        [InlineData("Outside Humidity", "83%", "OutsideHumidity", new[] { 83.0 }, "%")]
        public void ParseTableItem(string label, string data, string name, double[] values, string unit)
        {
            var undertest = WeewxLogger.GetItem(label, data);
            Assert.NotNull(undertest);
            Assert.Equal(name, undertest.Name);
            Assert.Equal(values, undertest.Values);
            Assert.Equal(unit, undertest.Unit);
        }
    }
}