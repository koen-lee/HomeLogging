using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TelemetryToRaven.Kasa
{
    public static class StreamExtensions
    {
        public static async Task<dynamic> GetResponseAsync(this Stream stream, CancellationToken cancellationToken = default)
        {
            byte[] result = await ReadResult(stream, cancellationToken);
            var json = Decrypt(result);
            dynamic deserialized = JsonConvert.DeserializeObject(json);
            return deserialized;
        }

        public static async Task WriteRequestAsync(this Stream stream, object request, CancellationToken cancellationToken = default)
        {
            var requestJson = JsonConvert.SerializeObject(request);
            var message = Encrypt(requestJson);
            var messageSize = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(message.Length));
            await stream.WriteAsync(messageSize, cancellationToken);
            await stream.WriteAsync(message, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        private static async Task<byte[]> ReadResult(Stream stream, CancellationToken cancellationToken = default)
        {
            byte[] result = new byte[4];
            if (await stream.ReadAsync(result, cancellationToken) < result.Length) throw new InvalidDataException($"Too short response");
            var length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(result, 0));
            result = new byte[length];
            await stream.ReadAsync(result);
            return result;
        }

        static byte[] Encrypt(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            byte key = 171;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                key ^= b;
                bytes[i] = key;
            }
            return bytes;
        }

        static string Decrypt(byte[] input)
        {
            byte key = 171;
            for (int i = 0; i < input.Length; i++)
            {
                byte b = input[i];
                input[i] ^= key;
                key = b;
            }
            return Encoding.UTF8.GetString(input);
        }
    }

    public class PowerReading
    {
        private readonly dynamic _power;
        internal PowerReading(dynamic power)
        {
            _power = power;
        }

        public double CurrentPowerInW => _power.emeter.get_realtime.power_mw / 1000.0;

        public double CumulativeEnergyInkWh => _power.emeter.get_realtime.total_wh / 1000.0;

        public override string ToString()
        {
            return $"{nameof(_power)}: {_power}";
        }
    }
    public class InfoReading
    {
        private dynamic _info;
        internal InfoReading(dynamic info)
        {
            _info = info;
        }

        public string Mac => _info.system.get_sysinfo.mac;

        public string Alias => _info.system.get_sysinfo.alias;

        public override string ToString()
        {
            return $"{nameof(_info)}: {_info}";
        }
    }
}


