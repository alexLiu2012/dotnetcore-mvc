using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace WebHostServiceTest
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<WebServiceDefault>();
        }

        public void Configure(IApplicationBuilder builder)
        {
        }
    }
}
