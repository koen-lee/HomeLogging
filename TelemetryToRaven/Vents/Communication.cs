using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TelemetryToRaven.Vents
{
    public class Communication
    {
        public const string Temperatures = "01b91f20212233bbba326a";
        public const string SwitchOn = "030101";
        public const string SwitchOff = "030100";

        static readonly string Prefix = "fdfd02";

        public static byte[] ComposeCommand(string serial, string password, string hexpostfix)
        {
            var serialBytes = Encoding.UTF8.GetBytes(serial);
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            var command = GetBytes(Prefix)
                .Append((byte)serialBytes.Length).Concat(serialBytes)
                .Append((byte)password.Length).Concat(passwordBytes)
                .Concat(GetBytes(hexpostfix))
                .ToArray();
            ushort calculatedSum = 0;
            foreach (var b in command[2..])
                calculatedSum += b;
            return command.Concat(BitConverter.GetBytes(calculatedSum)).ToArray();
        }

        static byte[] GetBytes(ReadOnlySpan<char> s)
        {
            var result = new byte[s.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = byte.Parse(s.Slice(2 * i, 2), System.Globalization.NumberStyles.AllowHexSpecifier);
            }
            return result;
        }

    }
}
