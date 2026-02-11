using System.Text.Json;
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
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "bloghelper9000.json");
            fileSystem.File.WriteAllText(path, json);
            logger.LogInformation("Unsplash credentials set.");
            return default;
        }
    }
}
