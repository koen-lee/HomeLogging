using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TelemetryToRaven
{
    public abstract class LoggerService : BackgroundService
    {
        protected readonly ILogger _logger;
        protected readonly IDocumentStore _store;

        public LoggerService(ILogger logger, IDocumentStore database)
        {
            _logger = logger;
            _store = database;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            TimeSpan delay = TimeSpan.FromMinutes(1);

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                try
                {
                    var timer = Stopwatch.StartNew();
                    await DoWork(stoppingToken);
                    _logger.LogDebug($"Work took {timer.Elapsed}");
                    delay = TimeSpan.FromMinutes(1) - timer.Elapsed;
                    if (delay < TimeSpan.FromSeconds(5)) delay = TimeSpan.FromSeconds(5);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Task failed");
                    if (delay < TimeSpan.FromHours(1))
                        delay = delay.Add(delay);
                }
                await Task.Delay(delay, stoppingToken);
            }
        }

        protected abstract Task DoWork(CancellationToken stoppingToken);

        protected string RunScript(string scriptname)
        {
            var path = Path.Combine(Environment.GetEnvironmentVariable("LOGSCRIPTDIR") ?? "/etc/telemetry", scriptname);
            _logger.LogInformation($"Running {path}");
            var scriptInfo = new ProcessStartInfo
            {
                FileName = path,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            var toRun = Process.Start(scriptInfo);

            toRun.WaitForExit();
            string output = toRun.StandardOutput.ReadToEnd();
            if (toRun.ExitCode != 0)
                _logger.LogWarning($"Exit code {toRun.ExitCode}");
            _logger.LogDebug($"Output:\n{output}");
            string err = toRun.StandardError.ReadToEnd();
            if (!string.IsNullOrEmpty(err))
                _logger.LogWarning($"Err:\n{err}");
            return output;
        }
    }
}