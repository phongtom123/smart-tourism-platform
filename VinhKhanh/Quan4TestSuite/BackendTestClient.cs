using System.Net.Sockets;

namespace Quan4TestSuite;

public static class BackendTestClient
{
    public static HttpClient Create()
    {
        return new HttpClient
        {
            BaseAddress = new Uri(Quan4TestConfig.BaseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public static async Task<bool> IsReachableAsync()
    {
        try
        {
            var uri = new Uri(Quan4TestConfig.BaseUrl);
            var port = uri.Port > 0 ? uri.Port : uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? 443 : 80;

            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(uri.Host, port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(TimeSpan.FromMilliseconds(1500)));

            return completed == connectTask && client.Connected;
        }
        catch
        {
            return false;
        }
    }
}
