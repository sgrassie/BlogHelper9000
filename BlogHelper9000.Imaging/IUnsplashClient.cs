namespace BlogHelper9000.Imaging;

public sealed record UnsplashImageResult(
    Stream Image,
    string PhotoId,
    string PhotoUrl,
    string PhotographerName,
    string PhotographerUsername,
    string PhotographerProfileUrl,
    string? Description)
{
    /// <summary>
    /// The standard Unsplash attribution line, e.g. "Photo by Jane Doe on Unsplash". Falls back to
    /// the same generic wording <see cref="ImageProcessor"/> uses when Unsplash doesn't supply a
    /// photographer name, rather than emitting "Photo by  on Unsplash".
    /// </summary>
    public string Attribution => string.IsNullOrWhiteSpace(PhotographerName)
        ? "Background image by Unsplash"
        : $"Photo by {PhotographerName} on Unsplash";
}

public interface IUnsplashClient
{
    Task<UnsplashImageResult?> LoadImageAsync(string query, string? photoId = null, CancellationToken cancellationToken = default);
}
