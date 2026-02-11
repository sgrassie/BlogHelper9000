namespace BlogHelper9000.Imaging;

public interface IUnsplashClient
{
    Task<Stream> LoadImageAsync(string query);
}
