using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace HttpRequestMiddlewareTest
{
    public class TestUseHttpsRedirection
    {
        [Fact]
        public async Task UseHttpsRedicrection()
        {
            using var host = new HostBuilder()
               .ConfigureWebHost(builder =>
               {
                   builder.UseTestServer();

                   builder.ConfigureServices(services =>
                   {
                       services.AddHttpsRedirection(options =>
                       {
                           options.HttpsPort = 443;
                           options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
                       });
                   });

                   builder.Configure(app =>
                   {
                       app.UseHttpsRedirection();

                       app.Use((context, next) =>
                       {
                           context.Response.WriteAsync("hello world");
                           return Task.CompletedTask;
                       });
                   });
               })
               .Start();

            var client = host.GetTestClient();

            var resp = await client.GetAsync("/hello");
            Assert.Equal(HttpStatusCode.RedirectKeepVerb, resp.StatusCode);
            Assert.Equal("https://localhost/hello", resp.Headers.Location.ToString());
        }
    }
}
