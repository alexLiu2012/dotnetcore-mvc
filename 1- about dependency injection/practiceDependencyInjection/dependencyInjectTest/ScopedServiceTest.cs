using Microsoft.Extensions.DependencyInjection;
using serviceAndImpl;
using System;
using Xunit;

namespace dependencyInjectTest
{
    public class ScopedServiceTest
    {
        public IServiceCollection ServiceCollection { get; }
        public IServiceProvider Services { get; private set; }

        public ScopedServiceTest()
        {
            // create service collection
            ServiceCollection = new ServiceCollection();

            // inject services            
            ServiceCollection.AddScoped<IDummyScopedService, DummyScopedService>();           

            // build service provider
            Services = ServiceCollection.BuildServiceProvider();
        }

        [Fact]
        public void TestGetServiceScoped()
        {
            // get scoped service will be singleton (default scope)
            var service1 = Services.GetService<IDummyScopedService>();
            var service2 = Services.GetService<IDummyScopedService>();
            Assert.True(service1 == service2);

            // services gotten from various service scope are different
            var service3 = Services.CreateScope().ServiceProvider.GetService<IDummyScopedService>();
            var service4 = Services.CreateScope().ServiceProvider.GetService<IDummyScopedService>();
            Assert.False(service3 == service4);
        }
    }
}
