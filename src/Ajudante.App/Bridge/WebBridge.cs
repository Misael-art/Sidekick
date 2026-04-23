using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Ajudante.App.Bridge;

/// <summary>
/// Manages the WebView2 initialization and bidirectional message passing between
/// the React frontend and the C# backend.
/// </summary>
public class WebBridge : IDisposable
{
    private readonly WebView2 _webView;
    private BridgeRouter? _router;
    private bool _isInitialized;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public event Action<string>? LogMessage;

    public WebBridge(WebView2 webView)
    {
        _webView = webView ?? throw new ArgumentNullException(nameof(webView));
    }

    /// <summary>
    /// Initializes WebView2 environment, maps the virtual host to the wwwroot folder,
    /// and wires up message handling.
    /// </summary>
    public async Task InitializeAsync(string wwwrootPath)
    {
        if (_isInitialized) return;

        var env = await CoreWebView2Environment.CreateAsync(
            userDataFolder: GetWebView2DataFolder());

        await _webView.EnsureCoreWebView2Async(env);

        // Map virtual host to wwwroot so the app loads from https://app.local/
        _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "app.local",
            wwwrootPath,
            CoreWebView2HostResourceAccessKind.Allow);

        // Security and UX settings
        var settings = _webView.CoreWebView2.Settings;
        settings.IsStatusBarEnabled = false;
        settings.AreDefaultContextMenusEnabled = false;
        settings.IsZoomControlEnabled = false;
        settings.AreHostObjectsAllowed = false;
        settings.AreDevToolsEnabled =
#if DEBUG
            true;
#else
            false;
#endif

        await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(GetConsoleForwarderScript());

        // Listen for messages from JS
        _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

        // Navigate to the app entry point
        _webView.CoreWebView2.Navigate("https://app.local/index.html");
#if DEBUG
        _webView.CoreWebView2.OpenDevToolsWindow();
#endif

        _isInitialized = true;
        Log("WebView2 initialized and navigating to app.local");
    }

    /// <summary>
    /// Sets the router that will handle incoming messages.
    /// </summary>
    public void SetRouter(BridgeRouter router)
    {
        _router = router;
    }

    /// <summary>
    /// Sends an event from C# to the React frontend.
    /// </summary>
    public async Task SendEventAsync(string channel, string action, object? payload = null)
    {
        var message = new BridgeMessage
        {
            Type = BridgeMessage.Types.Event,
            Channel = channel,
            Action = action,
            Payload = SerializeToElement(payload)
        };

        await PostMessageAsync(message);
    }

    /// <summary>
    /// Sends a response to a specific request from the React frontend.
    /// </summary>
    public async Task SendResponseAsync(string requestId, object? payload = null)
    {
        var message = new BridgeMessage
        {
            Type = BridgeMessage.Types.Response,
            RequestId = requestId,
            Payload = SerializeToElement(payload)
        };

        await PostMessageAsync(message);
    }

    /// <summary>
    /// Sends an error response to a specific request from the React frontend.
    /// </summary>
    public async Task SendErrorResponseAsync(string requestId, string error)
    {
        var message = new BridgeMessage
        {
            Type = BridgeMessage.Types.Response,
            RequestId = requestId,
            Error = error
        };

        await PostMessageAsync(message);
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.WebMessageAsJson;
            var message = JsonSerializer.Deserialize<BridgeMessage>(json, JsonOptions);

            if (message == null)
            {
                Log("Received null or unparseable message from WebView2");
                return;
            }

            if (string.Equals(message.Type, "console", StringComparison.OrdinalIgnoreCase))
            {
                var consoleMessage = GetPayloadString(message.Payload, "message") ?? "(empty console message)";
                var href = GetPayloadString(message.Payload, "href");
                var location = string.IsNullOrWhiteSpace(href) ? "" : $" @ {href}";
                Log($"Browser console [{message.Action}]{location} - {consoleMessage}");
                return;
            }

            Log($"Received: [{message.Channel}] {message.Action} (id: {message.RequestId})");

            if (_router == null)
            {
                Log("No router configured; dropping message");
                if (message.RequestId != null)
                {
                    await SendErrorResponseAsync(message.RequestId, "Bridge router not initialized");
                }
                return;
            }

            await _router.HandleMessageAsync(message);
        }
        catch (Exception ex)
        {
            Log($"Error processing web message: {ex.Message}");
        }
    }

    private static string GetPayloadString(JsonElement? payload, string propertyName)
    {
        if (payload == null) return "";
        if (payload.Value.ValueKind != JsonValueKind.Object) return "";

        if (payload.Value.TryGetProperty(propertyName, out var prop) &&
            prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString() ?? "";
        }

        return "";
    }

    private async Task PostMessageAsync(BridgeMessage message)
    {
        if (!_isInitialized || _webView.CoreWebView2 == null) return;

        var json = JsonSerializer.Serialize(message, JsonOptions);

        // WebView2 calls must happen on the UI thread
        if (_webView.Dispatcher.CheckAccess())
        {
            _webView.CoreWebView2.PostWebMessageAsJson(json);
        }
        else
        {
            await _webView.Dispatcher.InvokeAsync(() =>
            {
                _webView.CoreWebView2?.PostWebMessageAsJson(json);
            });
        }
    }

    private static JsonElement? SerializeToElement(object? value)
    {
        if (value == null) return null;

        // If already a JsonElement, return directly
        if (value is JsonElement element) return element;

        var json = JsonSerializer.Serialize(value, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static string GetWebView2DataFolder()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Sidekick", "WebView2Data");
    }

    private static string GetConsoleForwarderScript()
    {
        return """
(() => {
  const host = window.chrome && window.chrome.webview;
  if (!host || window.__sidekickConsoleForwarderInstalled) {
    return;
  }

  window.__sidekickConsoleForwarderInstalled = true;

  const levels = ['log', 'info', 'warn', 'error', 'debug'];
  const stringifyArg = (value) => {
    if (typeof value === 'string') return value;
    try {
      return JSON.stringify(value);
    } catch {
      return String(value);
    }
  };

  for (const level of levels) {
    const original = typeof console[level] === 'function' ? console[level].bind(console) : null;
    console[level] = (...args) => {
      try {
        host.postMessage(JSON.stringify({
          type: 'console',
          action: level,
          payload: {
            message: args.map(stringifyArg).join(' '),
            href: window.location.href
          }
        }));
      } catch {
      }

      original?.(...args);
    };
  }
})();
""";
    }

    private void Log(string message)
    {
        LogMessage?.Invoke($"[WebBridge] {message}");
    }

    public void Dispose()
    {
        if (_webView.CoreWebView2 != null)
        {
            _webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
        }
    }
}
