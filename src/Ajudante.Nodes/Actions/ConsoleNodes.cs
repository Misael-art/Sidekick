using System.Diagnostics;
using System.Text;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.consoleSetDirectory",
    DisplayName = "Console Set Directory",
    Category = NodeCategory.Action,
    Description = "Sets the working directory variable used by console command nodes",
    Color = "#22C55E")]
public sealed class ConsoleSetDirectoryNode : IActionNode
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.consoleSetDirectory",
        DisplayName = "Console Set Directory",
        Category = NodeCategory.Action,
        Description = "Sets the working directory variable used by console command nodes",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Out", DataType = PortDataType.Flow },
            new() { Id = "workingDirectory", Name = "Working Directory", DataType = PortDataType.String }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "workingDirectory", Name = "Working Directory", Type = PropertyType.FolderPath, DefaultValue = "", Description = "Directory to store as the console PWD. Supports {{variable}} templates." },
            new() { Id = "variableName", Name = "Variable Name", Type = PropertyType.String, DefaultValue = "pwd", Description = "Flow variable that stores the current console directory" },
            new() { Id = "createIfMissing", Name = "Create If Missing", Type = PropertyType.Boolean, DefaultValue = false, Description = "Create the folder before storing it" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _properties = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var workingDirectory = NodeValueHelper.ResolveTemplateProperty(context, _properties, "workingDirectory", "");
        var variableName = NodeValueHelper.GetString(_properties, "variableName", "pwd");
        var createIfMissing = NodeValueHelper.GetBool(_properties, "createIfMissing", false);

        if (string.IsNullOrWhiteSpace(workingDirectory))
            return Task.FromResult(NodeResult.Fail("Working directory is required."));

        var fullPath = Path.GetFullPath(workingDirectory);
        if (!Directory.Exists(fullPath))
        {
            if (!createIfMissing)
                return Task.FromResult(NodeResult.Fail($"Working directory does not exist: {fullPath}"));

            Directory.CreateDirectory(fullPath);
        }

        context.SetVariable(variableName, fullPath);
        return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
        {
            ["workingDirectory"] = fullPath,
            ["variableName"] = variableName
        }));
    }
}

[NodeInfo(
    TypeId = "action.consoleCommand",
    DisplayName = "Console Command",
    Category = NodeCategory.Action,
    Description = "Runs a command with working directory, shell, timeout, stdout, and stderr control",
    Color = "#22C55E")]
public sealed class ConsoleCommandNode : IActionNode
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.consoleCommand",
        DisplayName = "Console Command",
        Category = NodeCategory.Action,
        Description = "Runs a command with working directory, shell, timeout, stdout, and stderr control",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Success", DataType = PortDataType.Flow },
            new() { Id = "error", Name = "Error", DataType = PortDataType.Flow },
            new() { Id = "exitCode", Name = "Exit Code", DataType = PortDataType.Number },
            new() { Id = "stdout", Name = "Stdout", DataType = PortDataType.String },
            new() { Id = "stderr", Name = "Stderr", DataType = PortDataType.String },
            new() { Id = "workingDirectory", Name = "Working Directory", DataType = PortDataType.String }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "shell", Name = "Shell", Type = PropertyType.Dropdown, DefaultValue = "direct", Description = "How to run the command", Options = new[] { "direct", "cmd", "powershell" } },
            new() { Id = "command", Name = "Command", Type = PropertyType.String, DefaultValue = "", Description = "Executable or shell command. Supports {{variable}} templates." },
            new() { Id = "arguments", Name = "Arguments", Type = PropertyType.String, DefaultValue = "", Description = "Arguments for direct mode or text appended to the shell command." },
            new() { Id = "workingDirectory", Name = "Working Directory", Type = PropertyType.FolderPath, DefaultValue = "{{pwd}}", Description = "Run directory. Defaults to the pwd flow variable." },
            new() { Id = "timeoutMs", Name = "Timeout (ms)", Type = PropertyType.Integer, DefaultValue = 30000, Description = "Maximum time before killing the process" },
            new() { Id = "captureOutput", Name = "Capture Output", Type = PropertyType.Boolean, DefaultValue = true, Description = "Capture stdout and stderr" },
            new() { Id = "failOnNonZeroExit", Name = "Fail On Non-zero Exit", Type = PropertyType.Boolean, DefaultValue = true, Description = "Route to Error when exit code is not zero" },
            new() { Id = "storeStdoutInVariable", Name = "Store Stdout In Variable", Type = PropertyType.String, DefaultValue = "", Description = "Optional variable to receive stdout" },
            new() { Id = "storeStderrInVariable", Name = "Store Stderr In Variable", Type = PropertyType.String, DefaultValue = "", Description = "Optional variable to receive stderr" },
            new() { Id = "storeExitCodeInVariable", Name = "Store Exit Code In Variable", Type = PropertyType.String, DefaultValue = "", Description = "Optional variable to receive the exit code" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _properties = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var command = NodeValueHelper.ResolveTemplateProperty(context, _properties, "command", "");
        var arguments = NodeValueHelper.ResolveTemplateProperty(context, _properties, "arguments", "");
        var shell = NodeValueHelper.GetString(_properties, "shell", "direct");
        var timeoutMs = Math.Max(100, NodeValueHelper.GetInt(_properties, "timeoutMs", 30000));
        var captureOutput = NodeValueHelper.GetBool(_properties, "captureOutput", true);
        var failOnNonZeroExit = NodeValueHelper.GetBool(_properties, "failOnNonZeroExit", true);
        var workingDirectory = ResolveWorkingDirectory(context);

        if (string.IsNullOrWhiteSpace(command))
            return NodeResult.Fail("Command is required.");

        if (!string.IsNullOrWhiteSpace(workingDirectory) && !Directory.Exists(workingDirectory))
            return NodeResult.Fail($"Working directory does not exist: {workingDirectory}");

        var startInfo = BuildStartInfo(shell, command, arguments, workingDirectory, captureOutput);
        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        if (captureOutput)
        {
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    stdout.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    stderr.AppendLine(e.Data);
            };
        }

        try
        {
            context.EmitPhase(RuntimePhases.Retrying, $"Running console command: {command}");
            if (!process.Start())
                return NodeResult.Fail("Process did not start.");

            if (captureOutput)
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeoutMs);
            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                TryKill(process);
                return BuildResult("error", -1, stdout.ToString(), $"Command timed out after {timeoutMs} ms.", workingDirectory, context);
            }

            var exitCode = process.ExitCode;
            var outputText = stdout.ToString();
            var errorText = stderr.ToString();
            var outputPort = failOnNonZeroExit && exitCode != 0 ? "error" : "out";

            return BuildResult(outputPort, exitCode, outputText, errorText, workingDirectory, context);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
        catch (Exception ex)
        {
            return BuildResult("error", -1, stdout.ToString(), ex.Message, workingDirectory, context);
        }
    }

    private string ResolveWorkingDirectory(FlowExecutionContext context)
    {
        var raw = NodeValueHelper.ResolveTemplateProperty(context, _properties, "workingDirectory", "");
        if (string.Equals(raw, "{{pwd}}", StringComparison.OrdinalIgnoreCase))
            raw = context.GetVariable<string>("pwd") ?? "";

        if (string.IsNullOrWhiteSpace(raw))
            raw = context.GetVariable<string>("pwd") ?? Environment.CurrentDirectory;

        return Path.GetFullPath(raw);
    }

    private static ProcessStartInfo BuildStartInfo(string shell, string command, string arguments, string workingDirectory, bool captureOutput)
    {
        var commandLine = string.IsNullOrWhiteSpace(arguments) ? command : $"{command} {arguments}";
        var startInfo = shell.Trim().ToLowerInvariant() switch
        {
            "cmd" => new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/d /s /c {QuoteForCommandLine(commandLine)}"
            },
            "powershell" => new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command {QuoteForCommandLine(commandLine)}"
            },
            _ => new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments
            }
        };

        startInfo.WorkingDirectory = workingDirectory;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = captureOutput;
        startInfo.RedirectStandardError = captureOutput;
        startInfo.StandardOutputEncoding = Encoding.UTF8;
        startInfo.StandardErrorEncoding = Encoding.UTF8;
        return startInfo;
    }

    private static string QuoteForCommandLine(string value)
    {
        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private NodeResult BuildResult(
        string outputPort,
        int exitCode,
        string stdout,
        string stderr,
        string workingDirectory,
        FlowExecutionContext context)
    {
        NodeValueHelper.SetVariableIfRequested(context, NodeValueHelper.GetString(_properties, "storeStdoutInVariable", ""), stdout);
        NodeValueHelper.SetVariableIfRequested(context, NodeValueHelper.GetString(_properties, "storeStderrInVariable", ""), stderr);
        NodeValueHelper.SetVariableIfRequested(context, NodeValueHelper.GetString(_properties, "storeExitCodeInVariable", ""), exitCode);

        return NodeResult.Ok(outputPort, new Dictionary<string, object?>
        {
            ["exitCode"] = exitCode,
            ["stdout"] = stdout,
            ["stderr"] = stderr,
            ["workingDirectory"] = workingDirectory
        });
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }
}
