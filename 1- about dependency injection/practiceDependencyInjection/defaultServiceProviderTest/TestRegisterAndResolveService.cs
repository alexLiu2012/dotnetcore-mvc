using Microsoft.Extensions.DependencyInjection;
using serviceAndImpl;
using System;
using Xunit;

namespace defaultServiceProviderTest
{
    public class TestRegisterAndResolveService
    {
        public IServiceCollection ServiceCollection { get; }
        public IServiceProvider ServiceProvider { get; private set; }


        public TestRegisterAndResolveService()
        {
            ServiceCollection = new ServiceCollection();
        }


        [Fact]
        public void TestResoveSingleton()
        {
            ServiceCollection.Clear();

            // register singleton service & build service provider
            ServiceCollection.AddSingleton<IDummySingletonService, DummySingletonService>();
            ServiceProvider = ServiceCollection.BuildServiceProvider();

            // get service            
            var service1 = ServiceProvider.GetService<IDummySingletonService>();
            Assert.NotNull(service1);

            // get required service
            var service2 = ServiceProvider.GetRequiredService<IDummySingletonService>();
            Assert.NotNull(service2);

            // singleton servie will always get the same instance
            Assert.Equal(service1, service2);            
        }


        [Fact]
        public void TestResovleTransient()
        {
            ServiceCollection.Clear();

            // register transient service & build service provider
            ServiceCollection.AddTransient<IDummyTransientService, DummyTransientService>();
            ServiceProvider = ServiceCollection.BuildServiceProvider();

            // get service 
            var service1 = ServiceProvider.GetService<IDummyTransientService>();
            Assert.NotNull(service1);

            // get required service
            var service2 = ServiceProvider.GetRequiredService<IDummyTransientService>();
            Assert.NotNull(service2);

            // transient service will always get NEW instance
            Assert.NotEqual(service1, service2);
        }


        [Fact]
        public void TestResolveScoped()
        { 
            ServiceCollection.Clear();

            // register scope service & build service provider
            ServiceCollection.AddScoped<IDummyScopedService, DummyScopedService>();
            ServiceProvider = ServiceCollection.BuildServiceProvider();

            // get service 
            var service1 = ServiceProvider.GetService<IDummyScopedService>();
            Assert.NotNull(service1);

            // get required service
            var service2 = ServiceProvider.GetRequiredService<IDummyScopedService>();
            Assert.NotNull(service2);

            // scope service will always get the same instance for specific scope (similar as singlton but with scope)     
            
            // without "create scope", services got were all with root scope, they are same
            Assert.Equal(service1, service2);

            // with scopes created, instances are different            
            var srv1 = ServiceProvider.CreateScope().ServiceProvider.GetService<IDummyScopedService>();            
            Assert.NotNull(srv1);

            var srv2 = ServiceProvider.CreateScope().ServiceProvider.GetService<IDummyScopedService>();
            Assert.NotNull(srv2);

            Assert.NotEqual(srv1, srv2);
        }



        [Fact]
        public void TestResolveServiceFailure()
        {
            ServiceCollection.Clear();

            // build service provider
            ServiceProvider = ServiceCollection.BuildServiceProvider();

            // get service failure (not registered) -> return null
            var service1 = ServiceProvider.GetService<IDummyService>();
            Assert.Null(service1);

            // get required serviced failure (not registered) -> thrown exception            
            Action action = () => ServiceProvider.GetRequiredService<IDummyService>();
            Assert.ThrowsAny<Exception>(action);
        }
    }
}
