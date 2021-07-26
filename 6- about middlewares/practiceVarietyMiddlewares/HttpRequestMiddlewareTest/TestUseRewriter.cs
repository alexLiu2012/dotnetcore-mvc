using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace HttpRequestMiddlewareTest
{
    public class TestUseRewriter
    {
        [Fact]
        public async Task AddRewriteRules()
        {            

            using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();

                    builder.ConfigureServices(services =>
                    {
                        services.Configure<RewriteOptions>(options =>
                        {
                            // 正则，app/n => app?id=n
                            options.AddRewrite(@"app/(\d+)", "app?id=$1", skipRemainingRules: false);
                        });
                    });

                    builder.Configure(app =>
                    {
                        app.UseRewriter();

                        app.Use((context, next) =>
                        {                            
                            if (context.Request.Path == "/app" &&
                                context.Request.Query["id"] == "2")
                            {
                                context.Response.WriteAsync("rewrite");
                                return Task.CompletedTask;
                            }
                            else
                            {
                                return next();
                            }                           
                        });

                    });
                })
                .Start();

            var client = host.GetTestClient();

            // rewrite will not override the "status code"!!!
            var resp = await client.GetAsync("/app/2");
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

            var result = await resp.Content.ReadAsStringAsync();
            Assert.Equal("rewrite", result);
        }


        [Fact]
        public async Task AddRedirectRules()
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();
                    
                    builder.ConfigureServices(services =>
                    {
                        services.Configure<RewriteOptions>(options =>
                        {
                            // 正则，alpha/ 结尾 => origin，加上 307
                            options.AddRedirect("(.*)/$", "/aloha", StatusCodes.Status307TemporaryRedirect);
                        });
                    });
                    
                    builder.Configure(app =>
                    {
                        app.UseRewriter();
                        
                        app.Use((context, next) =>
                        {
                            context.Response.WriteAsync("hello world");
                            return Task.CompletedTask;
                        });
                    });
                })
                .Start();

            var client = host.GetTestClient();

            // redirect will override the "status code", response the new url & no content back
            var resp = await client.GetAsync("/hello/");
            Assert.Equal(HttpStatusCode.TemporaryRedirect, resp.StatusCode);

            string result = string.Empty;

            if (resp.StatusCode == HttpStatusCode.TemporaryRedirect)
            {
                var location = resp.Headers.Location!;
                result = await client.GetStringAsync(location);
            }

            Assert.Equal("hello world", result);           
        }



        [Fact]
        public async Task AddRedirectRulesDefaultCode()
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();

                    builder.ConfigureServices(services =>
                    {
                        services.Configure<RewriteOptions>(options =>
                        {
                            // 正则，alpha/ 结尾 => origin，不显式指定 status code
                            options.AddRedirect("(.*)/$", "/aloha");
                        });
                    });

                    builder.Configure(app =>
                    {
                        app.UseRewriter();

                        app.Use((context, next) =>
                        {
                            context.Response.WriteAsync("hello world");
                            return Task.CompletedTask;
                        });
                    });
                })
                .Start();

            var client = host.GetTestClient();

            // redirect will override the "status code", response the new url & no content back
            // if no "status code" specified explicied, default code = 302
            var resp = await client.GetAsync("/hello/");
            Assert.Equal(HttpStatusCode.Found, resp.StatusCode);

            string result = string.Empty;

            if (300 <= (int)resp.StatusCode && (int)resp.StatusCode < 400)
            {
                var location = resp.Headers.Location!;
                result = await client.GetStringAsync(location);
            }

            Assert.Equal("hello world", result);
        }


        /// host = localhost will not be allowed to redirect to www or non-www rule ///
        

        [Fact]
        public async Task AddRedirectHttpsTemporary()
        {
            using var host = new HostBuilder()
               .ConfigureWebHost(builder =>
               {
                   builder.UseTestServer();

                   builder.ConfigureServices(services =>
                   {
                       services.Configure<RewriteOptions>(options =>
                       {
                           // the default setting of https redirection is 302 & 443
                           options.AddRedirectToHttps();
                       });
                   });

                   builder.Configure(app =>
                   {
                       app.UseRewriter();

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
            Assert.Equal(HttpStatusCode.Found, resp.StatusCode);
            Assert.Equal("https://localhost/hello", resp.Headers.Location.ToString());

            //var result = await resp.Content.ReadAsStringAsync();
        }


        [Fact]
        public async Task AddRedirectHttpsPermanently()
        {
            using var host = new HostBuilder()
               .ConfigureWebHost(builder =>
               {
                   builder.UseTestServer();

                   builder.ConfigureServices(services =>
                   {
                       services.Configure<RewriteOptions>(options =>
                       {
                           // the default setting of https redirection is 301 & 443
                           options.AddRedirectToHttpsPermanent();
                       });
                   });

                   builder.Configure(app =>
                   {
                       app.UseRewriter();

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
            Assert.Equal(HttpStatusCode.MovedPermanently, resp.StatusCode);
            Assert.Equal("https://localhost/hello", resp.Headers.Location.ToString());           
        }

    }    
}
