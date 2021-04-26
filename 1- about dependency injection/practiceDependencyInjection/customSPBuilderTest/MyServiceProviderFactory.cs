using Microsoft.Extensions.DependencyInjection;
using System;

namespace customSPBuilderTest
{
    public class MyServiceProviderFactory : IServiceProviderFactory<MyServiceBuilder>
    {
        public MyServiceBuilder CreateBuilder(IServiceCollection services)
        {
            throw new NotImplementedException();
        }

        public IServiceProvider CreateServiceProvider(MyServiceBuilder containerBuilder)
        {
            return containerBuilder.BuildServiceProvider();
        }
    }
}
