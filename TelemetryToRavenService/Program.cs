namespace TelemetryToRavenService
{
    public class Program
    {
        public static async Task Main(
              string host = "192.168.2.255",
              int timeout = 1000,
              string serverurl = "http://energylogger:8080", string database = "Eiland17Logging")
        {
            var builder = Application.CreateBuilder(args);

             
            var app = builder.Build();
                        app.Run();

        }
    }
}

