using InfluxDB.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading.Tasks;
using WeewxToInflux.Weewx;

namespace WeewxToInflux
{
    public static class Program
    {
        public static async Task Main(
           string serverurl = null, string database = null, string username = null, string password = null, string enabledServices = null)
        {
            Console.WriteLine("Hello");
            serverurl ??= Environment.GetEnvironmentVariable("INFLUXDB_URL") ?? "http://localhost:8086";
            database ??= Environment.GetEnvironmentVariable("INFLUXDB_DATABASE") ?? "appptemp";
            username ??= Environment.GetEnvironmentVariable("INFLUXDB_USERNAME") ?? "appptempuser1";
            password ??= Environment.GetEnvironmentVariable("INFLUXDB_PASSWORD") ?? throw new InvalidOperationException("Missing password");
            enabledServices ??= Environment.GetEnvironmentVariable("ENABLED_SERVICES");
            IHost host = Host.CreateDefaultBuilder()
                            .ConfigureServices(services =>
                            {
                                AddHostedServiceWhenEnabled<WeewxLogger>(services, enabledServices);
                                services.AddSingleton(CreateDocumentStore(serverurl, database, username, password));
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

        private static InfluxDBClient CreateDocumentStore(string serverUrl, string database, string username, string password)
        {
            var store = new InfluxDBClient(serverUrl, username, password, database, "default");
            return store;
        }
    }
}
