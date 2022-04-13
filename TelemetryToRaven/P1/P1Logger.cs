using DSMRParser;
using Raven.Client.Documents;
using System.IO.Ports;

namespace TelemetryToRaven.P1
{
    public class P1Logger : LoggerService
    {
        public P1Logger(ILogger<P1Logger> logger, IDocumentStore database) : base(logger, database)
        {
        }

        protected override async Task DoWork(CancellationToken cancellationToken)
        {
            var port = Environment.GetEnvironmentVariable("P1_PORT_PATH") ?? "/dev/ttyUSB1";
            _logger.LogInformation($"Using {port}");

            var parser = new DSMRTelegramParser();

            using (var serial = new SerialPort(port))
            {
                serial.ReadTimeout = 1500; // more than a second

                var telegram = parser.Parse(serial.ReadLine());
                throw new NotImplementedException();

            }
        }
    }
}
