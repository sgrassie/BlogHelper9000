using System.IO.Abstractions.TestingHelpers;
using System.Net;
using System.Text;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Models;
using BlogHelper9000.Imaging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlogHelper9000.Tests.Imaging;

public class UnsplashClientTests
{
    private const string FullPhotoJson = """
        {
            "id": "photo-123",
            "description": "A mountain range",
            "alt_description": "mountains at dusk",
            "urls": { "raw": "https://images.example/photo" },
            "links": {
                "self": "https://api.unsplash.com/photos/photo-123",
                "html": "https://unsplash.com/photos/photo-123",
                "download": "https://unsplash.com/photos/photo-123/download",
                "download_location": "https://api.unsplash.com/photos/photo-123/download"
            },
            "user": {
                "id": "user-1",
                "username": "janedoe",
                "name": "Jane Doe",
                "links": {
                    "self": "https://api.unsplash.com/users/janedoe",
                    "html": "https://unsplash.com/@janedoe",
                    "download": "",
                    "download_location": ""
                }
            }
        }
        """;

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public readonly List<Uri> RequestedUris = [];
        public string PhotoJson { get; set; } = """{"urls":{"raw":"https://images.example/photo"}}""";
        public bool FailDownloadLocationPing { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUris.Add(request.RequestUri!);

            HttpResponseMessage response;
            if (request.RequestUri!.AbsolutePath.Contains("/download"))
            {
                response = FailDownloadLocationPing
                    ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    : new HttpResponseMessage(HttpStatusCode.OK);
            }
            else if (request.RequestUri!.AbsolutePath.Contains("/photos/random") ||
                     request.RequestUri!.AbsolutePath.StartsWith("/photos/photo-"))
            {
                response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(PhotoJson, Encoding.UTF8, "application/json")
                };
            }
            else
            {
                response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([1, 2, 3, 4])
                };
            }

            return Task.FromResult(response);
        }
    }

    private static MockFileSystem CreateFileSystemWithCredentials(string credentials = "abc123:secret")
    {
        var fileSystem = new MockFileSystem();
        var path = CredentialsPaths.UnsplashCredentialsPath(fileSystem);
        fileSystem.AddFile(path, new MockFileData($$"""{"UnsplashCredentials":"{{credentials}}"}"""));
        return fileSystem;
    }

    [Fact]
    public async Task LoadImageAsync_Should_Send_The_Supplied_Query()
    {
        var handler = new RecordingHandler();
        var httpClient = new HttpClient(handler);
        var fileSystem = CreateFileSystemWithCredentials();
        var sut = new UnsplashClient(httpClient, fileSystem, NullLogger.Instance);

        await sut.LoadImageAsync("mountains");

        handler.RequestedUris.Should().Contain(u => u.Query.Contains("query=mountains"));
    }

    [Fact]
    public async Task LoadImageAsync_Should_Reuse_The_Same_HttpClient_For_Search_And_Download()
    {
        var handler = new RecordingHandler();
        var httpClient = new HttpClient(handler);
        var fileSystem = CreateFileSystemWithCredentials();
        var sut = new UnsplashClient(httpClient, fileSystem, NullLogger.Instance);

        await sut.LoadImageAsync("mountains");

        handler.RequestedUris.Should().Contain(u => u.Host == "images.example");
    }

    [Fact]
    public async Task LoadImageAsync_Should_Return_Null_When_Credentials_Missing()
    {
        var handler = new RecordingHandler();
        var httpClient = new HttpClient(handler);
        var fileSystem = new MockFileSystem();
        var sut = new UnsplashClient(httpClient, fileSystem, NullLogger.Instance);

        var result = await sut.LoadImageAsync("mountains");

        result.Should().BeNull();
        handler.RequestedUris.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadImageAsync_Should_Return_Null_When_Credentials_Malformed()
    {
        var handler = new RecordingHandler();
        var httpClient = new HttpClient(handler);
        var fileSystem = CreateFileSystemWithCredentials(credentials: "");
        var sut = new UnsplashClient(httpClient, fileSystem, NullLogger.Instance);

        var result = await sut.LoadImageAsync("mountains");

        result.Should().BeNull();
    }

    [Fact]
    public async Task LoadImageAsync_Should_Populate_Attribution_Fields_From_The_Response()
    {
        var handler = new RecordingHandler { PhotoJson = FullPhotoJson };
        var httpClient = new HttpClient(handler);
        var fileSystem = CreateFileSystemWithCredentials();
        var sut = new UnsplashClient(httpClient, fileSystem, NullLogger.Instance);

        var result = await sut.LoadImageAsync("mountains");

        result.Should().NotBeNull();
        result!.PhotoId.Should().Be("photo-123");
        result.PhotoUrl.Should().Be("https://unsplash.com/photos/photo-123");
        result.PhotographerName.Should().Be("Jane Doe");
        result.PhotographerUsername.Should().Be("janedoe");
        result.PhotographerProfileUrl.Should().Be("https://unsplash.com/@janedoe");
        result.Description.Should().Be("A mountain range");
    }

    [Fact]
    public async Task LoadImageAsync_Should_Fall_Back_To_AltDescription_When_Description_Is_Missing()
    {
        const string json = """
            {
                "id": "photo-123",
                "alt_description": "mountains at dusk",
                "urls": { "raw": "https://images.example/photo" },
                "links": { "html": "https://unsplash.com/photos/photo-123" },
                "user": { "name": "Jane Doe", "username": "janedoe" }
            }
            """;
        var handler = new RecordingHandler { PhotoJson = json };
        var httpClient = new HttpClient(handler);
        var fileSystem = CreateFileSystemWithCredentials();
        var sut = new UnsplashClient(httpClient, fileSystem, NullLogger.Instance);

        var result = await sut.LoadImageAsync("mountains");

        result!.Description.Should().Be("mountains at dusk");
    }

    [Fact]
    public async Task LoadImageAsync_Should_Use_EmptyStrings_When_User_Or_Links_Are_Missing()
    {
        const string json = """{"id":"photo-123","urls":{"raw":"https://images.example/photo"}}""";
        var handler = new RecordingHandler { PhotoJson = json };
        var httpClient = new HttpClient(handler);
        var fileSystem = CreateFileSystemWithCredentials();
        var sut = new UnsplashClient(httpClient, fileSystem, NullLogger.Instance);

        var result = await sut.LoadImageAsync("mountains");

        result.Should().NotBeNull();
        result!.PhotoUrl.Should().BeEmpty();
        result.PhotographerName.Should().BeEmpty();
        result.PhotographerUsername.Should().BeEmpty();
        result.PhotographerProfileUrl.Should().BeEmpty();
        result.Description.Should().BeNull();
    }

    [Fact]
    public async Task LoadImageAsync_Should_Request_The_Photo_By_Id_When_PhotoId_Supplied()
    {
        var handler = new RecordingHandler { PhotoJson = FullPhotoJson };
        var httpClient = new HttpClient(handler);
        var fileSystem = CreateFileSystemWithCredentials();
        var sut = new UnsplashClient(httpClient, fileSystem, NullLogger.Instance);

        var result = await sut.LoadImageAsync("ignored-query", photoId: "photo-123");

        result.Should().NotBeNull();
        handler.RequestedUris.Should().Contain(u => u.AbsolutePath == "/photos/photo-123");
        handler.RequestedUris.Should().NotContain(u => u.AbsolutePath.Contains("/photos/random"));
    }

    [Fact]
    public async Task LoadImageAsync_Should_Ping_The_DownloadLocation_On_Success()
    {
        var handler = new RecordingHandler { PhotoJson = FullPhotoJson };
        var httpClient = new HttpClient(handler);
        var fileSystem = CreateFileSystemWithCredentials();
        var sut = new UnsplashClient(httpClient, fileSystem, NullLogger.Instance);

        await sut.LoadImageAsync("mountains");

        handler.RequestedUris.Should().Contain(u =>
            u.GetLeftPart(UriPartial.Path) == "https://api.unsplash.com/photos/photo-123/download");
    }

    [Fact]
    public async Task LoadImageAsync_Should_Not_Fail_When_The_DownloadLocation_Ping_Fails()
    {
        var handler = new RecordingHandler { PhotoJson = FullPhotoJson, FailDownloadLocationPing = true };
        var httpClient = new HttpClient(handler);
        var fileSystem = CreateFileSystemWithCredentials();
        var sut = new UnsplashClient(httpClient, fileSystem, NullLogger.Instance);

        var result = await sut.LoadImageAsync("mountains");

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task LoadImageAsync_Should_Not_Ping_DownloadLocation_When_Missing()
    {
        const string json = """{"id":"photo-123","urls":{"raw":"https://images.example/photo"}}""";
        var handler = new RecordingHandler { PhotoJson = json };
        var httpClient = new HttpClient(handler);
        var fileSystem = CreateFileSystemWithCredentials();
        var sut = new UnsplashClient(httpClient, fileSystem, NullLogger.Instance);

        await sut.LoadImageAsync("mountains");

        handler.RequestedUris.Should().HaveCount(2);
    }
}
