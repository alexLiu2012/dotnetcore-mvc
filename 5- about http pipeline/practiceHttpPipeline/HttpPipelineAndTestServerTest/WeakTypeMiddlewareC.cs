using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace HttpPipelineAndTestServerTest
{
    public class WeakTypeMiddlewareC
    {        
        public WeakTypeMiddlewareC()
        {            
        }

        public Task InvokeAsync(HttpContext context)
        {
            context.Response.StatusCode = StatusCodes.Status201Created;
            return Task.CompletedTask;
        }
    }
}
