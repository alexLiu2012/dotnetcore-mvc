using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace HttpPipelineAndTestServerTest
{
    public class StrongTypeMiddlewareA : IMiddleware
    {
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            context.Response.StatusCode = StatusCodes.Status101SwitchingProtocols;
            await next(context);
        }
    }
}
