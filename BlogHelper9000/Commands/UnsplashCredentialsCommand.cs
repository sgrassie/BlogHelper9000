using System.Text.Json;
using BlogHelper9000.Models;
using TimeWarp.Mediator;

namespace BlogHelper9000.Commands;

public class UnsplashCredentialsCommand : IRequest
{
    public string AccessKey { get; set; }
    public string SecretKey { get; set; }

    public class Handler(ILogger<Handler> logger, IFileSystem fileSystem) : IRequestHandler<UnsplashCredentialsCommand>
    {
        public Task Handle(UnsplashCredentialsCommand request, CancellationToken cancellationToken)
        {
            var credentials = $"{request.AccessKey}:{request.SecretKey}";
            var model = new AppDataModel { UnsplashCredentials = credentials };
            var json = JsonSerializer.Serialize(model);
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "bloghelper9000.json");
            fileSystem.File.WriteAllText(path, json);
            logger.LogInformation("Unsplash credentials set.");
            return Task.CompletedTask;
        }
    }
}