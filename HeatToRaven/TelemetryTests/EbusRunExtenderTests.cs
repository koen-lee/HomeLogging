using Divergic.Logging.Xunit;
using TelemetryToRaven;
using TelemetryToRaven.Weewx;
using Xunit;

namespace TelemetryTests
{
    public class EbusRunExtenderTests
    {
        private TestEbusRunExtender _undertest;
        private EbusMeter _doc;

        public EbusRunExtenderTests()
        {
            _undertest = new TestEbusRunExtender();
            _doc = new EbusMeter
            {
                MinimumFlowTemperature = 24,
                MaximumFlowTemperature = 30
            };
        }

        [Theory]
        [InlineData(0, 24, 24, 25, 25)]
        [InlineData(0, 26, 0, 25, 24)]
        [InlineData(0, 23, 0, 15, 24)]
        [InlineData(0, 23, 23, 23.5, 24)]
        [InlineData(0, 24, 24, 24.5, 24.5)]
        public void ParseTableItem(double modulation, double currentMinimum, double desired, double actualFlow, double newMinimum)
        {
            _undertest.UpdateMinimumFlowTemp(_doc, currentMinimum, actualFlow, desired, modulation);
            Assert.Equal(newMinimum, _undertest.MinimumFlowTemperature);
        }


        public class TestEbusRunExtender : EbusRunExtender
        {
            public TestEbusRunExtender() : base(new CacheLogger<TestEbusRunExtender>(), null)
            {
                MinimumFlowTemperature = double.NaN;
            }
            public double MinimumFlowTemperature { get; private set; }

            protected override void SetMinimumFlowTemp(double minimumFlowTemperature)
            {
                MinimumFlowTemperature = minimumFlowTemperature;
            }
        }
    }
}