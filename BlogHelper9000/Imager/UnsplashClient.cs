using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BlogHelper9000.Models;
using BlogHelper9000.Unsplash;

namespace BlogHelper9000.Imager;

public class UnsplashClient(ILogger logger) : IDisposable, IUnsplashClient
{
    private const string UnsplashApiUrl = "https://api.unsplash.com/photos/random";
    private const string UnsplashRequestVersion = "v1";
    private readonly HttpClient _httpClient = new();

    public async Task<Stream> LoadImageAsync(string query)
    {
        var (client, clientId) = CreateUnsplashClient();
        if (client != null)
        {
            var queryUrl = AddQueryTooUrl(query);
            var fullUrl = AddClientIdToUrl(queryUrl, clientId);
            logger.LogInformation("Loading random Unsplash image for the query '{ImageQuery}'", query);
            var unsplashData = await _httpClient.GetFromJsonAsync<UnsplashData>(fullUrl);
            if (unsplashData != null)
            {
                var imageUrl = $"{unsplashData.Urls.Raw}&w=1280&h=720&fit=min";
                var imageStream = await _httpClient.GetStreamAsync(imageUrl);
                return imageStream;
            }
        }

        logger.LogError("Could not load Unsplash image because credentials are missing");
        return Stream.Null;

        string AddQueryTooUrl(string query)
        {
            return $"{UnsplashApiUrl}?query=programming";
        }
        
        string AddClientIdToUrl(string url, string clientId)
        {
            return $"{url}&client_id={clientId}";
        }
    }
    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _httpClient.Dispose();
    }
    
    private (HttpClient? httpClient, string clientId) CreateUnsplashClient()
    {
        var credentials = LoadCredentials();
        if (credentials != null)
        {
            var credentialParts = credentials.UnsplashCredentials.Split(':');
            var clientId = credentialParts[0];
            var client = new HttpClient();
            client.BaseAddress = new Uri(UnsplashApiUrl);
            client.DefaultRequestHeaders.Add("Accept-Version", UnsplashRequestVersion);
            return (client, clientId);
        }

        logger.LogError("Could not create Unsplash client because credentials are missing");
        return (null, string.Empty);
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

