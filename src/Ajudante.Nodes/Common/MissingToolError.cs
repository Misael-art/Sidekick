namespace Ajudante.Nodes.Common;

internal sealed class MissingToolException : Exception
{
    public string ToolName { get; }
    public string InstallHint { get; }

    public MissingToolException(string toolName, string installHint)
        : base($"Required tool '{toolName}' was not found. {installHint}")
    {
        ToolName = toolName;
        InstallHint = installHint;
    }
}
