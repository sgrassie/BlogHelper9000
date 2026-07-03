namespace BlogHelper9000.Core.Models;

public enum PublishOutcome
{
    Published,
    NotFound,
    AlreadyPublished,
    TargetExists
}

public sealed record PublishPostResult(PublishOutcome Outcome, string? PublishedPath);
