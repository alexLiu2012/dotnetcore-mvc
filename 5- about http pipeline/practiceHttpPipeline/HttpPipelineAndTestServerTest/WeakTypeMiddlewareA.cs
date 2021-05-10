using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace HttpPipelineAndTestServerTest
{
    public class WeakTypeMiddlewareA
    {
        private readonly RequestDelegate _next;

        public WeakTypeMiddlewareA(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            context.Response.StatusCode = StatusCodes.Status101SwitchingProtocols;
            await _next(context);
        }
    }
}
