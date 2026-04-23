using System.Net.Http.Headers;
using System.Text;

namespace Ajudante.Nodes.Common;

internal static class HttpClientProvider
{
    private static readonly HttpClient Client = new();

    public static async Task<(int statusCode, string body, string reasonPhrase)> SendAsync(
        string method,
        string url,
        string? body,
        string? contentType,
        string? headersRaw,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        ApplyHeaders(request, headersRaw);

        if (!string.IsNullOrWhiteSpace(body) && request.Method != HttpMethod.Get && request.Method != HttpMethod.Head)
        {
            request.Content = new StringContent(body, Encoding.UTF8, string.IsNullOrWhiteSpace(contentType) ? "application/json" : contentType);
        }

        using var response = await Client.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        return ((int)response.StatusCode, responseBody, response.ReasonPhrase ?? string.Empty);
    }

    private static void ApplyHeaders(HttpRequestMessage request, string? headersRaw)
    {
        if (string.IsNullOrWhiteSpace(headersRaw))
            return;

        var lines = headersRaw.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
                continue;

            var name = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            if (!request.Headers.TryAddWithoutValidation(name, value))
            {
                request.Content ??= new StringContent(string.Empty);
                request.Content.Headers.TryAddWithoutValidation(name, value);
            }
        }
    }
}
