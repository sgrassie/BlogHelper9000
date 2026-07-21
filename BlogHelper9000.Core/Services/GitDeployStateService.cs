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

    public DeployState GetDeployState()
    {
        var revParse = RunGit("rev-parse --is-inside-work-tree");
        if (revParse.ExitCode != 0 || revParse.TimedOut)
        {
            _logger.LogWarning("{Directory} is not a git repository, or git is unavailable", _baseDirectory);
            return new DeployState(false, false, [], 0, [], []);
        }

        var status = RunGit("status --porcelain=v1");
        var uncommittedFiles = ParsePorcelainStatus(status.StandardOutput);

        var revList = RunGit("rev-list --count @{upstream}..HEAD");
        var hasUpstream = revList.ExitCode == 0 && !revList.TimedOut;

        var unpushedCommits = 0;
        IReadOnlyList<string> unpushedFiles = [];

        if (hasUpstream)
        {
            unpushedCommits = int.TryParse(revList.StandardOutput.Trim(), out var count) ? count : 0;

            var diff = RunGit("diff --name-only @{upstream}..HEAD");
            unpushedFiles = ParseNameOnlyLines(diff.StandardOutput);
        }

        var pendingPostFiles = uncommittedFiles
            .Concat(unpushedFiles)
            .Where(IsPublishRelated)
            .Distinct()
            .ToList();

        return new DeployState(true, hasUpstream, uncommittedFiles, unpushedCommits, unpushedFiles, pendingPostFiles);
    }

    public DeployResult Deploy(bool dryRun = true, string? message = null)
    {
        var state = GetDeployState();

        if (!state.IsGitRepo)
            return new DeployResult(DeployOutcome.NotARepo, dryRun, [], null, false, null);

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
            var add = RunGit(BuildAddArguments(stageable));
            if (add.ExitCode != 0)
            {
                _logger.LogError("git add failed: {Error}", add.StandardError);
                return new DeployResult(DeployOutcome.GitFailed, false, stageable, commitMessage, false, add.StandardError);
            }

            var commit = RunGit($"commit -m {QuoteArg(commitMessage!)}");
            if (commit.ExitCode != 0)
            {
                _logger.LogError("git commit failed: {Error}", commit.StandardError);
                return new DeployResult(DeployOutcome.GitFailed, false, stageable, commitMessage, false, commit.StandardError);
            }
        }

        var push = RunGit("push");
        if (push.ExitCode != 0)
        {
            _logger.LogError("git push failed: {Error}", push.StandardError);
            return new DeployResult(DeployOutcome.GitFailed, false, stageable, commitMessage, false, push.StandardError);
        }

        _logger.LogInformation("Deployed {Count} staged file(s) to upstream", stageable.Count);
        return new DeployResult(DeployOutcome.Deployed, false, stageable, commitMessage, true, null);
    }

    private ProcessResult RunGit(string arguments)
    {
        try
        {
            return _runner.Run("git", arguments, _baseDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "git invocation failed: git {Arguments}", arguments);
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

    private static string BuildAddArguments(IReadOnlyList<string> paths) =>
        $"add -A -- {string.Join(' ', paths.Select(QuoteArg))}";

    private static string QuoteArg(string value) => $"\"{value}\"";

    /// <summary>
    /// Parses `git status --porcelain=v1` output. Each line is <c>XY PATH</c> (path starts
    /// at column 4); rename/copy lines are <c>XY OLDPATH -&gt; NEWPATH</c>, from which we take
    /// the new path. Paths git wraps in double quotes (for special characters) are unwrapped.
    /// </summary>
    private static IReadOnlyList<string> ParsePorcelainStatus(string output)
    {
        var results = new List<string>();

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length < 4) continue;

            var pathPart = line[3..];
            var arrowIndex = pathPart.IndexOf(" -> ", StringComparison.Ordinal);
            var path = arrowIndex >= 0 ? pathPart[(arrowIndex + 4)..] : pathPart;
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

    private static string StripQuotes(string value) =>
        value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;
}
