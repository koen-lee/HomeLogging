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
/*
await store.TimeSeries.RegisterAsync<Meter>("HeatEnergy", new[] { "HeatEnergy [kWh]" });
await store.TimeSeries.RegisterAsync<Meter>("FlowTemperature", new[] { "Flow temperature [°C]" });
await store.TimeSeries.RegisterAsync<Meter>("ReturnTemperature", new[] { "Return temperature [°C]" });
await store.TimeSeries.RegisterAsync<Meter>("VolumeFlow", new[] { "Volume flow [m³/h]" });*/
await store.TimeSeries.RegisterAsync<Meter>("OutsideTemp", new[] { "Outside temperature [°C]" });
await store.TimeSeries.RegisterAsync<Meter>("DesiredFlowTemperature", new[] { "Flow setpoint [°C]" });
await store.TimeSeries.RegisterAsync<Meter>("RoomTemperature", new[] { "Room temperature [°C]" });
await store.TimeSeries.RegisterAsync<Meter>("DesiredRoomTemperature", new[] { "Room setpoint [°C]" });
await store.TimeSeries.RegisterAsync<Meter>("Modulation", new[] { "Modulation [%]" });
session.SaveChanges();

Console.WriteLine("The world is now a better place.");