using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Raven.Client.Documents;
using System;
using System.Threading.Tasks;
using TelemetryToRaven;
using TelemetryToRaven.Goodwe;
using TelemetryToRaven.Mbus;
using TelemetryToRaven.P1;
using TelemetryToRaven.Sdm;

namespace TelemetryToRaven
{
    public static class Program
    {
        public static async Task Main(
           string serverurl = null, string database = null, string enabledServices = null)
        {
            Console.WriteLine("Hello");
            serverurl ??= Environment.GetEnvironmentVariable("RAVENDB_URL") ?? "http://localhost:8080";
            database ??= Environment.GetEnvironmentVariable("RAVENDB_DATABASE") ?? "telemetry";
            enabledServices ??= Environment.GetEnvironmentVariable("ENABLED_SERVICES");
            IHost host = Host.CreateDefaultBuilder()
                            .ConfigureServices(services =>
                            {
                                AddHostedServiceWhenEnabled<EbusLogger>(services, enabledServices);
                                AddHostedServiceWhenEnabled<MbusLogger>(services, enabledServices);
                                AddHostedServiceWhenEnabled<GoodweLogger>(services, enabledServices);
                                AddHostedServiceWhenEnabled<P1Logger>(services, enabledServices);
                                AddHostedServiceWhenEnabled<SdmLogger>(services, enabledServices);
                                services.AddSingleton(CreateDocumentStore(serverurl, database));
                            })
                            .Build();

            await host.RunAsync();
        }

        private static void AddHostedServiceWhenEnabled<T>(IServiceCollection services, string enabledServices) where T : LoggerService
        {
            if (enabledServices == null || enabledServices.Contains(typeof(T).Name))
            {
                services.AddHostedService<T>();
            }
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
