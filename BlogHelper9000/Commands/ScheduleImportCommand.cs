using BlogHelper9000.Core;
using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Scheduling;
using Microsoft.Extensions.Options;
using TimeWarp.Nuru;

namespace BlogHelper9000.Commands;

// [NuruRouteGroup] is silently ignored by the Nuru generator in 3.0.0-beta.71, so all
// schedule commands use hyphenated single-literal routes until groups work upstream.
[NuruRoute("schedule-import", Description = "Convert a publishing-schedule spreadsheet into the blog's schedule database")]
public sealed class ScheduleImportCommand : ICommand<Unit>
{
    [Parameter(Description = "Path to the .xlsx publishing schedule.")]
    public string Path { get; set; }

    [Option("force", "f", Description = "Replace an existing schedule database.")]
    public bool Force { get; set; }

    public sealed class Handler(ILogger<Handler> logger, IFileSystem fileSystem, IOptions<BlogHelperOptions> options)
        : ICommandHandler<ScheduleImportCommand, Unit>
    {
        public ValueTask<Unit> Handle(ScheduleImportCommand request, CancellationToken cancellationToken)
        {
            if (!fileSystem.File.Exists(request.Path))
            {
                logger.LogError("Spreadsheet not found at {Path}", request.Path);
                return default;
            }

            var databasePath = ScheduleDatabase.PathFor(options.Value.BaseDirectory);
            if (fileSystem.File.Exists(databasePath))
            {
                if (!request.Force)
                {
                    logger.LogError(
                        "A schedule database already exists at {Path} — re-run with --force to replace it",
                        databasePath);
                    return default;
                }
                fileSystem.File.Delete(databasePath);
            }

            using var stream = fileSystem.File.OpenRead(request.Path);
            using var database = ScheduleDatabase.Open(options.Value.BaseDirectory);

            var summary = ScheduleXlsxImporter.Import(stream, database);

            foreach (var (series, entries) in summary.SeriesCounts)
                logger.LogInformation("Imported {Series}: {Entries} entries", series, entries);
            logger.LogInformation("Schedule database written to {Path}", databasePath);

            return default;
        }
    }
}
