using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HostTest
{
    public class TestConfigureHost
    {

        [Fact]
        public void TestAddHostConfiguration()
        {
            var configs = new Dictionary<string, string>()
            {
                { "config1", "value1" },
                { "config2", "value2" },
                { "config3", "value3" }
            };

            var host = new HostBuilder().ConfigureHostConfiguration(config => config.AddInMemoryCollection(configs)).Build();

            var config1 = host.Services.GetService<IConfiguration>()?.GetValue<string>("config1");
            Assert.Equal("value1", config1);
        }


        [Fact]
        public void TestAddAppConfiguration()
        {
            var configs = new Dictionary<string, string>()
            {
                { "config1", "value1" },
                { "config2", "value2" },
                { "config3", "value3" }
            };

            var host = new HostBuilder().ConfigureAppConfiguration(config => config.AddInMemoryCollection(configs)).Build();

            var config2 = host.Services.GetService<IConfiguration>()?.GetValue<string>("config2");
            Assert.Equal("value2", config2);
        }


        [Fact]
        public void TestEnvironment()
        {
            // by default, environment is "Production"
            var host1 = new HostBuilder().Build();
            var env1 = host1.Services.GetService<IHostEnvironment>();
            Assert.True(env1.IsProduction());

            // set environment
            var host2 = new HostBuilder().UseEnvironment(Environments.Staging).Build();
            var env2 = host2.Services.GetService<IHostEnvironment>();
            Assert.True(env2.IsStaging());            
        }


        [Fact]
        public void TestAddService()
        {            
            var host = new HostBuilder().ConfigureServices(services =>
                {
                    // common service
                    services.AddSingleton<DemoService>();
                    // hosted service
                    services.AddHostedService<DemoHostedService>();
                }).Build();

            var service = host.Services.GetService<DemoService>();
            Assert.NotNull(service);

            var hostedServices = host.Services.GetServices<IHostedService>();
            Assert.True(hostedServices.Where(h => h.GetType().IsAssignableFrom(typeof(DemoHostedService))).Any());            
        }
        


        [Fact]
        public void TestUseServiceProviderFactory()
        {
            var host = new HostBuilder().UseServiceProviderFactory(new MyServiceProviderFactory()).Build();

            var env = host.Services.GetService<IHostEnvironment>();
            Assert.True(env.IsProduction());
        }        
    }
}
