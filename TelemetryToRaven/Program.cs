using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Raven.Client.Documents;
using System;
using System.Threading.Tasks;
using TelemetryToRaven;
using TelemetryToRaven.Goodwe;
using TelemetryToRaven.P1;
using TelemetryToRaven.Sdm;

namespace TelemetryToRaven
{
    public static class Program
    {
        public static async Task Main(
           string serverurl = null, string database = null)
        {
            Console.WriteLine("Hello");
            serverurl ??= Environment.GetEnvironmentVariable("RAVENDB_URL") ?? "http://localhost:8080";
            database ??= Environment.GetEnvironmentVariable("RAVENDB_DATABASE") ?? "telemetry";
            IHost host = Host.CreateDefaultBuilder()
                            .ConfigureServices(services =>
                            {
                                //services.AddHostedService<EbusLogger>();
                                //services.AddHostedService<MbusLogger>();
                                services.AddHostedService<GoodweLogger>();
                                services.AddHostedService<P1Logger>();
                                //services.AddHostedService<SdmLogger>();
                                services.AddSingleton(CreateDocumentStore(serverurl, database));
                            })
                            .Build();

            await host.RunAsync();
        }

        private static IDocumentStore CreateDocumentStore(string serverUrl, string database)
        {
            var store = new DocumentStore()
            {
                Database = database,
                Urls = new[] { serverUrl }
            };
            store.Initialize();
            return store;
        }
    }
}
