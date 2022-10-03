using Microsoft.Extensions.Logging;
using NModbus;
using NModbus.Serial;
using Raven.Client.Documents;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NModbus.Device;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

namespace TelemetryToRaven.Sdm
{
    public class SdmLogger : LoggerService
    {
        private const string ElectricityMeter = "Sdm series electricity meter";

        public SdmLogger(ILogger<SdmLogger> logger, IDocumentStore database) : base(logger, database)
        {
        }

        protected override async Task DoWork(CancellationToken cancellationToken)
        {
            var port = Environment.GetEnvironmentVariable("SDM_PORT_PATH") ?? "/dev/ttyUSB1";
            _logger.LogInformation($"Using {port}");

            using var session = _store.OpenAsyncSession();
            var meters = await GetSdmMeters(cancellationToken, session);
            using var serialPort = new SerialPort(port);
            using var master = CreateModbusMaster(serialPort);

            foreach (var doc in meters)
            {
                try
                {
                    await ReadMeter(cancellationToken, doc, master, session);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Failure while reading device {doc.Id}");
                }
            }

            _logger.LogInformation("Done");
        }

        private ConcurrentModbusMaster CreateModbusMaster(SerialPort serialPort)
        {
            serialPort.BaudRate = 2400;
            serialPort.Parity = Parity.None;
            serialPort.ReadTimeout = (int)Delay.TotalMilliseconds;
            serialPort.Open();
            var master =
                new ConcurrentModbusMaster(new ModbusFactory().CreateRtuMaster(serialPort),
                    TimeSpan.FromMilliseconds(40));
            return master;
        }

        private async Task<List<SdmMeter>> GetSdmMeters(CancellationToken cancellationToken,
            IAsyncDocumentSession session)
        {
            var meters = session.Query<SdmMeter>(collectionName: "Meters")
                .Where(m => m.VendorInfo == ElectricityMeter)
                .ToList();

            if (!meters.Any())
            {
                var doc = await CreateNewDocument();
                await session.StoreAsync(doc, cancellationToken);
                meters.Add(doc);
            }

            return meters;
        }

        private async Task ReadMeter(CancellationToken cancellationToken, SdmMeter doc, ConcurrentModbusMaster master,
            IAsyncDocumentSession session)
        {
            _logger.LogInformation(
                $"Reading meter: {doc.Id} address: {doc.ModbusAddress} registers: {doc.Registers.Length}");
            var timestamp = DateTime.UtcNow;

            foreach (var definition in doc.Registers)
            {
                var data = await master.ReadInputRegistersAsync(doc.ModbusAddress, definition.Register, 2, 125,
                    cancellationToken);
                float value = BitConverter.Int32BitsToSingle(data[0] << 16 | data[1]);
                double rounded = Math.Round(value, 4);
                _logger.LogDebug("Got {value} {tag} for {SeriesName}", rounded, definition.Tag, definition.SeriesName);
                session.TimeSeriesFor(doc, definition.SeriesName)
                    .Append(timestamp, rounded, definition.Tag);
            }

            await session.SaveChangesAsync(cancellationToken);
        }


        private async Task<SdmMeter> CreateNewDocument()
        {
            await _store.TimeSeries.RegisterAsync<Meter>("Voltage", new[]
            {
                "Voltage [V]"
            });
            var doc = new SdmMeter
            {
                Id = "TestSdmMeter",
                VendorInfo = ElectricityMeter,
                Medium = "Electricity for heat pump",
                ModbusAddress = 1,
                Registers = new SdmMeter.RegisterDefinition[]
                {
                    new(Register: 12, SeriesName: "Power", Tag: "W"),
                    new(Register: 72, SeriesName: "Energy", Tag: "kWh"),
                }
            };

            return doc;
        }
    }

    public class SdmMeter : Meter
    {
        private RegisterDefinition[] _registers;
        public byte ModbusAddress { get; init; }
        public RegisterDefinition[] Registers
        {
            get => _registers ??= Array.Empty<RegisterDefinition>();
            init => _registers = value ?? throw new ArgumentNullException();
        }

        public record RegisterDefinition(ushort Register, string SeriesName, string Tag);
    }
}
