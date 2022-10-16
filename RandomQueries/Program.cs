// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Logging.Abstractions;
using Raven.Client.Documents;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Queries;
using Raven.Client.Documents.Queries.TimeSeries;
using TelemetryToRaven;
using TelemetryToRaven.Kasa;
using TelemetryToRaven.Weewx;

Console.WriteLine("Hello, World!");

var store = new DocumentStore()
{
    Database = "Eiland17Logging",
    Urls = new[] { "http://tinkerboard:8080" }
};

store.Conventions.FindCollectionName = type =>
{
    if (typeof(Meter).IsAssignableFrom(type))
        return "Meters";

    return DocumentConventions.DefaultGetCollectionName(type);
};
store.Initialize();
var session = store.OpenSession();
string documentId = "meters/CookingBoiler";
string tsName = "PowerEnergy";
var from = DateTimeOffset.Parse("2022-10-11T11:15:16.6430000Z").UtcDateTime;
var doc = session.Load<KasaDevice>(documentId);
var lastValues = session.Query<Meter>()
    .Where(c => c.Id == documentId)
    .Select(q => RavenQuery.TimeSeries(q, tsName).Where(ts => ts.Timestamp < from)
        .GroupBy(b => b.Years(100))
        .Select(x => x.Last()).ToList()
    ).Single().Results.Single().Last;
var offset = lastValues[1];
Console.WriteLine($"Entity {doc.Id} offset {offset}");
var query = session.Query<Meter>()
    .Where(c => c.Id == documentId)
    .Select(q => RavenQuery.TimeSeries(q, tsName).Where(ts => ts.Timestamp >= @from)
        .ToList()
    );

var result = query.Single();

var ts = session.TimeSeriesFor(doc, tsName);
using (var tsStream = result.Stream)
{
    while (tsStream.MoveNext())
    {
        var entry = tsStream.Current;
        var rawReading = (entry.Values.Length == 2) ? entry.Values[1] : entry.Values[2];

        var newReading = Math.Round(rawReading + offset, 3);
        ts.Append(entry.Timestamp, new[] { entry.Values[0], newReading, rawReading }, entry.Tag);
        Console.Write(".");
    }
}

doc.EnergyOffset = offset;
session.Store(doc);
session.SaveChanges();
Console.WriteLine("The world is now a better place.");