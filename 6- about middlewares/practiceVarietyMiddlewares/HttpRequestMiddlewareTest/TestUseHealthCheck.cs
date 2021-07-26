using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HttpRequestMiddlewareTest
{
    // health check to success
    public class HealthCheckSuccess : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, 
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(HealthCheckResult.Healthy("good"));
        }
    }

    // health check to fail
    public class HealthCheckFailure : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, 
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy());
        }
    }

    // health check need service inject
    public class HealthCheckNeedService : IHealthCheck
    {
        //public HealthCheckNeedService()
        //{

        //}

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, 
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    public class TestUseHealthCheck
    {
        [Fact]
        public async Task AddRegistration()
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();

                    builder.ConfigureServices(services =>
                    {
                        services.AddHealthChecks().Add(
                            new HealthCheckRegistration(
                                "health check success", 
                                s => new HealthCheckSuccess(),
                                // IHealthCheck 的 check 方法抛出异常时，被 health check service 捕获，封装 ex 到 health check entry，需要注入的 health status
                                HealthStatus.Unhealthy,     
                                null));                            
                    });

                    builder.Configure(app =>
                    {
                        app.UseHealthChecks("/health");
                    });
                })
                .Start();

            var client = host.GetTestServer().CreateClient();

            var resp = await client.GetAsync("/health");
            var result = await resp.Content.ReadAsStringAsync();

            Assert.Equal("Healthy", result);
        }


        [Fact]
        public async Task AddHealthCheckInstance()
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();

                    builder.ConfigureServices(services =>
                    {
                        services.AddHealthChecks().AddCheck(
                            "health check success",
                            new HealthCheckSuccess(),
                            HealthStatus.Unhealthy,
                            null);                            

                    });

                    builder.Configure(app =>
                    {
                        app.UseHealthChecks("/health");
                    });
                })
                .Start();

            var client = host.GetTestServer().CreateClient();

            var resp = await client.GetAsync("/health");
            var result = await resp.Content.ReadAsStringAsync();

            Assert.Equal("Healthy", result);

        }


        [Fact]
        public async Task AddHealthCheckType()
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();

                    builder.ConfigureServices(services =>
                    {
                        services.AddHealthChecks().AddCheck<HealthCheckFailure>(
                            "health check failure",
                            HealthStatus.Unhealthy);
                            
                    });

                    builder.Configure(app =>
                    {
                        app.UseHealthChecks("/health");
                    });
                })
                .Start();

            var client = host.GetTestServer().CreateClient();

            var resp = await client.GetAsync("/health");
            var result = await resp.Content.ReadAsStringAsync();

            Assert.Equal("Unhealthy", result);
        }


        [Fact]
        public async Task AddHealthCheckWithOptions()
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();

                    builder.ConfigureServices(services =>
                    {
                        services
                            .AddHealthChecks()
                            .AddCheck<HealthCheckFailure>("health check failure", HealthStatus.Unhealthy)
                            .AddCheck<HealthCheckSuccess>("health check success", HealthStatus.Healthy);
                    });

                    builder.Configure(app =>
                    {
                        app.UseHealthChecks(
                            "/health",
                            new HealthCheckOptions()
                            { 
                                 Predicate = registration => registration.Name == "health check success"
                            });

                        app.UseHealthChecks(
                            "/health2",
                            new HealthCheckOptions()
                            {
                                Predicate = registration => registration.Name == "health check failure"
                            });
                    });
                })
                .Start();

            var client = host.GetTestServer().CreateClient();

            var resp = await client.GetAsync("/health");
            var result = await resp.Content.ReadAsStringAsync();
            Assert.Equal("Healthy", result);

            var resp2 = await client.GetAsync("/health2");
            var result2 = await resp2.Content.ReadAsStringAsync();
            Assert.Equal("Unhealthy", result2);
        }

    }
}
