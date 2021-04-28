using Microsoft.Extensions.DependencyInjection;
using System;

namespace HostTest
{
    public class MyServiceProviderFactory : IServiceProviderFactory<MyContainerBuilder>
    {
        public MyContainerBuilder CreateBuilder(IServiceCollection services)
        {
            var builder = new MyContainerBuilder();
            builder.Populate(services);

            return builder;
        }

        public IServiceProvider CreateServiceProvider(MyContainerBuilder containerBuilder)
        {
            return containerBuilder.Build();
        }
    }
}
