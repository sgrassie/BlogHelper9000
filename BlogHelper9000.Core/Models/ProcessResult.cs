namespace BlogHelper9000.Core.Models;

/// <summary>
/// Result of shelling out to an external process via <see cref="Services.IProcessRunner"/>.
/// </summary>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut);
