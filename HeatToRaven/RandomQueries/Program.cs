// See https://aka.ms/new-console-template for more information
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Queries;
using Raven.Client.Documents.Queries.TimeSeries;
using TelemetryToRaven;

Console.WriteLine("Hello, World!");
var store = new DocumentStore()
{
    Database = "Eiland17Logging",
    Urls = new[] { "http://tinkerboard:8080" }
};
store.Initialize();
var session = store.OpenSession();
string documentId = "meters/ISK5\\2M550T-1012";
string tsName = "PowerPerPhase";
var doc = session.Load<Meter>(documentId);
var query =
        (IRavenQueryable<TimeSeriesRawResult>)session.Query<Meter>()
        .Where(c => c.Id == documentId)
        .Select(q => RavenQuery.TimeSeries(q, tsName)
            .Where(ts => ts.Tag == "kW")
            .ToList()
         );

var result = query.Single();
var ts = session.TimeSeriesFor(doc, tsName);
using (var tsStream = result.Stream)
{
    while (tsStream.MoveNext())
    {
        var entry = tsStream.Current;
        ts.Append(entry.Timestamp, entry.Values.Select(v => v * 1000), "W");
        Console.Write(".");
    }
}
session.SaveChanges();

Console.WriteLine("The world is now a better place.");