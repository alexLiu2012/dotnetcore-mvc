using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;


[assembly: HostingStartup(typeof(HostingStartupLibrary.HostStartup1))]
namespace HostingStartupLibrary
{
    public class HostStartup1 : IHostingStartup
    {
        public void Configure(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services => services.AddSingleton<HostStartupService1>());
        }
    }
}
