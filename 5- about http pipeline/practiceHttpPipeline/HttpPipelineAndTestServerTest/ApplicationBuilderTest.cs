using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using System;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace HttpPipelineAndTestServerTest
{
    public class ApplicationBuilderTest
    {                                

        // while building the application pipeline wiht "applicaiton builder. builde" method, 
        // a default app (request delegate) will be created and pass as the parameter to middlewares!


        // if there is no middlewares registered, ONLY default app (request delegate) works -> return 404
        [Fact]
        public async Task TestAppBuilderNoMiddlewareConfigured()
        {           
            using var host = new HostBuilder()                
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();

                    // there must be "configure" method for startup !
                    builder.Configure(app => { });
                })
                .Start();

            // the very beginning request delegate = app (default, -> 404)
            var response = await host.GetTestClient().GetAsync("/");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }


        // if middleware registered, middleware works to modify the http context and return (terminate middleware, no calling next)
        [Fact]
        public async Task TestAppBuilderWithTerminateMiddleware()
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();
                    builder.Configure(app =>
                    {
                        // 1st registered, set 101, without next
                        // will work and terminate the request pipeline
                        app.Use(next =>
                        {
                            return context =>
                            {
                                context.Response.StatusCode = StatusCodes.Status101SwitchingProtocols;
                                return Task.CompletedTask;
                            };
                        })
                        // 2nd registered, set 102, withou next
                        // will NOT work as the request pipeline had been terminated
                        .Use(next =>
                        {
                            return context =>
                            {
                                context.Response.StatusCode = StatusCodes.Status102Processing;
                                return Task.CompletedTask;
                            };
                        });
                    });
                })
                .Start();
                        
            var response = await host.GetTestClient().GetAsync("/");

            // terminate middleware changed the status code to "no content"
            Assert.Equal(HttpStatusCode.SwitchingProtocols, response.StatusCode);
        }


        // or call next request delegate (non terminate middleware), until the default final request delegate (-> 404) if NO short circuit middleware defined
        [Fact]
        public async Task TestApplicationBuilderWithMiddlewareCallingNext()
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();
                    builder.Configure(app =>
                    {
                        // 1st registered, set 101, with next
                        app.Use(next =>
                        {
                            return context =>
                            {
                                context.Response.StatusCode = StatusCodes.Status101SwitchingProtocols;
                                return next(context);
                            };
                        })
                        // 2nd registered, set 102, with next
                        .Use(next =>
                        {
                            return context =>
                            {
                                context.Response.StatusCode = StatusCodes.Status102Processing;
                                return next(context);
                            };
                        });
                    });
                })
                .Start();

            var response = await host.GetTestClient().GetAsync("/");

            // terminate middleware changed the status code to "no content",
            // but after that, calling "next, (default app, -> 404)" change it back to "not found"
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        
        // utile a middleware short circuit the request pipeline
        [Fact]
        public async Task TestApplicationBuilderWithMiddlewareShortCircuit()
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();
                    builder.Configure(app =>
                    {
                        // 1st registered, set 101, with next
                        app.Use(next =>
                        {
                            return context =>
                            {
                                context.Response.StatusCode = StatusCodes.Status101SwitchingProtocols;
                                return next(context);
                            };
                        })
                        // 2nd registered, set 102, short circuite (without next) !!!
                        .Use(next =>
                        {
                            return context =>
                            {
                                context.Response.StatusCode = StatusCodes.Status102Processing;
                                return Task.CompletedTask;
                            };
                        });
                    });
                })
                .Start();

            // status code was changed to "101" then "102" by middleware, after that request pipeline had been terminated
            var response = await host.GetTestClient().GetAsync("/");            
            Assert.Equal(HttpStatusCode.Processing, response.StatusCode);
        }


        // http context is shared by all middleware (func)
        [Fact]
        public async Task TestInjectItemValue()
        {
            var value1 = "from middleware 1";
            var value2 = "from middldeare 2";

            using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();
                    builder.Configure(app =>
                    {
                        // 1st registered, inject "from middleware 1" in http context items, with next
                        app.Use(next =>
                        {
                            return context =>
                            {
                                var containts = context.Items.TryGetValue("key", out var itemValue);

                                if (containts)
                                {
                                    context.Items.Remove("key");
                                }

                                context.Items.Add("key", (string)itemValue ?? string.Empty + value1);

                                return next(context);
                            };
                        })
                        // 2nd registered, inject "from middleware 2" in http context items, with next
                        .Use(next =>
                        {
                            return context =>
                            {
                                var containts = context.Items.TryGetValue("key", out var itemValue);

                                if (containts)
                                {
                                    context.Items.Remove("key");
                                }

                                context.Items.Add("key", ((string)itemValue ?? string.Empty) + value2);

                                return next(context);
                            };
                        })
                        // 3rd registered, write item value to response, terminate
                        .Use(next =>
                        {
                            return async context =>
                            {
                                context.Items.TryGetValue("key", out var value);

                                value = value ?? "nothing injected in itmes";

                                await context.Response.WriteAsync((string)value);
                            };
                        });
                    });
                })
                .Start();

            var response = await host.GetTestClient().GetStringAsync("/");
            Assert.Equal(value1 + value2, response);
        }
        

        // short circuit middleware
        [Fact]
        public async Task TestInjectItemValueButShortCircuit()
        {
            var value1 = "from middleware 1";
            var value2 = "from middldeare 2";

            using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();
                    builder.Configure(app =>
                    {
                        // 1st registered, inject "from middleware 1" in http context items, with next
                        app.Use(next =>
                        {
                            return context =>
                            {
                                var containts = context.Items.TryGetValue("key", out var itemValue);

                                if (containts)
                                {
                                    context.Items.Remove("key");
                                }

                                context.Items.Add("key", (string)itemValue ?? string.Empty + value1);

                                return next(context);
                            };
                        })
                        // 2nd registered, inject "from middleware 2" in http context items, but terminate
                        .Use(next =>
                        {
                            return context =>
                            {
                                var containts = context.Items.TryGetValue("key", out var itemValue);

                                if (containts)
                                {
                                    context.Items.Remove("key");
                                }

                                context.Items.Add("key", ((string)itemValue ?? string.Empty) + value2);

                                /* short circuit the 2nd middleware, so that 3rd middleware will never worker,
                                   no response wrote to http context */                                
                                return Task.CompletedTask;
                            };
                        })
                        // 3rd registered, write item value to response, terminate
                        .Use(next =>
                        {
                            return async context =>
                            {
                                context.Items.TryGetValue("key", out var value);

                                value = value ?? "nothing injected in itmes";

                                await context.Response.WriteAsync((string)value);
                            };
                        });
                    });
                })
                .Start();

            // create http context esponse alway 200,
            // so after middleware 1 short circuit, no changed happened on the http context response,
            // response return "200"
            var response = await host.GetTestClient().GetStringAsync("/");
            Assert.Equal("", response);
        }


        // middleware set status code more times (write reponse, e.g.) will cause exception
        [Fact]
        public async Task TestWriteReponseMoreTimes()
        {
            var value1 = "hello from middleware 1";
            var value2 = "hello from middleware 2";

            using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();
                    builder.Configure(app =>
                    {
                        app.Use(next =>
                        {
                            return async context =>
                            {
                                await context.Response.WriteAsync(value1);
                                await next(context);
                            };
                        });

                        app.Use(next =>
                        {
                            return async context =>
                            {                                
                                await context.Response.WriteAsync(value2);
                                await next(context);
                            };
                        });
                    });
                })
                .Start();

            // cannot write to response more than one time as the "status code" had been set, -> throw exception
            Func<Task> func = () => host.GetTestClient().GetStringAsync("/");
            await Assert.ThrowsAnyAsync<Exception>(func);
        }                      
    }
}
