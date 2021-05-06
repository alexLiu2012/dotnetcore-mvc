using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace WebHostServiceTest
{
    public class MyStartupReturnSP
    {
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<WebService>();
            return services.BuildServiceProvider();
        }

        public void Configure(IApplicationBuilder builder)
        {
        }
    }
}
