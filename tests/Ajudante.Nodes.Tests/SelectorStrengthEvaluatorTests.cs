using System.Drawing;
using Ajudante.Platform.UIAutomation;

namespace Ajudante.Nodes.Tests;

public class SelectorStrengthEvaluatorTests
{
    [Fact]
    public void Evaluate_AutomationIdWithProcessAndWindow_ReturnsStrong()
    {
        var element = new ElementInfo
        {
            AutomationId = "continue",
            ProcessName = "Trae",
            WindowTitle = "Trae",
            ControlType = "Button"
        };

        Assert.Equal(SelectorStrength.Strong, SelectorStrengthEvaluator.Evaluate(element));
        Assert.Equal(SelectorStrategy.AutomationId, SelectorStrengthEvaluator.SuggestStrategy(element));
    }

    [Fact]
    public void Evaluate_NameControlTypeAndProcess_ReturnsMedium()
    {
        var element = new ElementInfo
        {
            Name = "Continue",
            ControlType = "Button",
            ProcessPath = @"%LOCALAPPDATA%\Programs\Trae\Trae.exe"
        };

        Assert.Equal(SelectorStrength.Medium, SelectorStrengthEvaluator.Evaluate(element));
        Assert.Equal(SelectorStrategy.Name, SelectorStrengthEvaluator.SuggestStrategy(element));
    }

    [Fact]
    public void SuggestStrategy_UsesRelativePositionBeforeAbsolute()
    {
        var element = new ElementInfo
        {
            BoundingRect = new Rectangle(110, 120, 80, 30),
            WindowBounds = new Rectangle(100, 100, 800, 600)
        };

        Assert.Equal(SelectorStrategy.RelativePosition, SelectorStrengthEvaluator.SuggestStrategy(element));
        Assert.Equal(new Rectangle(10, 20, 80, 30), element.RelativeBoundingRect);
    }
}
