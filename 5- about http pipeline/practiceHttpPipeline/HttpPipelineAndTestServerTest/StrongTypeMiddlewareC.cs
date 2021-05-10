using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace HttpPipelineAndTestServerTest
{
    public class StrongTypeMiddlewareC : IMiddleware
    {
        public Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            context.Response.StatusCode = StatusCodes.Status201Created;
            return Task.CompletedTask;
        }
    }
}
