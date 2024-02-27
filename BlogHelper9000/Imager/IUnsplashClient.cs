namespace BlogHelper9000.Imager;

public interface IUnsplashClient
{
    Task<Stream> LoadImageAsync(string query);
}