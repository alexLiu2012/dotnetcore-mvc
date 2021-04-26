using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using serviceAndImpl;
using System;
using System.Linq;
using Xunit;

namespace defaultServiceProviderTest
{
    public class TestAddMultipleService
    {
        public IServiceCollection ServiceCollection { get; }
        public IServiceProvider ServiceProvider { get; private set; }


        public TestAddMultipleService()
        {
            ServiceCollection = new ServiceCollection();
        }


        [Fact]
        public void TestAdd()
        {
            ServiceCollection.Clear();

            // register service multi times with different impl types by normal "add" method,
            // all types will be registered to the di, get the specific impl instance by filter the services
            ServiceCollection.AddSingleton<IDummyService, DummyService>();
            ServiceCollection.AddSingleton<IDummyService, DummyServiceImpl>();

            ServiceProvider = ServiceCollection.BuildServiceProvider();


            // all impl types registered
            var service1 = ServiceProvider.GetServices<IDummyService>().Where(srv => srv.GetType().IsAssignableFrom(typeof(DummyService))).FirstOrDefault();
            Assert.NotNull(service1);

            var service2 = ServiceProvider.GetServices<IDummyService>().Where(srv => srv.GetType().IsAssignableFrom(typeof(DummyServiceImpl))).FirstOrDefault();
            Assert.NotNull(service2);

            // "get service" will return the latest registered impl type
            var service = ServiceProvider.GetService<IDummyService>();
            Assert.False(service.GetType().IsAssignableFrom(typeof(DummyService)));
            Assert.True(service.GetType().IsAssignableFrom(typeof(DummyServiceImpl)));
        }


        [Fact]
        public void TestTryAdd()
        {
            ServiceCollection.Clear();

            // register service multi times with different impl types by "try add" method,
            // impl types will NOT be registerd to the di, if the service type had already been registered before
            ServiceCollection.TryAddSingleton<IDummyService, DummyService>();
            ServiceCollection.TryAddSingleton<IDummyService, DummyServiceImpl>();

            ServiceProvider = ServiceCollection.BuildServiceProvider();

            var service1 = ServiceProvider.GetServices<IDummyService>().Where(srv => srv.GetType().IsAssignableFrom(typeof(DummyService))).FirstOrDefault();
            Assert.NotNull(service1);

            // dummy singleton service impl was NOT registered
            var service2 = ServiceProvider.GetServices<IDummyService>().Where(srv => srv.GetType().IsAssignableFrom(typeof(DummyServiceImpl))).FirstOrDefault();
            Assert.Null(service2);
        }


        [Fact]
        public void TestAddEnumerable()
        {
            ServiceCollection.Clear();

            // register service multi times with different impl types by "add enumerable" method,
            // impl types will NOT be registered to the di, if the impl type had already been registered before
            var impl = new DummyServiceImpl();
            var impl1 = new DummyService();
            var impl2 = new DummyService();

            // register dummy service with "DummyServiceImpl"
            ServiceCollection.TryAddEnumerable(new ServiceDescriptor(typeof(IDummyService), impl));
            // register dummy service with "DummyService"
            ServiceCollection.TryAddEnumerable(new ServiceDescriptor(typeof(IDummyService), impl1));
            // another "DummyService" instance will not be registered as the same impl type "DummyService" had already been registered!
            ServiceCollection.TryAddEnumerable(new ServiceDescriptor(typeof(IDummyService), impl2));

            ServiceProvider = ServiceCollection.BuildServiceProvider();

            // impl had been registered with impl type "DummyServiceImpl"
            var srvImpl = ServiceProvider.GetServices<IDummyService>().Where(srv => srv.GetType().IsAssignableFrom(typeof(DummyServiceImpl))).FirstOrDefault();
            Assert.Equal(impl, srvImpl);

            // impl1 had been registered with impl type "DummyService"
            // impl2 was not registered as the same impl type "DummyService" instance had been registered before (impl1)

            // so only 1 srv got
            var count = ServiceProvider.GetServices<IDummyService>().Where(srv => srv.GetType().IsAssignableFrom(typeof(DummyService))).Count();
            Assert.Equal(1, count);
            // and it is impl1            
            var srv = ServiceProvider.GetServices<IDummyService>().Where(srv => srv.GetType().IsAssignableFrom(typeof(DummyService))).FirstOrDefault();
            Assert.Equal(impl1, srv);
        }
    }
}
