using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace ResourceDealingMiddlewaresTest
{
    public class TestUseFileServer
    {
        private string _contentRoot;
        private string _contentRootSub;
        private string _contentRootCn;


        public TestUseFileServer()
        {
            // default www root
            var rootDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var rootSubDirectory = Path.Combine(rootDirectory, "subdirectory");

            _contentRoot = File.ReadAllText(Path.Combine(rootDirectory, "index.html"));
            _contentRootSub = File.ReadAllText(Path.Combine(rootSubDirectory, "index.html"));
            _contentRootCn = File.ReadAllText(Path.Combine(rootDirectory, "我的文档.html"));
        }

        [Fact]
        public async Task UseFileServerWithAllDefault()
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();
                    builder.ConfigureServices(services =>
                    {
                    });

                    builder.Configure(app =>
                    {
                        app.UseFileServer();
                    });
                })
                .Start();

            var resp1 = await host.GetTestClient().GetStringAsync("/index.html");
            Assert.Equal(_contentRoot, resp1);

            var resp2 = await host.GetTestClient().GetStringAsync("/subdirectory/index.html");
            Assert.Equal(_contentRootSub, resp2);

            var resp3 = await host.GetTestClient().GetStringAsync("/我的文档.html");
            Assert.Equal(_contentRootCn, resp3);
        }


        [Fact]
        public async Task UseFileServerWithRequestPath()
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();
                    builder.ConfigureServices(services =>
                    {
                    });

                    builder.Configure(app =>
                    {
                        app.UseFileServer("/default");
                    });
                })
                .Start();

            var resp1 = await host.GetTestClient().GetStringAsync("/default/index.html");
            Assert.Equal(_contentRoot, resp1);
        }


        [Fact]
        public async Task UseFileServerWithDefaultFile()
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();
                    builder.ConfigureServices(services =>
                    {
                    });

                    builder.Configure(app =>
                    {
                        app.UseFileServer(
                            new FileServerOptions()
                            {
                                EnableDefaultFiles = true
                            });
                    });
                })
                .Start();

            var resp1 = await host.GetTestClient().GetStringAsync("/");
            Assert.Equal(_contentRoot, resp1);
        }


        [Fact]
        public async Task UseFileServerWithDirectoryBrowser()
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
                        app.UseFileServer(enableDirectoryBrowsing:true);
                    });
                })
                .Start();

            var resp1 = await host.GetTestClient().GetStringAsync("/");
            Assert.NotNull(resp1);
        }
    }
}
