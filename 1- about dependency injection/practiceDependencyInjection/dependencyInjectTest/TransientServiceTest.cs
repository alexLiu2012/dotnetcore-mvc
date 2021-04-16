using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using serviceAndImpl;
using System;
using System.Linq;
using Xunit;

namespace dependencyInjectTest
{
    public class TransientServiceTest
    {
       
        public IServiceCollection ServiceCollection { get; }
        public IServiceProvider Services { get; private set; }

        public TransientServiceTest()
        {
            // create service collection
            ServiceCollection = new ServiceCollection();

            // inject services           
            ServiceCollection.AddTransient<IDummyTransientService, DummyTransientService>();

            // build service provider
            Services = ServiceCollection.BuildServiceProvider();
        }

        [Fact]
        public void TestGetServiceTransient()
        {
            var service1 = Services.GetService<IDummyTransientService>();
            var service2 = Services.GetService<IDummyTransientService>();

            Assert.True(service1 != service2);
        }

        [Fact]
        public void TestAddTransientWithFunc()
        {
            // "add transient" method will always inject the impl func (object) 

            var services_pre = Services;

            var impl1 = new DummyTransientServiceImpl();
            var impl2 = new DummyTransientServiceImpl();

            // inject impl func
            ServiceCollection.AddTransient<IDummyTransientService>(factory => impl1);
            ServiceCollection.AddTransient<IDummyTransientService>(factory => impl2);
            Services = ServiceCollection.BuildServiceProvider();

            // get service (got the latest impl)
            var service = Services.GetService<IDummyTransientService>();
            Assert.True(service == impl2);

            // all impl func had been injected to the di 
            var count = Services.GetServices<IDummyTransientService>().Count();
            Assert.Equal(3, count);

            Services = services_pre;
        }

        [Fact]
        public void TestTryAddEnumerable()
        {
            // "try add enumerable" method will not inject the service impl if the same impl had been injected already
            // will NOT throw any exception
            
            var services_pre = Services;

            var impl1 = new DummyTransientServiceImpl();
            var impl2 = new DummyTransientServiceImpl();

            // inject impl func
            ServiceCollection.TryAddEnumerable(new ServiceDescriptor(typeof(IDummyTransientService), impl1));
            ServiceCollection.TryAddEnumerable(new ServiceDescriptor(typeof(IDummyTransientService), impl2));
            Services = ServiceCollection.BuildServiceProvider();

            // get service (got the impl with 1st func, the 2nd didn't injected)
            var service = Services.GetService<IDummyTransientService>();
            Assert.True(service == impl1);

            // only 1st func injected
            var count = Services.GetServices<IDummyTransientService>().Count();
            Assert.Equal(2, count);

            Services = services_pre;
        }
    }
}
