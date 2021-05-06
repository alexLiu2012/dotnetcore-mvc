using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

[assembly: HostingStartup(typeof(WebHostServiceTest.startups.HostingStartup))]
namespace WebHostServiceTest.startups
{
    public class HostingStartup : IHostingStartup
    {
        public void Configure(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services => services.AddSingleton<HostingStartupService>());
        }
    }
}
