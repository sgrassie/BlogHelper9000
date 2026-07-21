using BlogHelper9000.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlogHelper9000.Core.Services;

/// <summary>
/// Reports git deploy state and performs deploys for the blog repo at
/// <see cref="BlogHelperOptions.BaseDirectory"/> by shelling out to <c>git</c> via
/// <see cref="IProcessRunner"/>. No LibGit2Sharp — this is a handful of porcelain commands,
/// and process invocation is already an established injectable seam in this codebase (see
/// SQLite/<see cref="System.IO.Abstractions.IFileSystem"/>).
/// </summary>
public sealed class GitDeployStateService : IDeployStateService
{
    private static readonly string[] PublishPrefixes = ["_posts/", "_drafts/", "assets/images/"];

    private readonly IProcessRunner _runner;
    private readonly string _baseDirectory;
    private readonly ILogger<GitDeployStateService> _logger;

    public GitDeployStateService(IProcessRunner runner, IOptions<BlogHelperOptions> options, ILogger<GitDeployStateService> logger)
    {
        _runner = runner;
        _baseDirectory = options.Value.BaseDirectory;
        _logger = logger;
    }

    public DeployState GetDeployState() => Probe().State;

    public DeployResult Deploy(bool dryRun = true, string? message = null)
    {
        var (state, failedCommand) = Probe();

        if (!state.IsGitRepo)
            return new DeployResult(DeployOutcome.NotARepo, dryRun, [], null, false, null);

        // A broken `status`/`diff` means the picture we have of the repo can't be trusted —
        // surfacing NothingToDeploy or a bogus staged-file list here would be worse than
        // failing loudly. GetDeployState() itself only logs a warning and reports what it
        // knows (see Probe), since it has no failure outcome to return; Deploy does.
        if (failedCommand is not null)
            return new DeployResult(DeployOutcome.GitFailed, dryRun, [], null, false, failedCommand.StandardError);

        if (!state.HasUpstream)
            return new DeployResult(DeployOutcome.NoUpstream, dryRun, [], null, false, null);

        var stageable = state.UncommittedFiles.Where(IsPublishRelated).Distinct().ToList();

        if (stageable.Count == 0 && state.UnpushedCommits == 0)
            return new DeployResult(DeployOutcome.NothingToDeploy, dryRun, [], null, false, null);

        var commitMessage = stageable.Count > 0 ? message ?? GenerateCommitMessage(stageable) : null;

        if (dryRun)
            return new DeployResult(DeployOutcome.Deployed, true, stageable, commitMessage, false, null);

        if (stageable.Count > 0)
        {
            var add = RunGit(["add", "-A", "--", .. stageable]);
            if (add.ExitCode != 0)
            {
                _logger.LogError("git add failed: {Error}", add.StandardError);
                return new DeployResult(DeployOutcome.GitFailed, false, stageable, commitMessage, false, add.StandardError);
            }

            var commit = RunGit(["commit", "-m", commitMessage!]);
            if (commit.ExitCode != 0)
            {
                _logger.LogError("git commit failed: {Error}", commit.StandardError);
                return new DeployResult(DeployOutcome.GitFailed, false, stageable, commitMessage, false, commit.StandardError);
            }
        }

        var push = RunGit(["push"]);
        if (push.ExitCode != 0)
        {
            _logger.LogError("git push failed: {Error}", push.StandardError);
            return new DeployResult(DeployOutcome.GitFailed, false, stageable, commitMessage, false, push.StandardError);
        }

        _logger.LogInformation("Deployed {Count} staged file(s) to upstream", stageable.Count);
        return new DeployResult(DeployOutcome.Deployed, false, stageable, commitMessage, true, null);
    }

    /// <summary>
    /// Runs the read-only status commands and reports both the resulting <see cref="DeployState"/>
    /// and, if <c>status</c> or <c>diff</c> failed (non-zero exit or timeout), the failed
    /// <see cref="ProcessResult"/> so <see cref="Deploy"/> can refuse to act on an unreliable
    /// picture of the repo. <see cref="GetDeployState"/> deliberately discards that second
    /// value — it has no failure outcome to report, so it logs a warning and returns whatever
    /// it could determine (untrustworthy fields come back empty, exactly like <c>NotARepo</c>).
    /// </summary>
    private (DeployState State, ProcessResult? FailedCommand) Probe()
    {
        var revParse = RunGit(["rev-parse", "--is-inside-work-tree"]);
        if (revParse.ExitCode != 0 || revParse.TimedOut)
        {
            _logger.LogWarning("{Directory} is not a git repository, or git is unavailable", _baseDirectory);
            return (new DeployState(false, false, [], 0, [], []), null);
        }

        var status = RunGit(["status", "--porcelain=v1"]);
        if (status.ExitCode != 0 || status.TimedOut)
        {
            _logger.LogWarning("git status failed in {Directory}: {Error}", _baseDirectory, status.StandardError);
            return (new DeployState(true, false, [], 0, [], []), status);
        }

        var uncommittedFiles = ParsePorcelainStatus(status.StandardOutput);

        var revList = RunGit(["rev-list", "--count", "@{upstream}..HEAD"]);
        var hasUpstream = revList.ExitCode == 0 && !revList.TimedOut;

        var unpushedCommits = 0;
        IReadOnlyList<string> unpushedFiles = [];
        ProcessResult? failedCommand = null;

        if (hasUpstream)
        {
            unpushedCommits = int.TryParse(revList.StandardOutput.Trim(), out var count) ? count : 0;

            var diff = RunGit(["diff", "--name-only", "@{upstream}..HEAD"]);
            if (diff.ExitCode != 0 || diff.TimedOut)
            {
                _logger.LogWarning("git diff failed in {Directory}: {Error}", _baseDirectory, diff.StandardError);
                failedCommand = diff;
            }
            else
            {
                unpushedFiles = ParseNameOnlyLines(diff.StandardOutput);
            }
        }

        var pendingPostFiles = uncommittedFiles
            .Concat(unpushedFiles)
            .Where(IsPublishRelated)
            .Distinct()
            .ToList();

        var state = new DeployState(true, hasUpstream, uncommittedFiles, unpushedCommits, unpushedFiles, pendingPostFiles);
        return (state, failedCommand);
    }

    private ProcessResult RunGit(IReadOnlyList<string> arguments)
    {
        try
        {
            return _runner.Run("git", arguments, _baseDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "git invocation failed: git {Arguments}", string.Join(' ', arguments));
            return new ProcessResult(-1, string.Empty, ex.Message, false);
        }
    }

    private static bool IsPublishRelated(string path) =>
        PublishPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.Ordinal));

    private static string GenerateCommitMessage(IReadOnlyList<string> stagedFiles)
    {
        var postFiles = stagedFiles
            .Where(f => f.StartsWith("_posts/", StringComparison.Ordinal) || f.StartsWith("_drafts/", StringComparison.Ordinal))
            .Select(f => f.Split('/')[^1])
            .ToList();

        return postFiles.Count > 0
            ? $"publish: {string.Join(", ", postFiles)}"
            : "publish: blog content update";
    }

    /// <summary>
    /// Parses `git status --porcelain=v1` output. Each line is <c>XY PATH</c> (path starts
    /// at column 4). Only rename/copy lines — identified by an <c>R</c> or <c>C</c> in either
    /// status column, per the porcelain v1 format, never by sniffing the path text — are
    /// <c>OLDPATH -&gt; NEWPATH</c>; for those we split on the LAST " -&gt; " (an old path can
    /// itself legitimately contain a literal " -&gt; ", e.g. from a previous rename, so the
    /// final occurrence is the only one guaranteed to precede the real new path) and take the
    /// new path. Non-rename lines are never split on " -&gt; ", even if the filename happens to
    /// contain that text. Paths git wraps in double quotes (for special characters) are
    /// unwrapped — this strips only the surrounding quote characters; any C-style backslash
    /// escapes git introduced inside the quoted text (e.g. `\"`, `\\`) are left intact as-is.
    /// </summary>
    private static IReadOnlyList<string> ParsePorcelainStatus(string output)
    {
        var results = new List<string>();

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length < 4) continue;

            var isRenameOrCopy = line[0] is 'R' or 'C' || line[1] is 'R' or 'C';
            var pathPart = line[3..];

            string path;
            if (isRenameOrCopy)
            {
                var arrowIndex = pathPart.LastIndexOf(" -> ", StringComparison.Ordinal);
                path = arrowIndex >= 0 ? pathPart[(arrowIndex + 4)..] : pathPart;
            }
            else
            {
                path = pathPart;
            }

            path = StripQuotes(path);

            if (path.Length > 0) results.Add(path);
        }

        return results;
    }

    private static IReadOnlyList<string> ParseNameOnlyLines(string output) =>
        output.Split('\n')
            .Select(line => StripQuotes(line.TrimEnd('\r').Trim()))
            .Where(line => line.Length > 0)
            .ToList();

    /// <summary>
    /// Strips the surrounding double quotes git adds around a porcelain path when it contains
    /// special characters. Only the outer quote characters are removed — any C-style escapes
    /// git introduced inside (e.g. `\"` for an embedded quote, `\\` for a literal backslash)
    /// are left exactly as git printed them, unescaped.
    /// </summary>
    private static string StripQuotes(string value) =>
        value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;
}
