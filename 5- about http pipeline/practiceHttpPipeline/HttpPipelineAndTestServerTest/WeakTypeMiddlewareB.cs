using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace HttpPipelineAndTestServerTest
{
    public class WeakTypeMiddlewareB
    {
        private readonly RequestDelegate _next;

        public WeakTypeMiddlewareB(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            context.Response.StatusCode = StatusCodes.Status102Processing;
            await _next(context);
        }
    }
}
