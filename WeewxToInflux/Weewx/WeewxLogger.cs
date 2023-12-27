using AngleSharp;
using InfluxDB.Client;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WeewxToInflux.Weewx
{
    public class WeewxLogger : LoggerService
    {
        public WeewxLogger(ILogger<WeewxLogger> logger, InfluxDBClient database) : base(logger, database)
        {
        }

        protected override async Task DoWork(CancellationToken cancellationToken)
        {
            var path = Environment.GetEnvironmentVariable("WEEWX_INDEX") ?? "/var/www/html/weewx/index.html";
            await DoSpiderPage(path, cancellationToken);
        }

        public async Task DoSpiderPage(string path, CancellationToken cancellationToken)
        {
            var context = BrowsingContext.New(Configuration.Default.WithRequesters().WithDefaultLoader());

            var parsed = await context.OpenAsync(path);

            var aboutRows = parsed.QuerySelector("#about_widget").QuerySelectorAll("tr").ToDictionary(
                r => r.GetElementsByClassName("label").First().TextContent,
                r => r.GetElementsByClassName("data").First().TextContent);

            var currentTable = parsed.QuerySelector("#current_widget").QuerySelectorAll("tr");
            var currentValues = currentTable.Select(r => GetItem(
                    r.GetElementsByClassName("label").Single().TextContent,
                    r.GetElementsByClassName("data").Single().TextContent))
                .Where(i => i != null).ToList();

            var session = _store.GetWriteApiAsync();
  
            var timestamp = DateTime.ParseExact(parsed.QuerySelector(".lastupdate").TextContent, "dd/MM/yy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);


            await session.WriteMeasurementsAsync(currentValues, cancellationToken: cancellationToken);
            _logger.LogInformation("Done");
        }

        static Regex _valueRegex = new Regex(@"(-?\d+[.]?\d*)\s?(\S+)[^(]*\(?([^)]*)", RegexOptions.Compiled);
        public static WeatherItem GetItem(string label, string data)
        {
            string unit;
            double value;
            var values = new List<double>();
            if (double.TryParse(data, out value))
            {
                unit = string.Empty;
                values.Add(value);
            }
            else
            {
                var valueParts = _valueRegex.Match(data);
                if (!valueParts.Success)
                    return null;

                unit = valueParts.Groups[2].Value;
                value = double.Parse(valueParts.Groups[1].Value);
                values.Add(value);
                if (valueParts.Groups[3].Success && !string.IsNullOrWhiteSpace(valueParts.Groups[3].Value))
                {
                    var subitem = GetItem(label, valueParts.Groups[3].Value);
                    if (subitem != null)
                    {
                        values.AddRange(subitem.Values);
                        unit += ";" + subitem.Unit;
                    }
                }
            }
            return new WeatherItem
            {
                Name = label.Replace(" ", ""),
                Description = $"{ label} [{unit}]",
                Values = values.ToArray(),
                Unit = unit,
            };
        }
    }

    public class WeatherItem
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public double[] Values { get; set; }

        public string Unit { get; set; }
    }
}
