namespace BlogHelper9000.Imager;

public class UnsplashClient(ILogger logger) : IDisposable, IUnsplashClient
{
    private const string RequestUrl = "https://source.unsplash.com/random/1280x720?";
    private readonly HttpClient _httpClient = new();

    public Task<Stream> LoadImageAsync(string query)
    {
        var request = RequestUrl + query;
        logger.LogInformation("Loading random Unsplash image for the query '{ImageQuery}'", query);
        return _httpClient.GetStreamAsync(request);
    }
    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _httpClient.Dispose();
    }
}
