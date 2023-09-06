using System;
using TelemetryToRaven;
using Xunit;

namespace TelemetryTests
{
    public class ExtensionTest
    {
        [Theory]
        [InlineData("2012-04-01T12:23:34.992233Z", "2012-04-01T12:23:34.000000Z")]
        [InlineData("2012-04-01T12:23:34.112233Z", "2012-04-01T12:23:34.000000Z")]
        [InlineData("2012-04-01T12:23:34.000000Z", "2012-04-01T12:23:34.000000Z")]
        [InlineData("2012-04-01T12:23:35.100000Z", "2012-04-01T12:23:35.000000Z")]
        [InlineData("2012-04-01T12:23:33.100000Z", "2012-04-01T12:23:33.000000Z")]
        public void TruncatingADateTimeWorks(string input, string output)
        {
            var toTruncate = DateTime.Parse(input, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
            var expected = DateTime.Parse(output, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
            Assert.Equal(expected, toTruncate.TruncateToSeconds());
        }

        [Theory]
        [InlineData("2012-04-01T12:23:34.992233Z", "2012-04-01T12:23:34.900000Z")]
        [InlineData("2012-04-01T12:23:34.112233Z", "2012-04-01T12:23:34.100000Z")]
        [InlineData("2012-04-01T12:23:34.000000Z", "2012-04-01T12:23:34.000000Z")]
        public void TruncatingADateTimeToIntervalWorks(string input, string output)
        {
            var toTruncate = DateTime.Parse(input, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
            var expected = DateTime.Parse(output, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
            Assert.Equal(expected, toTruncate.TruncateTo(TimeSpan.FromSeconds(0.1)));
        }
    }
}