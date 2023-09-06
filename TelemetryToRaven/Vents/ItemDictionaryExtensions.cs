using System;
using System.Collections.Generic;

namespace TelemetryToRaven.Vents
{
    public static class ItemDictionaryExtensions
    {
        public static bool Bool(this Dictionary<ItemAddress, byte[]> items, ItemAddress address)
        {
            var value = items[address];
            if (value.Length != 1) throw new InvalidOperationException();
            return value[0] != 0;
        }

        public static double Temperature(this Dictionary<ItemAddress, byte[]> items, ItemAddress address)
        {
            var value = items[address];
            if (value.Length != 2) throw new InvalidOperationException();
            return BitConverter.ToInt16(value) / 10.0;
        }
        public static byte Byte(this Dictionary<ItemAddress, byte[]> items, ItemAddress address)
        {
            var value = items[address];
            if (value.Length != 1) throw new InvalidOperationException();
            return value[0];
        }
    }
}
