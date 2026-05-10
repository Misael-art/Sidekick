using Ajudante.Core.Engine;

namespace Ajudante.Core.Tests;

public sealed class LogRedactorTests
{
    [Fact]
    public void Redact_masks_common_secret_shapes()
    {
        const string input = "password=minhaSenha token: abc123 Authorization: Bearer xyz sk-proj-live-key";

        var redacted = LogRedactor.Redact(input);

        Assert.DoesNotContain("minhaSenha", redacted);
        Assert.DoesNotContain("abc123", redacted);
        Assert.DoesNotContain("Bearer xyz", redacted);
        Assert.DoesNotContain("sk-proj-live-key", redacted);
        Assert.Contains("password=***", redacted);
        Assert.Contains("token: ***", redacted);
    }

    [Fact]
    public void Redact_keeps_non_secret_message_readable()
    {
        const string input = "Flow executado com sucesso em 2 passos.";

        var redacted = LogRedactor.Redact(input);

        Assert.Equal(input, redacted);
    }
}
