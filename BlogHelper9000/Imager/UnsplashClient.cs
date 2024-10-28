using System.Net;
using System.Text.Json;
using BlogHelper9000.Models;

namespace BlogHelper9000.Imager;

public class UnsplashClient(ILogger logger) : IDisposable, IUnsplashClient
{
    private const string UnsplashApiUrl = "https://api.unsplash.com/photos/random?query=";
    private const string UnsplashRequestVersion = "v1";
    private readonly HttpClient _httpClient = new();

    public Task<Stream> LoadImageAsync(string query)
    {
        var client = CreateUnsplashClient();
        if (client != null)
        {
            var request = UnsplashApiUrl + query;
            logger.LogInformation("Loading random Unsplash image for the query '{ImageQuery}'", query);
            return _httpClient.GetStreamAsync(request);
        }

        logger.LogError("Could not load Unsplash image because credentials are missing");
        return Task.FromResult(Stream.Null);
    }
    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _httpClient.Dispose();
    }
    
    private HttpClient? CreateUnsplashClient()
    {
        var credentials = LoadCredentials();
        if (credentials != null)
        {
            var credentialParts = credentials.UnsplashCredentials.Split(':');
            var client = new HttpClient();
            client.BaseAddress = new Uri(UnsplashApiUrl);
            client.DefaultRequestHeaders.Add("Accept-Version", UnsplashRequestVersion);
            client.DefaultRequestHeaders.Add("Authorization", $"Client-Id {credentialParts[0]}");
            return client;
        }

        logger.LogError("Could not create Unsplash client because credentials are missing");
        return null;
    }
    
    private AppDataModel? LoadCredentials()
    {
        var credentialsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "bloghelper9000.json");
        if (!File.Exists(credentialsPath))
        {
            logger.LogError("Could not find Unsplash credentials file at {CredentialsPath}", credentialsPath);
            return null;
        }

        var json = File.ReadAllText(credentialsPath);
        var credentials = JsonSerializer.Deserialize<AppDataModel>(json);
        return credentials;
    }
}

