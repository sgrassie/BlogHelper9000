namespace BlogHelper9000.Core.Models;

public enum DeleteDraftOutcome
{
    Deleted,
    NotFound,
    NotADraft
}

public sealed record DeleteDraftResult(DeleteDraftOutcome Outcome, string? FilePath);
