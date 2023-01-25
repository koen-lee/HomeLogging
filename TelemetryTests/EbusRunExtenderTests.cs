using Divergic.Logging.Xunit;
using TelemetryToRaven;
using Xunit;

namespace TelemetryTests
{
    public class EbusRunExtenderTests
    {
        private TestEbusRunExtender _undertest;
        private EbusMeter _doc;

        public EbusRunExtenderTests()
        {
            _undertest = new TestEbusRunExtender(new CacheLogger<TestEbusRunExtender>());

            _doc = new EbusMeter
            {
                MinimumFlowTemperature = 24,
                MaximumFlowTemperature = 30
            };
        }

        [Theory]
        [InlineData(0, 24, 24, 25, 25)] // Extend the run when overshooting Ta
        [InlineData(0, 26, 0, 22, 24)]  // Reset the run when undershooting Ta
        [InlineData(0, 23, 0, 15, 24)]  // Increase the minimum when it is lower than configured
        [InlineData(0, 23, 23, 23.5, 24)] // 
        [InlineData(0, 24, 24, 24.5, 24.5)] // extend the run when on minimum
        public void ParseTableItem(double modulation, double currentMinimum, double desired, double actualFlow, double newMinimum)
        {
            _undertest.UpdateMinimumFlowTemp(_doc, currentMinimum, actualFlow, desired, modulation);
            Assert.Equal(newMinimum, _undertest.MinimumFlowTemperature);
        }

        [Theory]
        [InlineData(9, 24, 25, 25, 25.5)]
        [InlineData(20, 26, 26, 26, 25.5)]
        [InlineData(5, 30, 30, 30, double.NaN)]
        [InlineData(20, 25, 26, 26, double.NaN)]
        public void ManageModulation(double modulation, double currentMinimum, double desired, double actualFlow, double newMinimum)
        {
            _doc.DesiredModulation = 10;
            _undertest.UpdateMinimumFlowTemp(_doc, currentMinimum, actualFlow, desired, modulation);
            Assert.Equal(newMinimum, _undertest.MinimumFlowTemperature);
        }
    }

    public class TestEbusRunExtender : EbusRunExtender
    {
        public CacheLogger CacheLogger;
        public TestEbusRunExtender(CacheLogger<TestEbusRunExtender> logger) : base(logger, null)
        {
            MinimumFlowTemperature = double.NaN;
            CacheLogger = logger;
        }
        public double MinimumFlowTemperature { get; private set; }

        protected override void SetMinimumFlowTemp(double minimumFlowTemperature)
        {
            MinimumFlowTemperature = minimumFlowTemperature;
        }
    }
}