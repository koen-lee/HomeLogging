using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Raven.Client.Documents;
using System;
using System.Threading.Tasks;
using Raven.Client.Documents.Conventions;

namespace ZyCO2
{
    public static class Program
    {
        public static async Task Main(
           string serverurl = null, string database = null, string enabledServices = null)
        {
            Console.WriteLine("Hello");
            serverurl ??= Environment.GetEnvironmentVariable("RAVENDB_URL") ?? "http://tinkerboard:8080;http://raspberrypi:8080";
            database ??= Environment.GetEnvironmentVariable("RAVENDB_DATABASE") ?? "telemetry";
            enabledServices ??= Environment.GetEnvironmentVariable("ENABLED_SERVICES");
            IHost host = Host.CreateDefaultBuilder()
                            .ConfigureServices(services =>
                            {
                                AddHostedServiceWhenEnabled<ZGm053Service>(services, enabledServices);
                                services.AddSingleton(CreateDocumentStore(serverurl, database));
                            })
                            .Build();

            await host.RunAsync();
        }

        private static void AddHostedServiceWhenEnabled<T>(IServiceCollection services, string enabledServices) where T : class, IHostedService
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
            store.Conventions.FindCollectionName = type =>
            {
                if (typeof(Meter).IsAssignableFrom(type))
                    return "Meters";

                return DocumentConventions.DefaultGetCollectionName(type);
            };
            store.Initialize();
            return store;
        }
    }

    public record Meter
    {
        public string Id { get; set; }
        public string VendorInfo { get; set; }
        public string Medium { get; set; }
    }
}
