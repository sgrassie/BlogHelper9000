using System.IO.Abstractions;
using System.Net.Http.Json;
using System.Text.Json;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Models;
using BlogHelper9000.Imaging.Unsplash;
using Microsoft.Extensions.Logging;

namespace BlogHelper9000.Imaging;

public class UnsplashClient(HttpClient httpClient, IFileSystem fileSystem, ILogger logger) : IUnsplashClient
{
    private const string UnsplashApiUrl = "https://api.unsplash.com/photos/random";

    public async Task<Stream?> LoadImageAsync(string query, CancellationToken cancellationToken = default)
    {
        var credentials = LoadCredentials();
        if (credentials is null)
        {
            logger.LogError("Could not load Unsplash image because credentials are missing");
            return null;
        }

        var credentialParts = credentials.UnsplashCredentials.Split(':');
        if (credentialParts.Length < 1 || string.IsNullOrWhiteSpace(credentialParts[0]))
        {
            logger.LogError("Unsplash credentials file is malformed");
            return null;
        }

        var clientId = credentialParts[0];
        var fullUrl = $"{UnsplashApiUrl}?query={Uri.EscapeDataString(query)}&client_id={clientId}";

        logger.LogInformation("Loading random Unsplash image for the query '{ImageQuery}'", query);

        using var response = await httpClient.GetAsync(fullUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var unsplashData = await response.Content.ReadFromJsonAsync<UnsplashData>(cancellationToken: cancellationToken);
        if (unsplashData is null)
        {
            logger.LogError("Unsplash returned an empty response for query '{ImageQuery}'", query);
            return null;
        }

        var imageUrl = $"{unsplashData.Urls.Raw}&w=1280&h=720&fit=min";
        using var imageResponse = await httpClient.GetAsync(imageUrl, cancellationToken);
        imageResponse.EnsureSuccessStatusCode();

        var imageBytes = await imageResponse.Content.ReadAsByteArrayAsync(cancellationToken);
        return new MemoryStream(imageBytes);
    }

    private AppDataModel? LoadCredentials()
    {
        var credentialsPath = CredentialsPaths.UnsplashCredentialsPath(fileSystem);
        if (!fileSystem.File.Exists(credentialsPath))
        {
            logger.LogError("Could not find Unsplash credentials file at {CredentialsPath}", credentialsPath);
            return null;
        }

        var json = fileSystem.File.ReadAllText(credentialsPath);
        return JsonSerializer.Deserialize<AppDataModel>(json);
    }
}
