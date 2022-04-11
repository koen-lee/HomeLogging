using Raven.Client.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyConsumptionToTable
{
    internal class Program
    {
        public static void Main(string ciphertext =
            "53##+305))6*;4826)4#.)4#);806*;48+8$60))85;1#(;:#*8+83(88)5*+" +
            ";46(;88*96*?;8)*#(;485);5*+2:*#(;4956*2(5*-4)8$8*;4069285);)6+" +
            "8)4##;1(#9;48081;8:8#1;48+85;4)485+528806*81(#9;48;(88;4(#?" +
            "34;48)4#;161;:188;#?; ;483#0+2?3")
        {
            var englishFrequency = "etaoinsrhldcumfpgwybvkxjqz";
           // var englishFrequency = "ethosnairdflgbmyuvpc kxjqz";
            var counts = ciphertext.GroupBy(c => c).OrderByDescending(g => g.Count()).ToList();

            List<char> ciphers = new List<char>();
            List<char> substitutions = new List<char>();
            int i = 0;
            foreach (var count in counts)
            {
                Console.WriteLine($"{count.Key}\t{count.Count()}");
                substitutions.Add(englishFrequency[i++]);
                ciphers.Add(count.Key);
            }

            while (true)
            {
                Console.WriteLine(ciphertext);
                Console.WriteLine(Substitute(ciphertext, ciphers, substitutions));
                var input = Console.ReadLine();
                if (input.Length == 1)
                    Print(ciphers, substitutions);
                else if (input.StartsWith("@"))
                    ReorderSubstitutions(substitutions, input[1], input[2]);
                else if (!ciphers.Contains(input[0]))
                    Swap(substitutions, input[0], input[1]);
                else
                    AddSubstitution(ciphers, substitutions, input[0], input[1]);
            }
        }

        private static void ReorderSubstitutions(List<char> substitutions, char v1, char v2)
        {
            if (!substitutions.Remove(v2)) return;
            var index = substitutions.IndexOf(v1);
            if (index >= 0)
                substitutions.Insert(index, v2);
        }

        private static void AddSubstitution(List<char> ciphers, List<char> substitutions, char v1, char v2)
        {
            if (substitutions.Contains(v2))
            {
                Console.WriteLine($"{v2} already substituted");
                return;
            }
            substitutions[ciphers.IndexOf(v1)] = v2;
        }

        private static void Swap(List<char> substitutions, char v1, char v2)
        {
            Console.WriteLine($"Swap {v1} {v2}");
            var keyV1 = substitutions.IndexOf(v1);
            var keyV2 = substitutions.IndexOf(v2);
            if (keyV1 < 0 || keyV2 < 0) return;
            substitutions[keyV1] = v2;
            substitutions[keyV2] = v1;
        }

        private static string Substitute(string ciphertext, List<char> ciphers, List<char> substitutions)
        {
            for (int i = 0; i < ciphers.Count; i++)
            {
                ciphertext = ciphertext.Replace(ciphers[i], substitutions[i]);
            }
            return ciphertext;
        }


        private static void Print(List<char> ciphers, List<char> substitutions)
        {
            for (int i = 0; i < ciphers.Count; i++)
            {
                Console.WriteLine($"{ciphers[i]}  {substitutions[i]}");
            }
        }

        /// <summary>
        /// I suspect the heat meter to not include loss of defrost energy.
        /// Let's recalculate it with the reported temperature difference and flow.
        ///     Timestamp: 11-2-2022 15:13:02
        ///     Measured energy: 4789 kWh
        ///     Calculated energy: 4717.1 kWh(diff 71.9   -1.5%)
        /// </summary>
        /// <param name="serverurl"></param>
        /// <param name="database"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public static void TheOldMain(string serverurl = "http://192.168.2.11:8080", string database = "Eiland17Logging")
        {
            Console.WriteLine("Hello");
            var store = new DocumentStore()
            {
                Database = database,
                Urls = new[] { serverurl }
            };
            store.Initialize();

            using var session = store.OpenSession();
            string documentId = "meters/" + "10758808";
            var doc = session.Load<Meter>(documentId);

            var firstEntry = session.TimeSeriesFor(doc, "HeatEnergy").Get(pageSize: 1).Single();
            double startEnergy = firstEntry.Value;
            Console.WriteLine($"Start energy: {startEnergy} kWh");

            using var volumeflow = session.TimeSeriesFor(doc, "VolumeFlow").Stream();
            using var flowtemperature = session.TimeSeriesFor(doc, "FlowTemperature").Stream();
            using var returntemperature = session.TimeSeriesFor(doc, "ReturnTemperature").Stream();
            using var heatEnergy = session.TimeSeriesFor(doc, "HeatEnergy").Stream();

            double energyCalculated = 0; //joules
            int i = 0;
            while (volumeflow.MoveNext() && flowtemperature.MoveNext() && returntemperature.MoveNext() && heatEnergy.MoveNext())
            {
                if (volumeflow.Current.Timestamp != flowtemperature.Current.Timestamp ||
                    volumeflow.Current.Timestamp != returntemperature.Current.Timestamp ||
                    volumeflow.Current.Timestamp != heatEnergy.Current.Timestamp)
                {
                    throw new InvalidOperationException("Mismatched items");
                }
                // Q = Cw * dT * flow * time
                var power = 4186 * (flowtemperature.Current.Value - returntemperature.Current.Value) * (volumeflow.Current.Value / 3600 /* m³/h -> kg/s */);
                var energy = power * (volumeflow.Current.Timestamp - firstEntry.Timestamp).TotalSeconds;
                energyCalculated += energy;
                if (i++ % 100 == 0)
                    Console.Write('.');
                if (i % 5000 == 0)
                    Print(startEnergy, heatEnergy, energyCalculated);
                firstEntry = volumeflow.Current;
            }
            Print(startEnergy, heatEnergy, energyCalculated);
        }

        private static void Print(double startEnergy, IEnumerator<Raven.Client.Documents.Session.TimeSeries.TimeSeriesEntry> heatEnergy, double energyCalculated)
        {
            var energyCalculatedkWh = startEnergy + (energyCalculated / 3_600_000);
            Console.WriteLine($"\nMeasured energy: {heatEnergy.Current.Value} kWh Timestamp: {heatEnergy.Current.Timestamp}");
            Console.WriteLine($"Calculated energy: { energyCalculatedkWh:0.0} kWh (diff {heatEnergy.Current.Value - energyCalculatedkWh:0.0}   {100 - (100 * heatEnergy.Current.Value / energyCalculatedkWh):0.0}%)");
        }
    }
}
