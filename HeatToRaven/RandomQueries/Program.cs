// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Logging.Abstractions;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Queries;
using Raven.Client.Documents.Queries.TimeSeries;
using TelemetryToRaven;
using TelemetryToRaven.Weewx;

Console.WriteLine("Hello, World!");
var store = new DocumentStore()
{
    Database = "Eiland17Logging",
    Urls = new[] { "http://tinkerboard:8080" }
};
store.Initialize();
var session = store.OpenSession();

var underTest = new WeewxLogger(new NullLogger<WeewxLogger>(), store);
await underTest.DoSpiderPage(@"file://C:\Users\kpvle\Documents\GitHub\HomeLogging\TelemetryToRaven\Weewx\index.html");

Console.WriteLine("The world is now a better place.");