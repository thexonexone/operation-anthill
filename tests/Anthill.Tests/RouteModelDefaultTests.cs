using System.Text.Json;
using Anthill.Core.Configuration;
using Anthill.Core.Models;
using Xunit;

namespace Anthill.Tests;

/// <summary>
/// The Ollama model is a default for the LOCAL provider and for nothing else. v0.3.8.41.
///
/// Routing every ant to Claude Code produced routes reading `agent:claude-code : gemma4:31b` — an
/// agent paired with a local model tag it has never heard of and cannot serve. Two separate lines
/// did it, both spelling `?? AnthillRuntime.OllamaModel`:
///
///   • RoleRoute defaulted a stored route's missing model to the Ollama tag.
///   • GetClient defaulted an unknown provider's model to the Ollama tag.
///
/// It was never only an agent problem. A keyed OpenAI route with no model would have carried
/// gemma4:31b just as happily, and the symptom — a provider running fine while the console named a
/// model nothing was using — is the kind that reads as a display bug for a long time.
///
/// These assert through the ROUTER rather than by reading fields, because the defect was in what
/// the router returned, and a test that re-implemented the defaulting rule would agree with
/// whichever version of it was written last.
/// </summary>
[Collection("Autonomy")]
public class RouteModelDefaultTests : IDisposable
{
    private readonly Dictionary<string, Dictionary<string, string>> _routes =
        AnthillRuntime.ModelRouting.ToDictionary(kv => kv.Key, kv => new Dictionary<string, string>(kv.Value));
    private readonly string _ollamaModel = AnthillRuntime.OllamaModel;

    // These are process-wide statics, so anything set here is put back — the shared-static leak this
    // repository serialises suites for. Restored through the same production path used to set them.
    public void Dispose()
    {
        Apply(_routes);
        AnthillRuntime.OllamaModel = _ollamaModel;
    }

    private static ModelRouter Router() => new();

    /// <summary>
    /// Routes are written through ApplySettingsUpdate — the path POST /settings uses — rather than
    /// by assigning the static. ModelRouting is `private set` for exactly that reason, and going
    /// through the real writer means these tests exercise the code an operator's change travels.
    /// </summary>
    private static void Apply(Dictionary<string, Dictionary<string, string>> routes) =>
        AnthillRuntime.ApplySettingsUpdate(new Dictionary<string, JsonElement>
        {
            ["model_routes"] = JsonSerializer.SerializeToElement(routes),
        });

    private static void Route(string role, string provider, string? model)
    {
        var entry = new Dictionary<string, string> { ["provider"] = provider };
        if (model is not null) entry["model"] = model;
        Apply(new Dictionary<string, Dictionary<string, string>> { [role] = entry });
    }

    /// <summary>
    /// The case that produced the bug report: a provider that is not the local one, and no model.
    /// It must NOT inherit the Ollama tag.
    /// </summary>
    [Fact]
    public void ANonLocalProviderWithNoModel_DoesNotInheritTheOllamaTag()
    {
        AnthillRuntime.OllamaModel = "gemma4:31b";
        Route("coder", "agent:claude-code", model: null);

        var (provider, model) = Router().RoleRoute("coder");

        Assert.Equal("agent:claude-code", provider);
        Assert.NotEqual("gemma4:31b", model);
        Assert.True(string.IsNullOrEmpty(model),
            $"expected the provider to decide its own model, got '{model}'");
    }

    /// <summary>
    /// The same rule protects keyed providers, which is how we know the fix is about PROVIDERS and
    /// not a special case bolted on for agents.
    /// </summary>
    [Fact]
    public void AKeyedProviderWithNoModel_DoesNotInheritTheOllamaTag()
    {
        AnthillRuntime.OllamaModel = "gemma4:31b";
        Route("coder", "openai", model: null);

        var (_, model) = Router().RoleRoute("coder");

        Assert.NotEqual("gemma4:31b", model);
    }

    /// <summary>
    /// The LOCAL provider still gets the local default. Removing that would be the opposite
    /// regression, and it is the behaviour the rest of the colony has always relied on.
    /// </summary>
    [Fact]
    public void TheLocalProviderWithNoModel_StillGetsTheLocalDefault()
    {
        AnthillRuntime.OllamaModel = "gemma4:31b";
        Route("coder", AnthillRuntime.DefaultModelProvider, model: null);

        var (_, model) = Router().RoleRoute("coder");

        Assert.Equal("gemma4:31b", model);
    }

    /// <summary>An explicitly chosen model is never second-guessed, whoever serves it.</summary>
    [Theory]
    [InlineData("agent:claude-code", "Claude Code")]
    [InlineData("openai", "gpt-4o")]
    [InlineData("ollama", "qwen3.6:27b")]
    public void AnExplicitModel_IsAlwaysKept(string provider, string model)
    {
        AnthillRuntime.OllamaModel = "gemma4:31b";
        Route("coder", provider, model);

        var (p, m) = Router().RoleRoute("coder");

        Assert.Equal(provider, p);
        Assert.Equal(model, m);
    }
}
