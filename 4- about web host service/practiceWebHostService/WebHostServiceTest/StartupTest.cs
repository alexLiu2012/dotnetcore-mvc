using HostingStartupLibrary;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using WebHostServiceTest.startups;
using Xunit;

namespace WebHostServiceTest
{
    public class StartupTest
    {
        // configure startup class 
        [Fact]
        public void TestUseStartup()
        {
            var host = Host
                .CreateDefaultBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseStartup<MyStartup>();
                })
                .Build();

            // "web service" configured in "my startup"
            var service = host.Services.GetService<WebService>();
            Assert.NotNull(service);
        }


        // NOT support "configure services" method with return "service provider", -> throw exception
        [Fact]
        public void TestUseStartupWithReturnSP()
        {
            Action action = () => Host.CreateDefaultBuilder()
                                      .ConfigureWebHost(builder =>
                                      {
                                          builder.UseStartup<MyStartupReturnSP>();
                                      })
                                      .Build();

            Assert.ThrowsAny<Exception>(action);
        }


        // "configure services" method (return void) is optional, 
        // without such method in "startup instance" will be OK
        [Fact]
        public void TestUseStartupWithoutConfigureServicesMethod()
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseStartup<MyStartupNoConfigureServiceMethod>();
                })
                .Build();

            var service = host.Services.GetService<WebService>();
            Assert.Null(service);
        }


        // "configure" method must be defined in "startup instance", to build the application builder,
        // without such method will throw exception.
        [Fact]
        public void TestStartupWithoutConfigureMethod()
        {
            Action action = () => Host.CreateDefaultBuilder()
                                      .ConfigureWebHost(builder =>
                                      {
                                          builder.UseStartup<MyStartupNoConfigureMethod>();                                          
                                      })                                      
                                      .Build();

            Assert.ThrowsAny<Exception>(action);
        }


        // configure the startup by assembly name
        [Fact]
        public void TestStartupByAssemblyName()
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureWebHost(builder => 
                {
                    // xxx, version=...
                    builder.UseStartup(typeof(Startup).Assembly.FullName);
                                       
                    // human readable assembly name 
                    /* Assembly.GetAssembly(typeof(Startup)).GetName().Name*/
                })
                .Build();            

            var service = host.Services.GetService<WebServiceDefault>();
            Assert.NotNull(service);
        }


        // istartup is obsolute interface for normal IWebHostBuilder,
        // not support for "generic web service builder", will throw exception if the "startup instance" implemente such interface
        [Fact]
        public void TestUseStartupInterface()
        {
            Action action = () => Host
                .CreateDefaultBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseStartup<MyStartupByInterface>();
                })
                .Build();

            Assert.ThrowsAny<Exception>(action);
        }


        // "entry assembly" will be the default assembly,
        // "hosting startup class" will NOT be loaded even defined in the main program assembly
        [Fact]
        public void TestHostingStartupMainAssembly()
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureWebHost(builder =>
                {
                    // if nothing configured for "hosting startup assembly", 
                    // it will inject the "entry assembly" as the assembly to load "startup class" and "hosting startup class"
                })
                .Build();

            var service = host.Services.GetService<HostingStartupService>();
            Assert.Null(service);
        }


        // startup by "hosting startup class"
        [Fact]
        public void TestHostingStartupAssemblyDeclared()
        {
            // set assembly of hosting startup class
            var host = Host.CreateDefaultBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseSetting(WebHostDefaults.HostingStartupAssembliesKey, typeof(HostStartupService1).Assembly.FullName);
                })
                .Build();

            // service will be injected by "hosting startup class" with "hosting startup attribute"
            var service1 = host.Services.GetService<HostStartupService1>();
            Assert.NotNull(service1);

            // service will NOT be injected by "hosting startup class" IF without "hosting startup attribute"
            var service2 = host.Services.GetService<HostStartupService2>();
            Assert.Null(service2);
        }
    }
}
