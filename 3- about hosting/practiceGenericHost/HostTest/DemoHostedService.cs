using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace HostTest
{
    public class DemoHostedService : IHostedService
    {
        private readonly ILogger _logger;

        public DemoHostedService(ILogger<DemoHostedService> logger)
        {
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("demo hosted service started");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("demo hosted service stopped");
            return Task.CompletedTask;
        }
    }
}
