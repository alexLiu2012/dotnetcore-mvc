using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace HttpPipelineAndTestServerTest
{
    public class StrongTypeMiddlewareB : IMiddleware
    {
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            context.Response.StatusCode = StatusCodes.Status102Processing;
            await next(context);
        }
    }
}
