using BlogHelper9000.Core.Models;

namespace BlogHelper9000.Core.Services;

public interface IDeployStateService
{
    /// <summary>
    /// Reports the blog repo's git state: uncommitted changes, whether an upstream is
    /// configured, and how far HEAD is ahead of it. Never throws — a repo that can't be
    /// inspected (missing binary, not a git repo, timeout) reports <c>IsGitRepo: false</c>
    /// with all other fields empty/zero.
    /// </summary>
    DeployState GetDeployState();

    /// <summary>
    /// Stages publish-related changes (<c>_posts/</c>, <c>_drafts/</c>,
    /// <c>assets/images/</c>), commits them, and pushes. When <paramref name="dryRun"/> is
    /// true (the default), only read-only status commands run — no <c>add</c>/<c>commit</c>/
    /// <c>push</c> is ever invoked, and the result reports what would happen.
    /// </summary>
    DeployResult Deploy(bool dryRun = true, string? message = null);
}
