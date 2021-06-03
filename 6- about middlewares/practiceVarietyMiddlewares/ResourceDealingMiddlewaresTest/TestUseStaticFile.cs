using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace ResourceDealingMiddlewaresTest
{
    public class TestUseStaticFile
    {        
        private string _contentRoot;
        private string _contentRootSub;

        private string _contentMyRoot;
        private string _contentMyRootSub;       

        public TestUseStaticFile()
        {
            // default www root
            var rootDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var rootSubDirectory = Path.Combine(rootDirectory, "subdirectory");

            _contentRoot = File.ReadAllText(Path.Combine(rootDirectory, "index.html"));
            _contentRootSub = File.ReadAllText(Path.Combine(rootSubDirectory, "index.html"));

            // specified www root
            var myRootDirectory = Path.Combine(Directory.GetCurrentDirectory(), "mycontent");
            var myRootSubDirectory = Path.Combine(myRootDirectory, "subdirectory");

            _contentMyRoot = File.ReadAllText(Path.Combine(myRootDirectory, "index.html"));
            _contentMyRootSub = File.ReadAllText(Path.Combine(myRootSubDirectory, "index.html"));                        
        }

        // load read static file in defautl "wwwroot" fold
        [Fact]
        public async Task UseStaticFileWithAllDefault()
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
                        
                        app.UseStaticFiles();                        
                    });
                })
                .Start();
           
            // read ~/index.html
            var resp = await host.GetTestClient().GetStringAsync("/index.html");
            Assert.Equal(_contentRoot, resp);

            var resp1 = await host.GetTestClient().GetStringAsync("/我的文档.html");

            var cnDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var cn = await File.ReadAllTextAsync(Path.Combine(cnDir,"我的文档.html"));
            Assert.Equal(cn, resp1);

            // read ~/subDirectory/index.html
            var resp2 = await host.GetTestClient().GetStringAsync("/subdirectory/index.html");
            Assert.Equal(_contentRootSub, resp2);
        }

        // test load static file from specific fold instead "wwwroot"
        [Fact]
        public async Task UseStaticFileWithWwwRoot()
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();
                    builder.UseWebRoot("mycontent");
                    builder.ConfigureServices(services =>
                    {

                    });

                    builder.Configure(app =>
                    {

                        app.UseStaticFiles();
                    });
                })
                .Start();

            // read (myroot) ~/index.html
            var resp = await host.GetTestClient().GetStringAsync("/index.html");
            Assert.Equal(_contentMyRoot, resp);

            // read (myroot) ~/subDirectory/index.html
            var resp2 = await host.GetTestClient().GetStringAsync("/subdirectory/index.html");
            Assert.Equal(_contentMyRootSub, resp2);
        }


        [Fact]
        public async Task UseStaticFileWithRequestPath()
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
                        // specify the request path, (prefix before url of the related resource path)
                        app.UseStaticFiles("/files");
                    });
                })
                .Start();

            // read (/files) /index.html
            var resp = await host.GetTestClient().GetStringAsync("/files/index.html");
            Assert.Equal(_contentRoot, resp);

            // read (/files) /subDirectory/index.html
            var resp2 = await host.GetTestClient().GetStringAsync("/files/subdirectory/index.html");
            Assert.Equal(_contentRootSub, resp2);
        }

        [Fact]
        public async Task UseStaticFileWithOptions()
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
                        app.UseStaticFiles(
                            new StaticFileOptions()
                            {
                                // url (prefix)
                                RequestPath = "/my",
                                // file actually
                                FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(),"mycontent"))
                            });
                    });
                })
                .Start();

            // read (my)/index.html
            var resp = await host.GetTestClient().GetStringAsync("/my/index.html");
            Assert.Equal(_contentMyRoot, resp);
        }
    }
}
