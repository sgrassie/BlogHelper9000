using System.Text.Json;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Models;
using TimeWarp.Nuru;

namespace BlogHelper9000.Commands;

public class UnsplashCredentialsCommand : ICommand<Unit>
{
    [Parameter(Description = "The access key for the Unsplash API.")]
    public string AccessKey { get; set; }
    [Parameter(Description = "The secret")]
    public string SecretKey { get; set; }

    public class Handler(ILogger<Handler> logger, IFileSystem fileSystem)
        : ICommandHandler<UnsplashCredentialsCommand, Unit>
    {
        public ValueTask<Unit> Handle(UnsplashCredentialsCommand request, CancellationToken cancellationToken)
        {
            var credentials = $"{request.AccessKey}:{request.SecretKey}";
            var model = new AppDataModel { UnsplashCredentials = credentials };
            var json = JsonSerializer.Serialize(model);
            var path = CredentialsPaths.UnsplashCredentialsPath(fileSystem);
            var directory = fileSystem.Path.GetDirectoryName(path)!;

            if (!fileSystem.Directory.Exists(directory))
                fileSystem.Directory.CreateDirectory(directory);

            fileSystem.File.WriteAllText(path, json);

            if (!OperatingSystem.IsWindows())
            {
                try
                {
                    System.IO.File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
                catch (IOException)
                {
                    // Best-effort permission hardening; not fatal if the underlying filesystem doesn't support it.
                }
            }

            logger.LogInformation("Unsplash credentials set.");
            return default;
        }
    }
}
