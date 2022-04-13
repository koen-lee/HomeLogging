using Raven.Client.Documents;
using TelemetryToRaven;
using TelemetryToRaven.Goodwe;
using TelemetryToRaven.P1;
using TelemetryToRaven.Sdm;

namespace TelemetryToRaven
{
    public static class Program
    {
        public static async Task Main(
           string serverurl = "http://energylogger:8080", string database = "Eiland17Logging")
        {
            Console.WriteLine("Hello");
            IHost host = Host.CreateDefaultBuilder()
                            .ConfigureServices(services =>
                            {
                                //services.AddHostedService<EbusLogger>();
                                //services.AddHostedService<MbusLogger>();
                                services.AddHostedService<GoodweLogger>();
                                services.AddHostedService<SdmLogger>();
                                services.AddHostedService<P1Logger>();
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
