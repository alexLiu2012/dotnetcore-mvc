using Microsoft.Extensions.DependencyInjection;
using System;

namespace customSPBuilderTest
{
    public class MyServiceBuilder
    {
        private IServiceCollection _services;

        public MyServiceBuilder(IServiceCollection services)
        {
            _services = services;
        }

        public IServiceProvider BuildServiceProvider()
        {
            return _services.BuildServiceProvider();
        }

        public MyServiceBuilder AddService<TSrv, TImpl>() where TSrv:class where TImpl : class
        {
            var instatnce = Activator.CreateInstance<TImpl>();

            var descriptor = new ServiceDescriptor(typeof(TSrv), typeof(TImpl), ServiceLifetime.Transient);
            _services.Add(descriptor);

            return this;
        }
    }
}
