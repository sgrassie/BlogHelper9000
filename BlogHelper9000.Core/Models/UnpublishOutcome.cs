namespace BlogHelper9000.Core.Models;

public enum UnpublishOutcome
{
    Unpublished,
    NotFound,
    NotPublished,
    TargetExists
}

public sealed record UnpublishPostResult(UnpublishOutcome Outcome, string? DraftPath);
