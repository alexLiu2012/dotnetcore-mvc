using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using Xunit;

namespace ResourceDealingMiddlewaresTest
{
    public class TestUseDirectoryBrowser
    {
        [Fact]
        public async Task UseDirectoryBrowserWithAllDefault()
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();
                    builder.ConfigureServices(services =>
                    {
                        services.AddDirectoryBrowser();
                    });

                    builder.Configure(app =>
                    {
                        app.UseDirectoryBrowser();                        
                    });
                })
                .Start();

            var resp1 = await host.GetTestClient().GetStringAsync("/");
            Assert.NotNull(resp1);

            var resp2 = await host.GetTestClient().GetStringAsync("/subdirectory/");
            Assert.NotNull(resp2);
        }


        [Fact]
        public async Task UseDirectoryBrowserWithReqeustPath()
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();
                    builder.ConfigureServices(services =>
                    {
                        services.AddDirectoryBrowser();
                    });

                    builder.Configure(app =>
                    {
                        app.UseDirectoryBrowser("/files");                        
                    });
                })
                .Start();

            // url to browse directory MUST be end with "/"
            var resp = await host.GetTestClient().GetStringAsync("/files/");
            Assert.NotNull(resp);

            var resp2 = await host.GetTestClient().GetStringAsync("/files/subdirectory/");
            Assert.NotNull(resp2);
        }


        [Fact]
        public async Task UseDirectoryBrowserWithOptions()
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();
                    builder.ConfigureServices(services =>
                    {
                        services.AddDirectoryBrowser();
                    });

                    builder.Configure(app =>
                    {
                        app.UseDirectoryBrowser(
                            new DirectoryBrowserOptions() 
                            {
                                RequestPath = "/files"
                            });
                    });
                })
                .Start();

            // url to browse directory MUST be end with "/"
            var resp = await host.GetTestClient().GetStringAsync("/files/");
            Assert.NotNull(resp);

            var resp2 = await host.GetTestClient().GetStringAsync("/files/subdirectory/");
            Assert.NotNull(resp2);
        }
    }
}
