using System.CommandLine.IO;
using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Handlers;
using BlogHelper9000.Helpers;
using BlogHelper9000.Tests.Helpers;

namespace BlogHelper9000.Tests.Handlers;

public class InfoCommandHandlerTests
{
    private const string _file1 = """
                                  ---
                                  published: 01/01/2000
                                  ---
                                  """;
    
    private const string _file2 = """
                                  ---
                                  published: 01/02/2000
                                  ---
                                  """;
    [Fact(Skip = "How to test this now?")]
    public void Should_GetCorrectPostCount()
    {
        var fileSystem = new JekyllBlogFilesystemBuilder()
            .AddFiles(new Dictionary<string, MockFileData>
            {
                { "/blog/_posts/2000-01-01-first-post.md", new MockFileData(_file1) },
                { "/blog/_posts/2000-02-01-second-post.md", new MockFileData(_file2) }
            }).BuildFileSystem();
        var testConsole = new TestConsole();
        var sut = new InfoCommandHandler(new PostManager(fileSystem, "/blog"), testConsole);
        
        sut.Execute();
    }
}