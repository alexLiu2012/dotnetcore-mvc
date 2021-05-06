using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;


//[assembly: HostingStartup(typeof(HostingStartupLibrary.HostStartup2))]
namespace HostingStartupLibrary
{
    public class HostStartup2 : IHostingStartup
    {
        public void Configure(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services => services.AddSingleton<HostStartupService2>());

            builder.Configure(app => app.Use(request =>
            {
                Console.WriteLine(request.Method.Name);
                return request;
            }));
        }
    }
}
