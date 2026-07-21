using BlogHelper9000.Imaging;

namespace BlogHelper9000.Tests.Imaging;

public class UnsplashImageResultTests
{
    private static UnsplashImageResult Result(string photographerName) =>
        new(Stream.Null, "photo-1", "https://unsplash.com/photos/photo-1",
            photographerName, "janedoe", "https://unsplash.com/@janedoe", "A description");

    [Fact]
    public void Attribution_WhenPhotographerNamePresent_CreditsThem()
    {
        var result = Result("Jane Doe");

        result.Attribution.Should().Be("Photo by Jane Doe on Unsplash");
    }

    [Fact]
    public void Attribution_WhenPhotographerNameEmpty_FallsBackToGenericUnsplashCredit()
    {
        var result = Result("");

        result.Attribution.Should().Be("Background image by Unsplash");
    }

    [Fact]
    public void Attribution_WhenPhotographerNameWhitespace_FallsBackToGenericUnsplashCredit()
    {
        var result = Result("   ");

        result.Attribution.Should().Be("Background image by Unsplash");
    }
}
