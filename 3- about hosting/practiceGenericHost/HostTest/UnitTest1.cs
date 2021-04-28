using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Reflection;
using Xunit;

namespace HostTest
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            var hostBuilder = new HostBuilder();


            var fieldInfo = typeof(HostBuilder).GetField("_appServices", BindingFlags.NonPublic | BindingFlags.Instance);
            

            var host = hostBuilder.Build();
            var services = fieldInfo.GetValue(hostBuilder);
            var options = ((IServiceProvider)services).GetService<IOptions<ConsoleLifetimeOptions>>();

            host.Run();

            Console.WriteLine("started");
        }
    }
}
