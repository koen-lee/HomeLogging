using HidSharp;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;

namespace TelemetryToRaven.ZyCO2
{
    public class ZGm053Service : BackgroundService
    {
        private readonly ILogger<ZGm053Service> _logger;
        private readonly IDocumentStore _store;

        public ZGm053Service(ILogger<ZGm053Service> logger, IDocumentStore documentStore)
        {
            _logger = logger;
            _store = documentStore;
        }

        public async Task ReadDevice(CancellationToken cancellationToken, HidDevice dev)
        {
            _logger.LogInformation($"Reading {dev}");
            string source = dev.GetFriendlyName() + " s/n" + dev.GetSerialNumber();
            string documentId = "meters/" + dev.GetSerialNumber();
            await EnsureDocument(source, documentId);
            using (var stream = dev.Open())
            {
                var msg = new byte[1].Concat(_key).ToArray(); //prepend 0
                stream.SetFeature(msg);
                while (true)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    await PublishNextReading(stream, documentId);
                }
            }
        }

        private async Task EnsureDocument(string source, string documentId)
        {
            using (var session = _store.OpenAsyncSession())
            {

                var doc = await session.LoadAsync<Meter>(documentId);
                if (doc == null)
                {
                    doc = new Meter
                    {
                        Id = documentId,
                        Medium = "CO2",
                        VendorInfo = source
                    };

                    await _store.TimeSeries.RegisterAsync<Meter>("CO2", new[] { "CO₂ concentration" });
                    await _store.TimeSeries.RegisterAsync<Meter>("RoomTemperature", new[] { "Room temperature [°C]" });
                    await session.StoreAsync(doc);
                    await session.SaveChangesAsync();
                }
            }
        }

        private async Task PublishNextReading(HidStream stream, string documentId)
        {
            var data = new byte[9];
            await stream.ReadAsync(data);
            if (data[0] == 0)
            {
                data = data.Skip(1).ToArray();
            }

            ParseAndPublishReading(data, documentId);
        }

        readonly byte[] _key = { 0xc4, 0xc6, 0xc0, 0x92, 0x40, 0x23, 0xdc, 0x96 };

        private void DecryptHoltekZytempReport(byte[] data)
        {
            int[] shuffle = { 2, 4, 0, 7, 1, 6, 5, 3 };

            var temp = new byte[8];
            for (int i = 0; i < 8; i++)
            {
                int di = shuffle[i];
                temp[di] = data[i];
                temp[di] ^= _key[di];
            }

            byte[] temp1 = new byte[8];
            for (int i = 0; i < 8; i++)
            {
                int oi = (i - 1 + 8) & 7;
                temp1[i] = (byte)((((temp[i] >> 3) & 31) | (temp[oi] << 5)) & 0xff);
            }

            byte[] cstate = { (byte)'H', (byte)'t', (byte)'e', (byte)'m', (byte)'p', (byte)'9', (byte)'9', (byte)'e' }; // salt
            byte[] ctemp = new byte[8];
            for (int i = 0; i < 8; i++)
            {
                ctemp[i] = (byte)(((cstate[i] >> 4) & 15) | (cstate[i] << 4));
            }

            for (int i = 0; i < 8; i++)
            {
                data[i] = (byte)(0x100 + temp1[i] - ctemp[i]);
            }
        }

        enum ZyAuraOpcode : byte
        {
            RelativeHumidity = (byte)'A', // 0x41,
            Temperature = (byte)'B', // 0x42,
            Unknown_C = (byte)'C', // 0x43,
            Unknown_O = (byte)'O', // 0x4f,
            Relative_CO2_Concentration = (byte)'P', // 0x50,
            Unknown_R = (byte)'R', // 0x52
            Checksum_Error = (byte)'S', // 0x53
            Unknown_V = (byte)'V', // 0x56
            Unknown_W = (byte)'W', // 0x57
            Unknown_m = (byte)'m', // 0x6d,
            Unknown_n = (byte)'n', // 0x6e,
            Unknown_q = (byte)'q', // 0x71,
        }

        public void ParseAndPublishReading(byte[] data, string documentId)
        {
            var telemetry = new Telemetry
            {
                Timestamp = DateTimeOffset.UtcNow
            };
            DecryptHoltekZytempReport(data);
            var rawValue = (ushort)((data[1] << 8) | data[2]);
            switch ((ZyAuraOpcode)data[0])
            {
                case ZyAuraOpcode.Relative_CO2_Concentration:
                    telemetry = telemetry with
                    {
                        Measurement = rawValue,
                        Unit = "ppm",
                        SensorName = "CO2"
                    };
                    break;
                case ZyAuraOpcode.Temperature:
                    telemetry = telemetry with
                    {
                        Measurement = rawValue / 16.0 - 273.15,
                        Unit = "°C",
                        SensorName = "RoomTemperature"
                    };
                    break;
            }

            _logger.LogInformation("Read {reading}", telemetry);
            using var session = _store.OpenSession();
            session.TimeSeriesFor(documentId, telemetry.SensorName)
                      .Append(telemetry.Timestamp.UtcDateTime, telemetry.Measurement, telemetry.Unit);
            session.SaveChanges();
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var allDeviceList = DeviceList.Local.GetHidDevices(0x04d9, 0xa052).ToList();
            _logger.LogDebug($"Polling {allDeviceList.Count} devices");
            IEnumerable<Task> readTasks = allDeviceList.Select(dev => ReadDevice(stoppingToken, dev));
            return Task.WhenAll(readTasks);
        }
    }

}
