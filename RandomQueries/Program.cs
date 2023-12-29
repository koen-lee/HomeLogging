using Raven.Client.Documents;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Queries;
using Raven.Client.Documents.Queries.TimeSeries;
using TelemetryToRaven;
using TelemetryToRaven.Gpio;

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
string documentId = "WaterMeter";
string tsName = "Water";
var doc = session.Load<GpioMeter>(documentId);
var lastValues = session.Query<Meter>()
    .Where(c => c.Id == documentId)
    .Select(q => RavenQuery.TimeSeries(q, tsName)
        .Select(x => x.Count()).ToList()
    ).Single();

var count = (int)lastValues.Results.Single().Count[0];

var rawLast = session.TimeSeriesFor(documentId, tsName).Get(start: count - 1);

var offset = rawLast.Last();
Console.WriteLine($"Entity {doc.Id} last {offset.Value} ts {offset.Timestamp}");
Console.WriteLine("The world is now a better place.");