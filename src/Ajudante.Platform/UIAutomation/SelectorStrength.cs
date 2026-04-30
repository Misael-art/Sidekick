namespace Ajudante.Platform.UIAutomation;

public enum SelectorStrength
{
    Weak = 0,
    Medium = 1,
    Strong = 2
}

public enum SelectorStrategy
{
    AutomationId,
    Name,
    RelativePosition,
    ImageFallback,
    AbsoluteCoordinates,
    None
}

public static class SelectorStrengthEvaluator
{
    public static SelectorStrength Evaluate(ElementInfo element)
    {
        ArgumentNullException.ThrowIfNull(element);

        var hasProcessScope = !string.IsNullOrWhiteSpace(element.ProcessName)
            || !string.IsNullOrWhiteSpace(element.ProcessPath);
        var hasAutomationId = !string.IsNullOrWhiteSpace(element.AutomationId);
        var hasName = !string.IsNullOrWhiteSpace(element.Name);
        var hasControlType = !string.IsNullOrWhiteSpace(element.ControlType);
        var hasWindow = !string.IsNullOrWhiteSpace(element.WindowTitle);

        if (hasAutomationId && hasProcessScope && hasWindow)
            return SelectorStrength.Strong;

        if ((hasAutomationId && hasWindow) || (hasName && hasControlType && hasProcessScope))
            return SelectorStrength.Medium;

        return SelectorStrength.Weak;
    }

    public static SelectorStrategy SuggestStrategy(ElementInfo element)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (!string.IsNullOrWhiteSpace(element.AutomationId))
            return SelectorStrategy.AutomationId;

        if (!string.IsNullOrWhiteSpace(element.Name) && !string.IsNullOrWhiteSpace(element.ControlType))
            return SelectorStrategy.Name;

        if (!element.RelativeBoundingRect.IsEmpty)
            return SelectorStrategy.RelativePosition;

        if (!element.BoundingRect.IsEmpty)
            return SelectorStrategy.AbsoluteCoordinates;

        return SelectorStrategy.None;
    }

    public static string Explain(ElementInfo element)
    {
        ArgumentNullException.ThrowIfNull(element);

        return Evaluate(element) switch
        {
            SelectorStrength.Strong => "Seletor forte: AutomationId com janela e processo conhecidos.",
            SelectorStrength.Medium when !string.IsNullOrWhiteSpace(element.AutomationId) =>
                "Seletor medio: AutomationId disponivel; adicionar processPath aumenta a robustez.",
            SelectorStrength.Medium =>
                "Seletor medio: nome visivel, tipo de controle e processo ajudam, mas texto pode mudar.",
            _ when !element.RelativeBoundingRect.IsEmpty =>
                "Seletor fraco: use posicao relativa ou fallback visual se a UIAutomation falhar.",
            _ => "Seletor fraco: sem identificadores estaveis; considere Snip/fallback visual."
        };
    }

    public static string ToPublicLabel(SelectorStrength strength)
    {
        return strength switch
        {
            SelectorStrength.Strong => "forte",
            SelectorStrength.Medium => "media",
            _ => "fraca"
        };
    }

    public static string ToPublicStrategy(SelectorStrategy strategy)
    {
        return strategy switch
        {
            SelectorStrategy.AutomationId => "selector",
            SelectorStrategy.Name => "selector",
            SelectorStrategy.RelativePosition => "relative",
            SelectorStrategy.ImageFallback => "image",
            SelectorStrategy.AbsoluteCoordinates => "absolute",
            _ => "manual"
        };
    }
}
