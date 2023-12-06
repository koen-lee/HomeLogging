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

        protected TimeSpan Delay { get; set; }

        protected TimeSpan BaseInterval { get; set; } = TimeSpan.FromMinutes(1);

        public LoggerService(ILogger logger, IDocumentStore database)
        {
            _logger = logger;
            _store = database;
            Delay = BaseInterval;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                try
                {
                    CancellationToken timeoutToken = CreateLinkedTimeout(stoppingToken, 2 * BaseInterval);
                    var timer = Stopwatch.StartNew();
                    await DoWork(timeoutToken);
                    _logger.LogDebug($"Work took {timer.Elapsed}");
                    Delay = BaseInterval - timer.Elapsed;
                    if (Delay < TimeSpan.FromSeconds(5)) Delay = TimeSpan.FromSeconds(5);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Task failed");
                    if (Delay < TimeSpan.FromHours(1))
                        Delay = Delay.Add(Delay);
                }
                await Task.Delay(Delay, stoppingToken);
            }
        }

        private CancellationToken CreateLinkedTimeout(CancellationToken stoppingToken, TimeSpan time)
        {
            var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeout.CancelAfter(time);
            var timeoutToken = timeout.Token;
            return timeoutToken;
        }

        protected abstract Task DoWork(CancellationToken stoppingToken);

        protected async Task<T> Retry<T>(Func<Task<T>> errorProneCode)
        {
            try
            {
                return await errorProneCode();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Subtask failed, will retry");
                return await errorProneCode();
            }
        }
        protected string RunScript(string scriptname, string arguments = null)
        {
            var path = Path.Combine(Environment.GetEnvironmentVariable("LOGSCRIPTDIR") ?? "/etc/telemetry", scriptname);
            return RunCommand(path, arguments);
        }

        protected string RunCommand(string command, string arguments)
        {
            _logger.LogInformation($"Running {command} {arguments}");
            var scriptInfo = new ProcessStartInfo
            {
                FileName = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Arguments = arguments ?? string.Empty
            };
            var toRun = Process.Start(scriptInfo);

            if (!toRun.WaitForExit(50 * 1000))
                toRun.Kill();
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