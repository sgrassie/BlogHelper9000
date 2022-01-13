using BlogHelper9000.Commands.Inputs;

namespace BlogHelper9000.Commands;

public class ImageCommand : AsyncBaseCommand<ImageInput>
{
    private string _imagesRoot = string.Empty;
    
    public ImageCommand()
    {
        Usage("Default");
        Usage("Update images across all posts")
            .Arguments(
                a => a.ImageQuery,
                a => a.ApplyAllFlag, 
                a => a.ApplyAllFlag,
                a => a.ApplyToDrafts,
                a => a.ApplyToPosts,
                a => a.BaseDirectoryFlag);
    }
    protected override async Task<bool> Run(ImageInput input)
    {
        await using var stream = await UnsplashImageFetcher.FetchImageAsync(input.ImageQuery);

        return true;
    }

    private class ImageProcessor
    {
        
    }

    private class UnsplashImageFetcher : IDisposable
    {
        private readonly string _requestUrl;
        private readonly HttpClient _httpClient;
        
        private UnsplashImageFetcher(string query)
        {
            _requestUrl = $"https://source.unsplash.com/random/1280x720?{query}";
            _httpClient = new HttpClient();
        }

        public static Task<Stream> FetchImageAsync(string query)
        {
            var fetcher = new UnsplashImageFetcher(query);
            return fetcher.FetchImage();
        }

        private Task<Stream> FetchImage()
        {
            return _httpClient.GetStreamAsync(_requestUrl);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}