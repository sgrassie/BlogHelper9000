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
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public readonly List<Uri> RequestedUris = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUris.Add(request.RequestUri!);

            HttpResponseMessage response;
            if (request.RequestUri!.AbsolutePath.Contains("/photos/random"))
            {
                var json = """{"urls":{"raw":"https://images.example/photo"}}""";
                response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
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

        handler.RequestedUris.Should().HaveCount(2);
        handler.RequestedUris[1].Host.Should().Be("images.example");
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
}
