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
    private const string UnsplashPhotosApiUrl = "https://api.unsplash.com/photos";

    // The Unsplash API returns snake_case field names (e.g. "alt_description", "download_location")
    // which don't match the PascalCase model properties without an explicit naming policy.
    private static readonly JsonSerializerOptions UnsplashJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public async Task<UnsplashImageResult?> LoadImageAsync(string query, string? photoId = null, CancellationToken cancellationToken = default)
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

        var fullUrl = string.IsNullOrEmpty(photoId)
            ? $"{UnsplashPhotosApiUrl}/random?query={Uri.EscapeDataString(query)}&client_id={clientId}"
            : $"{UnsplashPhotosApiUrl}/{Uri.EscapeDataString(photoId)}?client_id={clientId}";

        if (string.IsNullOrEmpty(photoId))
        {
            logger.LogInformation("Loading random Unsplash image for the query '{ImageQuery}'", query);
        }
        else
        {
            logger.LogInformation("Loading Unsplash image by id '{PhotoId}'", photoId);
        }

        using var response = await httpClient.GetAsync(fullUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var unsplashData = await response.Content.ReadFromJsonAsync<UnsplashData>(UnsplashJsonOptions, cancellationToken);
        if (unsplashData is null)
        {
            logger.LogError("Unsplash returned an empty response for query '{ImageQuery}'", query);
            return null;
        }

        var imageUrl = $"{unsplashData.Urls.Raw}&w=1280&h=720&fit=min";
        using var imageResponse = await httpClient.GetAsync(imageUrl, cancellationToken);
        imageResponse.EnsureSuccessStatusCode();

        var imageBytes = await imageResponse.Content.ReadAsByteArrayAsync(cancellationToken);

        await PingDownloadLocationAsync(unsplashData.Links?.DownloadLocation, clientId, cancellationToken);

        return new UnsplashImageResult(
            new MemoryStream(imageBytes),
            unsplashData.Id ?? string.Empty,
            unsplashData.Links?.Html ?? string.Empty,
            unsplashData.User?.Name ?? string.Empty,
            unsplashData.User?.Username ?? string.Empty,
            unsplashData.User?.Links?.Html ?? string.Empty,
            unsplashData.Description ?? unsplashData.AltDescription);
    }

    /// <summary>
    /// Unsplash API guidelines require pinging the photo's download_location endpoint whenever
    /// the photo is actually used. This is best-effort: failures are logged and swallowed so
    /// they never affect the caller's result.
    /// </summary>
    private async Task PingDownloadLocationAsync(string? downloadLocation, string clientId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(downloadLocation))
        {
            return;
        }

        try
        {
            var separator = downloadLocation.Contains('?') ? '&' : '?';
            var pingUrl = $"{downloadLocation}{separator}client_id={clientId}";
            using var response = await httpClient.GetAsync(pingUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to ping Unsplash download location {DownloadLocation}", downloadLocation);
        }
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
