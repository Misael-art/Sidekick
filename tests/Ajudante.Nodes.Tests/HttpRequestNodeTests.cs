using System.Net;
using System.Text;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Nodes.Actions;

namespace Ajudante.Nodes.Tests;

public class HttpRequestNodeTests
{
    private static FlowExecutionContext CreateContext()
    {
        var flow = new Flow { Name = "HTTP Flow" };
        return new FlowExecutionContext(flow, CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccessPortForSuccessfulResponse()
    {
        using var server = new TestHttpServer(async context =>
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            var bytes = Encoding.UTF8.GetBytes("{\"ok\":true}");
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        });

        var node = new HttpRequestNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["method"] = "GET",
            ["url"] = server.Url,
            ["storeBodyInVariable"] = "responseBody"
        });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("success", result.OutputPort);
        Assert.Equal(200, result.Outputs["statusCode"]);
        Assert.Equal("{\"ok\":true}", result.Outputs["body"]);
        Assert.Equal("{\"ok\":true}", context.GetVariable("responseBody"));
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsErrorPortForServerError()
    {
        using var server = new TestHttpServer(async context =>
        {
            context.Response.StatusCode = 500;
            var bytes = Encoding.UTF8.GetBytes("boom");
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        });

        var node = new HttpRequestNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["method"] = "GET",
            ["url"] = server.Url
        });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("error", result.OutputPort);
        Assert.Equal(500, result.Outputs["statusCode"]);
        Assert.Equal("boom", result.Outputs["body"]);
    }

    private sealed class TestHttpServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _serverTask;

        public string Url { get; }

        public TestHttpServer(Func<HttpListenerContext, Task> handler)
        {
            var port = GetFreePort();
            Url = $"http://127.0.0.1:{port}/";
            _listener.Prefixes.Add(Url);
            _listener.Start();
            _serverTask = Task.Run(async () =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        var context = await _listener.GetContextAsync();
                        await handler(context);
                    }
                    catch (HttpListenerException)
                    {
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                }
            });
        }

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();
            _listener.Close();
            _serverTask.Wait(TimeSpan.FromSeconds(2));
            _cts.Dispose();
        }

        private static int GetFreePort()
        {
            var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
