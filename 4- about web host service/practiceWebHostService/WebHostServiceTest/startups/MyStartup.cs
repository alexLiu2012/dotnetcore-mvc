using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace WebHostServiceTest
{
    public class MyStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<WebService>();
        }

        public void Configure(IApplicationBuilder builder)
        {

        }
    }
}
