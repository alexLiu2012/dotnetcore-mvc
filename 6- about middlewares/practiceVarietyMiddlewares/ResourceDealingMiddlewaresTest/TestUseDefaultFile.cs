using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using System.IO;

using System.Threading.Tasks;
using Xunit;

namespace ResourceDealingMiddlewaresTest
{
    public class TestUseDefaultFile
    {
        private string _contentRoot;
        private string _contentRootSub;
        private string _contentCn;
        public TestUseDefaultFile()
        {
            // default www root
            var rootDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var rootSubDirectory = Path.Combine(rootDirectory, "subdirectory");

            _contentRoot = File.ReadAllText(Path.Combine(rootDirectory, "index.html"));
            _contentCn = File.ReadAllText(Path.Combine(rootDirectory, "我的文档.html"));
            _contentRootSub = File.ReadAllText(Path.Combine(rootSubDirectory, "index.html"));
        }


        [Fact]
        public async Task UseDefaultFileWithAllDefault()
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
                        // "default files" middleware MUST set before of others
                        app.UseDefaultFiles();
                        app.UseStaticFiles();                        
                    });
                })
                .Start();

            // read ~/index.html
            var resp = await host.GetTestClient().GetStringAsync("/");
            Assert.Equal(_contentRoot, resp);
        }



        [Fact]
        public async Task UseDefaultFileWithRequestPath()
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
                        app.UseDefaultFiles("/default");
                        // "static file middleware" should set the same request path (prefix),
                        // as the "default files middleware" will forward the request to static files middleware
                        app.UseStaticFiles("/default");
                    });
                })
                .Start();

            // load (default) /index.html
            // url must be end with "/"
            var resp = await host.GetTestClient().GetStringAsync("/default/");
            Assert.Equal(_contentRoot, resp);
        }


        [Fact]
        public async Task UseDefaultFileWithOptions()
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
                        var options = new DefaultFilesOptions();
                        options.RequestPath = "/default";
                        options.DefaultFileNames.Clear();
                        options.DefaultFileNames.Add("我的文档.html");

                        app.UseDefaultFiles(options);                        
                        app.UseStaticFiles("/default");
                    });
                })
                .Start();

            // load specified default file            
            var resp = await host.GetTestClient().GetStringAsync("/default/");
            Assert.Equal(_contentCn, resp);
        }
    }
}
