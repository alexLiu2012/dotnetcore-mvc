using Microsoft.Extensions.DependencyInjection;
using serviceAndImpl;
using Xunit;

namespace customSPBuilderTest
{
    public class CustomServiceBuilderTest
    {
        public MyServiceBuilder ServiceBuilder { get; }
        public IServiceProviderFactory<MyServiceBuilder> ServiceFactory { get; }


        public CustomServiceBuilderTest()
        {
            ServiceBuilder = new MyServiceBuilder(new ServiceCollection());
            ServiceFactory = new MyServiceProviderFactory();
        }
        
        [Fact]
        public void TestCustomServiceResolve()
        {
            ServiceBuilder.AddService<IDummySingletonService, DummySingletonService>();
            var services = ServiceFactory.CreateServiceProvider(ServiceBuilder);

            var srv = services.GetService<IDummySingletonService>();
            Assert.NotNull(srv);

            var srv2 = services.GetService<IDummyTransientService>();
            Assert.Null(srv2);
        }
    }
}
