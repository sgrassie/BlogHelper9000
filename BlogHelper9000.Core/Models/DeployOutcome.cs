namespace BlogHelper9000.Core.Models;

public enum DeployOutcome
{
    Deployed,
    NothingToDeploy,
    NotARepo,
    NoUpstream,
    GitFailed
}

/// <summary>
/// Result of a <see cref="Services.IDeployStateService.Deploy"/> call. On a dry run,
/// <see cref="StagedFiles"/> and <see cref="CommitMessage"/> reflect what would happen, but
/// no staging/commit/push commands are executed and <see cref="Pushed"/> is always false.
/// </summary>
public sealed record DeployResult(
    DeployOutcome Outcome,
    bool DryRun,
    IReadOnlyList<string> StagedFiles,
    string? CommitMessage,
    bool Pushed,
    string? Error);
