using Anthill.Core.Configuration;
using Anthill.Modules.Homelab;
using Xunit;

namespace Anthill.Tests.Homelab;

/// <summary>
/// Container control, and the three gates in front of it. v0.3.8.40.
///
/// Every test here asserts a REFUSAL, and that is the whole point at this stage. This runner can
/// stop and restart containers on a real host; the valuable property is not that it works but that
/// it declines in every state where it must, and says why. A test suite for this that only proved
/// the happy path would be proving the least important half.
///
/// Nothing here starts docker. Each refusal is reached before any process would be launched, which
/// is also why these run identically on a laptop with no docker installed and on CI.
/// </summary>
[Collection("runtime-config")]
public class DockerActionRunnerTests : IDisposable
{
    private readonly DeploymentMode _mode = AnthillRuntime.Deployment;
    private readonly bool _exec = AnthillRuntime.DockerExecuteEnabled;
    private readonly string _reason = AnthillRuntime.DeploymentReason;

    // The runtime is process-wide static, so anything moved here must be put back or the next test
    // inherits it — a shared-static leak is exactly the flake this repository serialises for.
    public void Dispose()
    {
        AnthillRuntime.Deployment = _mode;
        AnthillRuntime.DockerExecuteEnabled = _exec;
        AnthillRuntime.DeploymentReason = _reason;
    }

    private static ActionProposal Proposal(string type = "restart_container", string target = "web") => new()
    {
        ActionType = type,
        TargetKind = "container",
        TargetId = target,
        Title = "test",
    };

    private static DockerActionRunner Runner() => new();

    [Theory]
    [InlineData("start_container")]
    [InlineData("stop_container")]
    [InlineData("restart_container")]
    public void ItClaims_TheContainerLifecycleActions(string type) =>
        Assert.True(Runner().CanRun(Proposal(type)));

    /// <summary>
    /// It must not claim another runner's work. CanRun is first-match-wins in ActionExecutor, so a
    /// runner that over-claims silently shadows the one that should have acted — the defect
    /// v2.3.1.1 records about the mock runner hiding Proxmox.
    /// </summary>
    [Theory]
    [InlineData("start_vm", "vm")]
    [InlineData("restart_service", "service")]
    [InlineData("run_backup", "vm")]
    public void ItDoesNotClaim_AnotherRunnersWork(string type, string targetKind)
    {
        var p = Proposal(type);
        p.TargetKind = targetKind;

        Assert.False(Runner().CanRun(p));
    }

    /// <summary>
    /// Gate 1. A desktop colony refuses outright, and names the mode and the reason it holds it —
    /// otherwise an operator on a laptop sees "refused" with nothing to act on.
    /// </summary>
    [Fact]
    public async Task OnADesktop_ItRefuses_AndSaysWhy()
    {
        AnthillRuntime.Deployment = DeploymentMode.Desktop;
        AnthillRuntime.DeploymentReason = "No container signals found.";
        AnthillRuntime.DockerExecuteEnabled = true;   // even fully enabled, the mode still refuses

        var dry = await Runner().DryRunAsync(Proposal());
        var exec = await Runner().ExecuteAsync(Proposal());

        Assert.False(dry.Ok);
        Assert.False(exec.Ok);
        Assert.Contains("desktop", dry.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No container signals found.", dry.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Gate 3, and the one that is easy to get wrong. The target becomes a process ARGUMENT, so a
    /// name like `-v` or `--privileged` cannot inject a shell command — but docker would read it as
    /// an OPTION. Requiring a leading alphanumeric is what stops an argument becoming a flag.
    /// </summary>
    [Theory]
    [InlineData("-v")]
    [InlineData("--privileged")]
    [InlineData("--volume=/:/host")]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("semi;colon")]
    public async Task AnUnsafeContainerName_IsRefused(string target)
    {
        AnthillRuntime.Deployment = DeploymentMode.Server;
        AnthillRuntime.DockerExecuteEnabled = true;

        var result = await Runner().ExecuteAsync(Proposal(target: target));

        Assert.False(result.Ok);
        Assert.Contains("valid container name", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("web")]
    [InlineData("my-app_1")]
    [InlineData("a.b-c_d")]
    [InlineData("0abc")]
    public async Task AnOrdinaryContainerName_PassesTheNameGuard(string target)
    {
        AnthillRuntime.Deployment = DeploymentMode.Server;
        AnthillRuntime.DockerExecuteEnabled = false;   // stop at the execute gate, not the name gate

        var result = await Runner().ExecuteAsync(Proposal(target: target));

        Assert.False(result.Ok);
        // Refused for the EXECUTE gate, which proves the name was accepted on the way past.
        Assert.Contains("execution is disabled", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gate 2. Execution off is the default, and it must stop EXECUTE without stopping DRY RUN —
    /// seeing what would happen is precisely how an operator decides whether to enable it.
    /// </summary>
    [Fact]
    public async Task WithExecutionDisabled_ExecuteRefuses_AndSaysHowToEnableIt()
    {
        AnthillRuntime.Deployment = DeploymentMode.Server;
        AnthillRuntime.DockerExecuteEnabled = false;

        var result = await Runner().ExecuteAsync(Proposal());

        Assert.False(result.Ok);
        Assert.Contains("docker_execute_enabled", result.Message, StringComparison.Ordinal);
        Assert.Contains("Dry run is available", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A dry run never refuses for the EXECUTE gate. It may fail for any other reason — no docker,
    /// no such container — but "execution is disabled" must not be the answer, because that is the
    /// question the dry run exists to help answer.
    /// </summary>
    [Fact]
    public async Task DryRun_IsNotBlockedByTheExecuteGate()
    {
        AnthillRuntime.Deployment = DeploymentMode.Server;
        AnthillRuntime.DockerExecuteEnabled = false;

        var result = await Runner().DryRunAsync(Proposal(target: "anthill-no-such-container"));

        Assert.DoesNotContain("Container execution is disabled", result.Message, StringComparison.Ordinal);
    }
}
