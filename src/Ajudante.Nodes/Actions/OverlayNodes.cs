using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;
using Ajudante.Platform.Overlays;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.overlayColor",
    DisplayName = "Overlay Solid Color",
    Category = NodeCategory.Action,
    Description = "Shows a customizable foreground color overlay on the screen",
    Color = "#22C55E")]
public sealed class OverlayColorNode : IActionNode
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.overlayColor",
        DisplayName = "Overlay Solid Color",
        Category = NodeCategory.Action,
        Description = "Shows a customizable foreground color overlay on the screen",
        Color = "#22C55E",
        InputPorts = FlowInput(),
        OutputPorts = FlowOutput(),
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "color", Name = "Color", Type = PropertyType.Color, DefaultValue = "#000000", Description = "Solid overlay color" },
        }.Concat(CommonOverlayProperties()).ToList()
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _properties = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var options = BuildDisplayOptions(context, _properties) with
        {
            BackgroundColor = NodeValueHelper.ResolveTemplateProperty(context, _properties, "color", "#000000")
        };

        await OverlayDisplayService.ShowColorAsync(options, ct);
        return OverlayResult("color", options);
    }

    internal static List<PortDefinition> FlowInput() => new()
    {
        new() { Id = "in", Name = "In", DataType = PortDataType.Flow }
    };

    internal static List<PortDefinition> FlowOutput() => new()
    {
        new() { Id = "out", Name = "Out", DataType = PortDataType.Flow },
        new() { Id = "overlayKind", Name = "Overlay Kind", DataType = PortDataType.String },
        new() { Id = "durationMs", Name = "Duration (ms)", DataType = PortDataType.Number }
    };

    internal static IEnumerable<PropertyDefinition> CommonOverlayProperties()
    {
        return new[]
        {
            new PropertyDefinition { Id = "durationMs", Name = "Timer (ms)", Type = PropertyType.Integer, DefaultValue = 1000, Description = "How long the overlay stays visible. Use 0 to keep it until cancellation when wait is enabled." },
            new PropertyDefinition { Id = "waitForClose", Name = "Wait For Timer", Type = PropertyType.Boolean, DefaultValue = true, Description = "Wait for the timer before continuing the flow" },
            new PropertyDefinition { Id = "plane", Name = "Plane", Type = PropertyType.Dropdown, DefaultValue = "foreground", Description = "Foreground overlays stay above normal windows", Options = new[] { "foreground", "normal" } },
            new PropertyDefinition { Id = "fullScreen", Name = "Full Screen", Type = PropertyType.Boolean, DefaultValue = true, Description = "Cover all screens instead of a custom rectangle" },
            new PropertyDefinition { Id = "x", Name = "X", Type = PropertyType.Integer, DefaultValue = 0, Description = "Left position when not fullscreen" },
            new PropertyDefinition { Id = "y", Name = "Y", Type = PropertyType.Integer, DefaultValue = 0, Description = "Top position when not fullscreen" },
            new PropertyDefinition { Id = "width", Name = "Width", Type = PropertyType.Integer, DefaultValue = 640, Description = "Width when not fullscreen" },
            new PropertyDefinition { Id = "height", Name = "Height", Type = PropertyType.Integer, DefaultValue = 360, Description = "Height when not fullscreen" },
            new PropertyDefinition { Id = "opacity", Name = "Opacity", Type = PropertyType.Float, DefaultValue = 0.9, Description = "Overlay opacity from 0.05 to 1" },
            new PropertyDefinition { Id = "clickThrough", Name = "Click Through", Type = PropertyType.Boolean, DefaultValue = true, Description = "Let mouse clicks pass through the overlay" },
            new PropertyDefinition { Id = "motion", Name = "Motion", Type = PropertyType.Dropdown, DefaultValue = "none", Description = "Entrance motion", Options = new[] { "none", "slideUp", "slideDown", "slideLeft", "slideRight" } },
            new PropertyDefinition { Id = "fadeInMs", Name = "Fade In (ms)", Type = PropertyType.Integer, DefaultValue = 120, Description = "Entrance fade duration" },
            new PropertyDefinition { Id = "fadeOutMs", Name = "Fade Out (ms)", Type = PropertyType.Integer, DefaultValue = 120, Description = "Exit fade duration" }
        };
    }

    internal static OverlayDisplayOptions BuildDisplayOptions(FlowExecutionContext context, Dictionary<string, object?> properties)
    {
        var plane = NodeValueHelper.GetString(properties, "plane", "foreground");
        return new OverlayDisplayOptions
        {
            Bounds = new OverlayBounds(
                NodeValueHelper.GetInt(properties, "x", 0),
                NodeValueHelper.GetInt(properties, "y", 0),
                NodeValueHelper.GetInt(properties, "width", 640),
                NodeValueHelper.GetInt(properties, "height", 360),
                NodeValueHelper.GetBool(properties, "fullScreen", true)),
            DurationMs = Math.Max(0, NodeValueHelper.GetInt(properties, "durationMs", 1000)),
            WaitForClose = NodeValueHelper.GetBool(properties, "waitForClose", true),
            TopMost = !string.Equals(plane, "normal", StringComparison.OrdinalIgnoreCase),
            ClickThrough = NodeValueHelper.GetBool(properties, "clickThrough", true),
            Opacity = Math.Clamp(NodeValueHelper.GetDouble(properties, "opacity", 0.9), 0.05, 1),
            BackgroundColor = NodeValueHelper.ResolveTemplateProperty(context, properties, "backgroundColor", "#000000"),
            Motion = NodeValueHelper.GetString(properties, "motion", "none"),
            FadeInMs = Math.Max(0, NodeValueHelper.GetInt(properties, "fadeInMs", 120)),
            FadeOutMs = Math.Max(0, NodeValueHelper.GetInt(properties, "fadeOutMs", 120))
        };
    }

    internal static NodeResult OverlayResult(string kind, OverlayDisplayOptions options)
    {
        return NodeResult.Ok("out", new Dictionary<string, object?>
        {
            ["overlayKind"] = kind,
            ["durationMs"] = options.DurationMs,
            ["fullScreen"] = options.Bounds.FullScreen
        });
    }
}

[NodeInfo(
    TypeId = "action.overlayImage",
    DisplayName = "Overlay Image",
    Category = NodeCategory.Action,
    Description = "Shows an image overlay with fit, background, motion, timer, and fullscreen controls",
    Color = "#22C55E")]
public sealed class OverlayImageNode : IActionNode
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.overlayImage",
        DisplayName = "Overlay Image",
        Category = NodeCategory.Action,
        Description = "Shows an image overlay with fit, background, motion, timer, and fullscreen controls",
        Color = "#22C55E",
        InputPorts = OverlayColorNode.FlowInput(),
        OutputPorts = OverlayColorNode.FlowOutput(),
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "imagePath", Name = "Image Path", Type = PropertyType.FilePath, DefaultValue = "", Description = "Image file to show" },
            new() { Id = "fit", Name = "Fit", Type = PropertyType.Dropdown, DefaultValue = "contain", Description = "How the image fits the overlay area", Options = new[] { "contain", "cover", "stretch", "none" } },
            new() { Id = "backgroundColor", Name = "Background", Type = PropertyType.Color, DefaultValue = "#000000", Description = "Background behind the image" },
        }.Concat(OverlayColorNode.CommonOverlayProperties()).ToList()
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _properties = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var baseOptions = OverlayColorNode.BuildDisplayOptions(context, _properties);
        var options = new OverlayImageOptions
        {
            Bounds = baseOptions.Bounds,
            DurationMs = baseOptions.DurationMs,
            WaitForClose = baseOptions.WaitForClose,
            TopMost = baseOptions.TopMost,
            ClickThrough = baseOptions.ClickThrough,
            Opacity = baseOptions.Opacity,
            BackgroundColor = baseOptions.BackgroundColor,
            Motion = baseOptions.Motion,
            FadeInMs = baseOptions.FadeInMs,
            FadeOutMs = baseOptions.FadeOutMs,
            ImagePath = NodeValueHelper.ResolveTemplateProperty(context, _properties, "imagePath", ""),
            Fit = NodeValueHelper.GetString(_properties, "fit", "contain")
        };

        await OverlayDisplayService.ShowImageAsync(options, ct);
        return OverlayColorNode.OverlayResult("image", options);
    }
}

[NodeInfo(
    TypeId = "action.overlayText",
    DisplayName = "Overlay Text",
    Category = NodeCategory.Action,
    Description = "Shows fully customizable text on top of the desktop",
    Color = "#22C55E")]
public sealed class OverlayTextNode : IActionNode
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.overlayText",
        DisplayName = "Overlay Text",
        Category = NodeCategory.Action,
        Description = "Shows fully customizable text on top of the desktop",
        Color = "#22C55E",
        InputPorts = OverlayColorNode.FlowInput(),
        OutputPorts = OverlayColorNode.FlowOutput(),
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "text", Name = "Text", Type = PropertyType.String, DefaultValue = "Sidekick", Description = "Text to show. Supports {{variable}} templates." },
            new() { Id = "fontFamily", Name = "Font Family", Type = PropertyType.String, DefaultValue = "Segoe UI", Description = "Installed Windows font family" },
            new() { Id = "fontSize", Name = "Font Size", Type = PropertyType.Float, DefaultValue = 48, Description = "Font size in pixels" },
            new() { Id = "textColor", Name = "Text Color", Type = PropertyType.Color, DefaultValue = "#FFFFFF", Description = "Text color" },
            new() { Id = "backgroundColor", Name = "Background", Type = PropertyType.Color, DefaultValue = "#000000", Description = "Text overlay background" },
            new() { Id = "horizontalAlign", Name = "Horizontal Align", Type = PropertyType.Dropdown, DefaultValue = "center", Options = new[] { "left", "center", "right", "stretch" } },
            new() { Id = "verticalAlign", Name = "Vertical Align", Type = PropertyType.Dropdown, DefaultValue = "center", Options = new[] { "top", "center", "bottom", "stretch" } },
            new() { Id = "effect", Name = "Text Effect", Type = PropertyType.Dropdown, DefaultValue = "shadow", Options = new[] { "none", "shadow", "outline" } },
        }.Concat(OverlayColorNode.CommonOverlayProperties()).ToList()
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _properties = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var baseOptions = OverlayColorNode.BuildDisplayOptions(context, _properties);
        var options = new OverlayTextOptions
        {
            Bounds = baseOptions.Bounds,
            DurationMs = baseOptions.DurationMs,
            WaitForClose = baseOptions.WaitForClose,
            TopMost = baseOptions.TopMost,
            ClickThrough = baseOptions.ClickThrough,
            Opacity = baseOptions.Opacity,
            BackgroundColor = baseOptions.BackgroundColor,
            Motion = baseOptions.Motion,
            FadeInMs = baseOptions.FadeInMs,
            FadeOutMs = baseOptions.FadeOutMs,
            Text = NodeValueHelper.ResolveTemplateProperty(context, _properties, "text", ""),
            FontFamily = NodeValueHelper.GetString(_properties, "fontFamily", "Segoe UI"),
            FontSize = Math.Max(1, NodeValueHelper.GetDouble(_properties, "fontSize", 48)),
            TextColor = NodeValueHelper.ResolveTemplateProperty(context, _properties, "textColor", "#FFFFFF"),
            HorizontalAlign = NodeValueHelper.GetString(_properties, "horizontalAlign", "center"),
            VerticalAlign = NodeValueHelper.GetString(_properties, "verticalAlign", "center"),
            Effect = NodeValueHelper.GetString(_properties, "effect", "shadow")
        };

        await OverlayDisplayService.ShowTextAsync(options, ct);
        return OverlayColorNode.OverlayResult("text", options);
    }
}
