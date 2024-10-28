using System.Text.Json;
using BlogHelper9000.Models;

namespace BlogHelper9000.Handlers;

public class UnsplashCredentialsCommandHandler
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger _logger;

    public UnsplashCredentialsCommandHandler(IFileSystem fileSystem, ILogger logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public void Execute(string accessKey, string secretKey)
    {
        var credentials = $"{accessKey}:{secretKey}";
        var model = new AppDataModel { UnsplashCredentials = credentials };
        var json = JsonSerializer.Serialize(model);
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "bloghelper9000.json");
        _fileSystem.File.WriteAllText(path, json);
        _logger.LogInformation("Unsplash credentials set.");
    }
}