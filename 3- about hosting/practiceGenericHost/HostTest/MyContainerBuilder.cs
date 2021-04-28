using Microsoft.Extensions.DependencyInjection;
using System;

namespace HostTest
{
    public class MyContainerBuilder
    {
        private IServiceCollection _services;

        public MyContainerBuilder()
        {
            _services = new ServiceCollection();
        }

        public void Populate(IServiceCollection services)
        {
            foreach (var item in services)
            {
                _services.Add(item);
            }
        }

        public IServiceProvider Build()
        {
            return _services.BuildServiceProvider();
        }
    }
}
