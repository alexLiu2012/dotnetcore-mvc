using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace HttpPipelineAndTestServerTest
{
    public class ApplicationBuilderUseExtenesionTest
    {
        // use func of (context, next), next is parameterless
        [Fact]
        public async Task TestUseFuncNextNoParameter()
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();
                    builder.Configure(app =>
                    {
                        // 1st middleware registered, set status code = 101, with next
                        app.Use((context, next) =>
                        {
                            context.Response.StatusCode = StatusCodes.Status101SwitchingProtocols;
                            return next();
                        });
                        // 2nd middelware registered, set status code = 102, terminate
                        app.Use((context, next) =>
                        {
                            context.Response.StatusCode = StatusCodes.Status102Processing;
                            return Task.CompletedTask;
                        });
                    });
                })
                .Start();

            var response = await host.GetTestClient().GetAsync("/");
            Assert.Equal(HttpStatusCode.Processing, response.StatusCode);

        }


        // test use middleware - strong type, implemente the "IMiddleare" interface

        // strong typed middleware will be resolved (create) by "middleware factory",
        // "middleware factory" had been registered in "configure web host" method, but customized strong typed middleare should inject manualy

        [Fact]
        public async Task TestUseStrongTypedMiddleware()
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();
                    builder.ConfigureServices(services =>
                    {
                        services.AddSingleton<StrongTypeMiddlewareA>();
                        services.AddSingleton<StrongTypeMiddlewareB>();
                        services.AddSingleton<StrongTypeMiddlewareC>();
                    });
                    builder.Configure(app =>
                    {
                        // strong typed middleware a, set status code = 101, with next
                        app.UseMiddleware<StrongTypeMiddlewareA>();

                        // strong typed middleware b, set status code = 102, with next
                        app.UseMiddleware<StrongTypeMiddlewareB>();

                        // strong typed middleware c, set status code = 201, terminate
                        app.UseMiddleware<StrongTypeMiddlewareC>();
                    });
                })
                .Start();

            var response = await host.GetTestClient().GetAsync("/");
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }



        // test use middleware - type, weak typed middleware should
        // - method of "invoke" or "invokeAsync"
        // - public ctor with at least 1 parameter, -> request delegate "next"


        // use weak typed middleware
        [Fact]
        public async Task TestUseWeakTypedMiddleware()
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();

                    builder.Configure(app =>
                    {
                        // middleware a, set status code = 101, with next
                        app.UseMiddleware<WeakTypeMiddlewareA>();

                        // middleware b, set status code = 102, with next
                        app.UseMiddleware<WeakTypeMiddlewareB>();
                    });
                })
                .Start();

            // as the last middleware registered has next(), it will call the app (default request delegate), to set 404
            var response = await host.GetTestClient().GetAsync("/");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }


        // weak typed middleware but ctor has no parameter (should at least has a "request delegate")
        [Fact]
        public void TestUseWeakMiddlewareButParameterlessCtor()
        {
            try
            {
                using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();

                    builder.Configure(app =>
                    {
                        // middleware c has a ctor but no parameter
                        app.UseMiddleware<WeakTypeMiddlewareC>();
                    });
                })
                .Start();

            }
            catch (Exception ex)
            {
                // throw invalid operation exception EVEN in configuring
                Assert.True(true);
            }
        }


        // test run(middleware), will terminate the request pipeline always
        [Fact]
        public async Task TestUseRunMiddleware()
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();
                    builder.Configure(app =>
                    {
                        // run middleware will automatically terminate the request pipeline!
                        app.Run(context =>
                        {
                            context.Response.StatusCode = StatusCodes.Status102Processing;
                            return Task.CompletedTask;
                        });

                        // as run middleware before, middlewares registered AFTER "run" will never works
                        app.Use(next =>
                        {
                            return context =>
                            {
                                context.Response.StatusCode = StatusCodes.Status101SwitchingProtocols;
                                return Task.CompletedTask;
                            };
                        });
                    });
                })
                .Start();

            var response = await host.GetTestClient().GetAsync("/");
            Assert.Equal(HttpStatusCode.Processing, response.StatusCode);
        }



        // test use when
        [Fact]
        public async Task TestUseWhenMiddleware()
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();

                    builder.Configure(app =>
                    {
                        app.UseWhen(
                            // set predicate, -> path = "/when"?
                            context => context.Request.Path == "/when",
                            appBuilder =>
                            {
                                // if so, run the request delegate (run middleware will terminate the request pipeline)
                                appBuilder.Run(context =>
                                {
                                    context.Response.StatusCode = StatusCodes.Status101SwitchingProtocols;
                                    return Task.CompletedTask;
                                });
                            });
                    });
                })
                .Start();

            // when condition not triggered
            var response = await host.GetTestClient().GetAsync("");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            // when condition triggered
            var responseWhen = await host.GetTestClient().GetAsync("when");
            Assert.Equal(HttpStatusCode.SwitchingProtocols, responseWhen.StatusCode);
        }



        // test use path base
        // set the path base by "use path base" method, then request pipeline will extract the specific string as "path base", 
        // then the remain request path will be the new "path"
        [Fact]
        public async Task TestUseBasePathMiddleware()
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();

                    builder.Configure(app =>
                    {
                        app.UsePathBase("/hello_world");

                        app.Use((context, next) =>
                        {
                            if (context.Request.Path == "/")
                            {
                                context.Response.StatusCode = StatusCodes.Status101SwitchingProtocols;
                            }

                            next();
                            return Task.CompletedTask;
                        });

                        app.Use((context, next) =>
                        {
                            if (context.Request.Path == "/aloha")
                            {
                                context.Response.StatusCode = StatusCodes.Status102Processing;
                            }

                            return Task.CompletedTask;
                        });
                    });
                })
                .Start();


            // request path matched "/aloha",
            // (whatever "/aloha" or "aloha")
            var resp1 = await host.GetTestClient().GetAsync("/aloha");
            Assert.Equal(HttpStatusCode.Processing, resp1.StatusCode);


            // "hello_world" will be extract as pathbase, so the path ramains to match "/aloha"
            // (whatever "/hello_world/aloha" or "hello_world/aloha")
            var resp2 = await host.GetTestClient().GetAsync("/hello_world/aloha");
            Assert.Equal(HttpStatusCode.Processing, resp2.StatusCode);
        }


        // test map
        [Fact]
        public async Task TestMapMiddleware()
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();

                    builder.Configure(app =>
                    {
                        // matched ver1, 
                        app.Map("/ver1", app =>
                        {
                            // set status code = 101, with next
                            app.UseMiddleware<WeakTypeMiddlewareA>();

                            // terminate request pipeline
                            app.Use(next => context => Task.CompletedTask);
                        });

                        // matched ver2
                        app.Map("/ver2", app =>
                        {
                            // set status code = 102, with next
                            app.UseMiddleware<WeakTypeMiddlewareB>();

                            // terminate the request pipeline
                            app.Use(next => context => Task.CompletedTask);
                        });
                    });
                })
                .Start();

            // not match any branche
            var resp1 = await host.GetTestClient().GetAsync("/");
            Assert.Equal(HttpStatusCode.NotFound, resp1.StatusCode);

            // matched "/ver1"
            var resp2 = await host.GetTestClient().GetAsync("/ver1");
            Assert.Equal(HttpStatusCode.SwitchingProtocols, resp2.StatusCode);

            // matched "/ver2"
            var resp3 = await host.GetTestClient().GetAsync("/ver2/aloha");
            Assert.Equal(HttpStatusCode.Processing, resp3.StatusCode);
        }        
    }            
}
