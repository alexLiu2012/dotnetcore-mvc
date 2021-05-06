using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace WebHostServiceTest
{
    public class MyStartupByInterface : IStartup
    {
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<WebService>();
            return services.BuildServiceProvider();
        }

        public void Configure(IApplicationBuilder app)
        {                      
        }        
    }
}
