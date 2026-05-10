using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ajudante.App.Configuration;

/// <summary>
/// Persisted user preference for high-risk flow execution (AppData). Toggle alone does not bypass gates; UI must send securityAck.
/// </summary>
public sealed class SecuritySettings
{
    public bool AllowHighRiskExecution { get; set; }
}

/// <summary>
/// Reads and writes <c>security-settings.json</c> under the application data directory.
/// </summary>
public sealed class SecuritySettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _filePath;

    public SecuritySettingsStore(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "security-settings.json");
    }

    public SecuritySettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new SecuritySettings();
            }

            var json = File.ReadAllText(_filePath);
            var parsed = JsonSerializer.Deserialize<SecuritySettings>(json, JsonOptions);
            return parsed ?? new SecuritySettings();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Nao foi possivel ler as configuracoes de seguranca em '{_filePath}'. Verifique permissoes do ficheiro ou apague o JSON corrompido.",
                ex);
        }
    }

    public void Save(SecuritySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Nao foi possivel gravar as configuracoes de seguranca em '{_filePath}'. Verifique permissoes de escrita.",
                ex);
        }
    }
}
