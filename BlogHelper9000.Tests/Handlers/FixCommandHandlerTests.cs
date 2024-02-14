using System.CommandLine.IO;
using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Handlers;
using BlogHelper9000.Tests.Helpers;

namespace BlogHelper9000.Tests.Handlers;

public class FixCommandHandlerTests
{
    [Fact]
    public void Should_Update_PublishedOn_To_DateInFilename()
    {
        var header = @"
---
published: 01/01/2000
---
";
        var fileSystem = new JekyllBlogFilesystemBuilder()
            .AddFiles(new Dictionary<string, MockFileData>
            {
                { "/blog/_posts/2024-02-13-a-post.md", new MockFileData(header) }
            })
            .BuildFileSystem();
        
        var console = new TestConsole();
        var sut = new FixCommandHandler(fileSystem, "/blog", console);
        
        sut.Execute(true, false, false, false);

        var contents = fileSystem.File.ReadAllText("/blog/_posts/2024-02-13-a-post.md");

        contents.Should().Contain("13/02/2024");
    }
}