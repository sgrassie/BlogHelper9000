using System.CommandLine;
using System.CommandLine.IO;
using BlogHelper9000.CommandLine;
using BlogHelper9000.Tests.Helpers;
using Xunit.Abstractions;

namespace BlogHelper9000.Tests.CommandLine;

public class AddCommandTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    private Func<TestConsole, ITestOutputHelper, string> _consoleHelper = (console, helper) =>
    {
        var consoleOut = console.Out.ToString();
        helper.WriteLine(consoleOut);
        return consoleOut;
    };

    public AddCommandTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }
    
    [Fact]
    public async Task Should_Output_Help()
    {
        var console = new TestConsole();
        var commandFactory = new AddCommand(JekyllBlogFilesystemFactory.FileSystem, new Option<string>("--base-directory"));
        await commandFactory.InvokeAsync("add -h", console);
        var consoleOut = _consoleHelper(console, _testOutputHelper);
        
        consoleOut
            .Should().Contain("add <title> [<tags>...] [options]");
    }

    [Fact]
    public async Task Should_Accept_PostTitle()
    {
        var console = new TestConsole();
        var commandFactory = new AddCommand(JekyllBlogFilesystemFactory.FileSystem, new Option<string>("--base-directory"));
        
        await commandFactory.InvokeAsync("add \"Some shiny new blog post\"", console);
        var consoleOut = _consoleHelper(console, _testOutputHelper);

        consoleOut.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_Accept_DraftFlag()
    {
        var console = new TestConsole();
        var commandFactory = new AddCommand(JekyllBlogFilesystemFactory.FileSystem, new Option<string>("--base-directory"));

        await commandFactory.InvokeAsync("add \"New post in draft\" --draft", console);
        var consoleOut = _consoleHelper(console, _testOutputHelper);
        
        consoleOut.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_Add_NewPost_AsDraft()
    {
        var fileSystem = JekyllBlogFilesystemFactory.FileSystem;
        var console = new TestConsole();
        var option = new Option<string>("--base-directory");
        option.AddAlias("-b");
        var addCommand = new AddCommand(JekyllBlogFilesystemFactory.FileSystem, option);
        addCommand.AddOption(option);

        await addCommand.InvokeAsync("add \"New post in draft\" --draft -b /blog", console);
        var consoleOut = _consoleHelper(console, _testOutputHelper);

        fileSystem
            .File
            .Exists(Path.Combine(JekyllBlogFilesystemFactory.Drafts, "new-post-in-draft.md"))
            .Should().BeTrue();
    }
    
    [Fact]
    public async Task Should_Add_NewPost_StraightToPosts()
    {
        var fileSystem = JekyllBlogFilesystemFactory.FileSystem;
        var console = new TestConsole();
        var option = new Option<string>("--base-directory");
        option.AddAlias("-b");
        var addCommand = new AddCommand(JekyllBlogFilesystemFactory.FileSystem, option);
        addCommand.AddOption(option);

        await addCommand.InvokeAsync("add \"New post in posts\" -b /blog", console);
        var consoleOut = _consoleHelper(console, _testOutputHelper);

        fileSystem
            .File
            .Exists(Path.Combine(JekyllBlogFilesystemFactory.Posts, "new-post-in-posts.md"))
            .Should().BeTrue();
    }
}