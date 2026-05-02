using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;

namespace Ajudante.Platform.Windows;

public sealed record AppInstallRequest(
    string SourceType,
    string? PackageId,
    string? Url,
    string? Checksum,
    string? InstallerArgs,
    bool Silent,
    bool RequireAdmin,
    int TimeoutMs,
    int RetryCount,
    string? ExpectedProcessName,
    string? ExpectedPath,
    bool VerifyAfterInstall,
    bool DryRun);

public static class AppInstallerService
{
    public static async Task<ShellOperationResult> InstallAsync(AppInstallRequest request, CancellationToken cancellationToken)
    {
        if (request.DryRun)
        {
            return Ok("Dry-run: instalacao validada sem executar instalador.",
                ("sourceType", request.SourceType),
                ("packageId", request.PackageId),
                ("url", request.Url),
                ("verifyAfterInstall", request.VerifyAfterInstall));
        }

        return request.SourceType.Trim().ToLowerInvariant() switch
        {
            "winget" => await RunWithRetryAsync("winget", BuildWingetArgs(request), request, cancellationToken),
            "msi" => await RunMsiAsync(request, cancellationToken),
            "exe" => await RunExeAsync(request, cancellationToken),
            "url" => await DownloadAndInstallUrlAsync(request, cancellationToken),
            _ => Fail($"sourceType nao suportado nesta versao: {request.SourceType}")
        };
    }

    public static async Task<ShellOperationResult> DownloadFileAsync(string url, string outputPath, string checksum, bool dryRun, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
            return Fail("Informe a URL do download.");

        if (string.IsNullOrWhiteSpace(outputPath))
            return Fail("Informe outputPath para salvar o download.");

        if (dryRun)
        {
            return Ok("Dry-run: download validado sem baixar arquivo.", ("url", url), ("outputPath", outputPath), ("checksumRequired", !string.IsNullOrWhiteSpace(checksum)));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        using var http = new HttpClient();
        await using var source = await http.GetStreamAsync(url, cancellationToken);
        await using var destination = File.Create(outputPath);
        await source.CopyToAsync(destination, cancellationToken);

        if (!string.IsNullOrWhiteSpace(checksum))
        {
            var verification = await VerifyChecksumAsync(outputPath, checksum, cancellationToken);
            if (!verification.Success)
                return verification;
        }

        return Ok("Arquivo baixado.", ("url", url), ("outputPath", outputPath));
    }

    public static async Task<ShellOperationResult> VerifyChecksumAsync(string filePath, string checksum, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
            return Fail($"Arquivo nao encontrado: {filePath}");

        if (string.IsNullOrWhiteSpace(checksum))
            return Fail("Informe checksum esperado.");

        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        var actual = Convert.ToHexString(hash).ToLowerInvariant();
        var expected = checksum.Trim().ToLowerInvariant();
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)
            ? Ok("Checksum SHA256 verificado.", ("filePath", filePath), ("checksum", actual))
            : Fail($"Checksum divergente. Esperado {expected}, obtido {actual}.");
    }

    public static bool IsAppInstalled(string expectedProcessName, string expectedPath)
    {
        if (!string.IsNullOrWhiteSpace(expectedPath) && File.Exists(Environment.ExpandEnvironmentVariables(expectedPath)))
            return true;

        if (!string.IsNullOrWhiteSpace(expectedProcessName))
        {
            var name = Path.GetFileNameWithoutExtension(expectedProcessName);
            return Process.GetProcessesByName(name).Length > 0;
        }

        return false;
    }

    private static async Task<ShellOperationResult> DownloadAndInstallUrlAsync(AppInstallRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
            return Fail("URL direta requer url.");

        if (string.IsNullOrWhiteSpace(request.Checksum))
            return Fail("URL direta requer checksum SHA256 para instalacao real.");

        var extension = Path.GetExtension(new Uri(request.Url).AbsolutePath);
        var downloadPath = Path.Combine(Path.GetTempPath(), $"sidekick_install_{Guid.NewGuid():N}{extension}");
        var download = await DownloadFileAsync(request.Url, downloadPath, request.Checksum, dryRun: false, cancellationToken);
        if (!download.Success)
            return download;

        var nested = request with
        {
            SourceType = string.Equals(extension, ".msi", StringComparison.OrdinalIgnoreCase) ? "msi" : "exe",
            Url = downloadPath
        };
        return await InstallAsync(nested, cancellationToken);
    }

    private static Task<ShellOperationResult> RunMsiAsync(AppInstallRequest request, CancellationToken cancellationToken)
    {
        var path = ResolveInstallerPath(request);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return Task.FromResult(Fail("MSI nao encontrado. Use url ou expectedPath com caminho valido."));

        var args = $"/i \"{path}\" {(request.Silent ? "/qn" : "")} {request.InstallerArgs}".Trim();
        return RunWithRetryAsync("msiexec.exe", args, request, cancellationToken);
    }

    private static Task<ShellOperationResult> RunExeAsync(AppInstallRequest request, CancellationToken cancellationToken)
    {
        var path = ResolveInstallerPath(request);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return Task.FromResult(Fail("EXE nao encontrado. Use url ou expectedPath com caminho valido."));

        var args = request.Silent && string.IsNullOrWhiteSpace(request.InstallerArgs)
            ? "/S"
            : request.InstallerArgs ?? "";
        return RunWithRetryAsync(path, args, request, cancellationToken);
    }

    private static async Task<ShellOperationResult> RunWithRetryAsync(string fileName, string arguments, AppInstallRequest request, CancellationToken cancellationToken)
    {
        var attempts = Math.Max(1, request.RetryCount + 1);
        var timeoutMs = request.TimeoutMs <= 0 ? 300000 : request.TimeoutMs;
        Exception? lastError = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (process is null)
                    return Fail($"Nao foi possivel iniciar {fileName}.");

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(timeoutMs);
                await process.WaitForExitAsync(timeoutCts.Token);

                if (process.ExitCode == 0)
                {
                    var verified = !request.VerifyAfterInstall || IsAppInstalled(request.ExpectedProcessName ?? "", request.ExpectedPath ?? "");
                    return Ok("Instalador finalizado.", ("fileName", fileName), ("exitCode", process.ExitCode), ("verified", verified));
                }

                lastError = new InvalidOperationException($"ExitCode {process.ExitCode}");
            }
            catch (Exception ex) when (attempt < attempts)
            {
                lastError = ex;
                await Task.Delay(Math.Min(1000 * attempt, 5000), cancellationToken);
            }
        }

        return Fail($"Instalacao falhou apos {attempts} tentativa(s): {lastError?.Message}");
    }

    private static string BuildWingetArgs(AppInstallRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PackageId))
            return "";

        var silent = request.Silent ? " --silent" : "";
        return $"install --id \"{request.PackageId}\" --exact --accept-source-agreements --accept-package-agreements{silent} {request.InstallerArgs}".Trim();
    }

    private static string ResolveInstallerPath(AppInstallRequest request)
    {
        var candidate = !string.IsNullOrWhiteSpace(request.Url) && File.Exists(request.Url)
            ? request.Url
            : request.ExpectedPath;
        return Environment.ExpandEnvironmentVariables(candidate ?? "");
    }

    private static ShellOperationResult Ok(string message, params (string Key, object? Value)[] outputs) =>
        new(true, message, outputs.ToDictionary(item => item.Key, item => item.Value));

    private static ShellOperationResult Fail(string message) => new(false, message, new Dictionary<string, object?>());
}
