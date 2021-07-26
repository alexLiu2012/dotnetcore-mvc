using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HttpRequestMiddlewareTest
{
    public class TestUseWebSocket
    {
        private async Task Echo(HttpContext context, WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];

            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            while (!result.CloseStatus.HasValue)
            {
                await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage, CancellationToken.None);

                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }

            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }

        [Fact]
        public async Task Test1()
        {
            using var host = new HostBuilder()
                .ConfigureWebHost(builder =>
                {
                    builder.UseTestServer();
                    builder.ConfigureServices(services =>
                    {

                    });

                    builder.Configure(app =>
                    {
                        
                        app.UseWebSockets();
                        app.Use(async (context, next) =>
                        {
                            
                            if (context.WebSockets.IsWebSocketRequest)
                            {
                                var webSocket = context.WebSockets.AcceptWebSocketAsync().Result;

                                await Echo(context, webSocket);
                            }  
                            else
                            {
                                await next();
                            }
                        });
                       
                    });
                })
                .Start();

            var webClient = host.GetTestServer().CreateWebSocketClient();
            var webSocket = await webClient.ConnectAsync(new Uri("ws://localhost"), default);

            await webSocket.SendAsync(Encoding.UTF8.GetBytes("hello world"), WebSocketMessageType.Text, true, CancellationToken.None);

            Assert.True(true);
        }
    }
}
