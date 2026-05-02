using System.Media;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.playSound",
    DisplayName = "Play Sound",
    Category = NodeCategory.Action,
    Description = "Plays a WAV sound file",
    Color = "#22C55E")]
public class PlaySoundNode : IActionNode
{
    private string _soundFile = "";
    private string _systemSound = "exclamation";

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.playSound",
        DisplayName = "Play Sound",
        Category = NodeCategory.Action,
        Description = "Plays a WAV sound file",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition>
        {
            new() { Id = "in", Name = "In", DataType = PortDataType.Flow }
        },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Out", DataType = PortDataType.Flow }
        },
        Properties = new List<PropertyDefinition>
        {
            new()
            {
                Id = "soundFile",
                Name = "Sound File",
                Type = PropertyType.FilePath,
                DefaultValue = "",
                Description = "Optional path to a .wav sound file to play"
            },
            new()
            {
                Id = "systemSound",
                Name = "System Sound",
                Type = PropertyType.Dropdown,
                DefaultValue = "exclamation",
                Description = "Fallback Windows sound when no file is provided",
                Options = new[] { "exclamation", "asterisk", "beep", "hand", "question" }
            }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("soundFile", out var sf) && sf is string file)
            _soundFile = file;
        if (properties.TryGetValue("systemSound", out var ss) && ss is string systemSound)
            _systemSound = systemSound;
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_soundFile))
            {
                var sound = _systemSound.Trim().ToLowerInvariant() switch
                {
                    "asterisk" => SystemSounds.Asterisk,
                    "beep" => SystemSounds.Beep,
                    "hand" => SystemSounds.Hand,
                    "question" => SystemSounds.Question,
                    _ => SystemSounds.Exclamation
                };
                sound.Play();
                return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
                {
                    ["systemSound"] = _systemSound
                }));
            }

            var resolvedPath = context.ResolveTemplate(_soundFile);

            if (!File.Exists(resolvedPath))
                return Task.FromResult(NodeResult.Fail($"Sound file not found: {resolvedPath}"));

            using var player = new SoundPlayer(resolvedPath);
            player.PlaySync();

            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["soundFile"] = resolvedPath
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeResult.Fail($"Failed to play sound: {ex.Message}"));
        }
    }
}
