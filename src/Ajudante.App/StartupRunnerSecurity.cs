using System.IO;
using System.Text.Json;
using System.Windows;
using Ajudante.App.Configuration;
using Ajudante.Core;
using Ajudante.Core.Engine;

namespace Ajudante.App;

/// <summary>
/// Validates exported-runner layout (security-manifest + consent) and prompts when <see cref="FlowSecurityAnalyzer"/>
/// reports unsafe flows for <c>--run-flow</c> startup (no WebView bridge).
/// </summary>
internal static class StartupRunnerSecurity
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static async Task<bool> TryAuthorizeAsync(string flowPath, Flow flow)
    {
        var analyzer = new FlowSecurityAnalyzer();
        var security = analyzer.Analyze(flow);
        var flowDir = Path.GetDirectoryName(flowPath) ?? string.Empty;
        var manifestPath = Path.Combine(flowDir, "security-manifest.json");
        var consentPath = Path.Combine(flowDir, ".runner-consent.json");

        if (File.Exists(manifestPath))
        {
            string? fileHash;
            try
            {
                var json = await File.ReadAllTextAsync(manifestPath).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("manifestHash", out var hashElement))
                {
                    ShowOnUiThread(() => MessageBox.Show(
                        "security-manifest.json nao contem manifestHash. Execucao cancelada.",
                        ProductIdentity.ErrorTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error));
                    return false;
                }

                fileHash = hashElement.GetString();
            }
            catch (Exception ex)
            {
                ShowOnUiThread(() => MessageBox.Show(
                    $"Nao foi possivel ler security-manifest.json.\n\n{ex.Message}",
                    ProductIdentity.ErrorTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error));
                return false;
            }

            if (!string.Equals(fileHash, security.ManifestHash, StringComparison.OrdinalIgnoreCase))
            {
                ShowOnUiThread(() => MessageBox.Show(
                    "O flow.json nao coincide com security-manifest.json (fluxo alterado apos export ou manifest desatualizado). Execucao cancelada.",
                    ProductIdentity.ErrorTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning));
                return false;
            }
        }

        if (TryReadRunnerConsent(consentPath, security.ManifestHash))
        {
            return true;
        }

        if (security.IsSafeToRun)
        {
            return true;
        }

        var lines = security.Issues
            .Where(static i => i.Severity is SecuritySeverity.Block or SecuritySeverity.Warning)
            .Select(static i => $" - {i.Message}")
            .ToArray();
        var detail = lines.Length > 0 ? string.Join(Environment.NewLine, lines) : " (sem detalhe adicional)";
        var text =
            $"Este fluxo foi classificado com risco '{security.RiskLevel}'.\n\n{detail}\n\nDeseja executar agora?";
        var approved = false;
        ShowOnUiThread(() =>
        {
            approved = MessageBox.Show(
                    text,
                    $"{ProductIdentity.ProductName} - Consentimento",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning)
                == MessageBoxResult.OK;
        });

        if (!approved)
        {
            return false;
        }

        try
        {
            var dto = new RunnerConsentDto
            {
                accepted = true,
                manifestHash = security.ManifestHash,
                acceptedAt = DateTime.UtcNow.ToString("o"),
            };
            await File.WriteAllTextAsync(consentPath, JsonSerializer.Serialize(dto, JsonOptions)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ShowOnUiThread(() => MessageBox.Show(
                $"Nao foi possivel gravar .runner-consent.json.\n\n{ex.Message}",
                ProductIdentity.ErrorTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Error));
            return false;
        }

        return true;
    }

    private static bool TryReadRunnerConsent(string consentPath, string expectedManifestHash)
    {
        try
        {
            if (!File.Exists(consentPath))
            {
                return false;
            }

            var json = File.ReadAllText(consentPath);
            var dto = JsonSerializer.Deserialize<RunnerConsentDto>(json, JsonOptions);
            return dto is { accepted: true }
                   && !string.IsNullOrWhiteSpace(dto.manifestHash)
                   && string.Equals(dto.manifestHash, expectedManifestHash, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void ShowOnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            action();
            return;
        }

        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.Invoke(action);
        }
    }

    private sealed class RunnerConsentDto
    {
        public bool accepted { get; set; }
        public string manifestHash { get; set; } = "";
        public string acceptedAt { get; set; } = "";
    }
}
