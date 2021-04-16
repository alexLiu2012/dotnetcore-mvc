using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using serviceAndImpl;
using System;
using System.Linq;
using Xunit;

namespace dependencyInjectTest
{
    public class SingletonServiceTest
    {
        public IServiceCollection ServiceCollection { get; }
        public IServiceProvider Services { get; private set; }

        public SingletonServiceTest()
        {
            // create service collection
            ServiceCollection = new ServiceCollection();
            
            // inject services
            ServiceCollection.AddSingleton<IDummySingletonService, DummySingletonService>();
            ServiceCollection.AddScoped<IDummyScopedService, DummyScopedService>();
            ServiceCollection.AddTransient<IDummyTransientService, DummyTransientService>();

            // build service provider
            Services = ServiceCollection.BuildServiceProvider();
        }

        [Fact]
        public void TestGetServiceByServiceType()
        {
            // get service implementation by "get service t" method with service type (interface)
            // will NOT throw any exception if no service got (not injected before)
            //
            // can be also by "get required method t",
            // will throw exception if no service got (not injected before)

            var singletonService = Services.GetService<IDummySingletonService>();
            Assert.NotNull(singletonService);
           
        }

        [Fact]
        public void TestServiceIsSingleton()
        {
            var service1 = Services.GetService<IDummySingletonService>();
            var service2 = Services.GetService<IDummySingletonService>();

            Assert.True(service1 == service2);
        }

        [Fact]
        public void TestGetServiceByImplType()
        {
            // can NOT get service implementation by "get service t" method with service implementation type (class)
            // 
            // will NOT throw any exception by "get service t" method,
            // 
            // will throw exception by "get required service t" method

            var singletonService2 = Services.GetService<DummySingletonService>();
            Assert.Null(singletonService2);

            Assert.Throws<InvalidOperationException>(() =>
            {
                var _ = Services.GetRequiredService<DummySingletonService>();
            });
        }

        [Fact]
        public void TestAddServiceMultitimes()
        {
            // add service multitimes are accepted,
            // variety impl can be gotten by "get services" method     
            //
            // "get service" mothed will brings the lasted service imple

            var services_pre = Services;

            // inject another impl of "singleton service" (dummy singleton service impl) and build service provider
            ServiceCollection.AddSingleton<IDummySingletonService, DummySingletonServiceImpl>();
            Services = ServiceCollection.BuildServiceProvider();

            // get "singleton service", will be the latest impl injected
            var service = Services.GetService<IDummySingletonService>();
            Assert.True(service.GetType().IsAssignableFrom(typeof(DummySingletonServiceImpl)));

            // multi impl had been injected to the di
            var servicesCount = Services.GetServices<IDummySingletonService>().Count();
            Assert.True(servicesCount > 1);

            Services = services_pre;                       
        }

        [Fact]
        public void TestTryAddService()
        {
            // "try add service" method will not inject service type which had been injected already
            // and will NOT throw any exception
            //
            // it can make sure that the service had unique impl type

            var services_pre = Services;

            // try add singleton & build service provider
            ServiceCollection.TryAddSingleton<IDummySingletonService, DummySingletonServiceImpl>();
            Services = ServiceCollection.BuildServiceProvider();

            // get service (not injected the new impl)
            var service = Services.GetService<IDummySingletonService>();
            Assert.True(service.GetType().IsAssignableFrom(typeof(DummySingletonService)));

            // no impl with same service type injected
            var servicesCount = Services.GetServices<IDummySingletonService>().Count();
            Assert.True(servicesCount == 1);

            Services = services_pre;           
        }        
    }
}
