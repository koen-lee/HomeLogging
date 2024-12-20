using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Raven.Client.Documents;
using System;
using System.Threading.Tasks;
using Raven.Client.Documents.Conventions;
using TelemetryToRaven.Goodwe;
using TelemetryToRaven.Kasa;
using TelemetryToRaven.Mbus;
using TelemetryToRaven.P1;
using TelemetryToRaven.Sdm;
using TelemetryToRaven.Weewx;
using TelemetryToRaven.Tapo;
using TelemetryToRaven.Vents;
using TelemetryToRaven.Gpio;

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
                                AddHostedServiceWhenEnabled<WeewxLogger>(services, enabledServices);
                                AddHostedServiceWhenEnabled<EbusRunExtender>(services, enabledServices);
                                AddHostedServiceWhenEnabled<EbusThermostatSwitcher>(services, enabledServices);
                                AddHostedServiceWhenEnabled<KasaLogger>(services, enabledServices);
                                AddHostedServiceWhenEnabled<TapoLogger>(services, enabledServices);
                                AddHostedServiceWhenEnabled<VentsMicraLogger>(services, enabledServices);
                                AddHostedServiceWhenEnabled<GpioLogger>(services, enabledServices);
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
                Urls = serverUrl.Split(';')
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
}
