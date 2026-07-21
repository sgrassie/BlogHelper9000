namespace BlogHelper9000.Core.Models;

/// <summary>
/// Snapshot of the blog repo's git state relative to its upstream, as reported by
/// read-only porcelain commands. <see cref="PendingPostFiles"/> is the union of
/// <see cref="UncommittedFiles"/> and <see cref="UnpushedFiles"/>, filtered to
/// publish-related paths (<c>_posts/</c>, <c>_drafts/</c>, <c>assets/images/</c>).
/// </summary>
public sealed record DeployState(
    bool IsGitRepo,
    bool HasUpstream,
    IReadOnlyList<string> UncommittedFiles,
    int UnpushedCommits,
    IReadOnlyList<string> UnpushedFiles,
    IReadOnlyList<string> PendingPostFiles);
