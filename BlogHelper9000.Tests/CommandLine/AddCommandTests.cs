using System.CommandLine;
using System.CommandLine.IO;
using BlogHelper9000.CommandLine;
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
        var commandFactory = new CommandFactory();
        await commandFactory.RootCommand().InvokeAsync("add -h", console);
        var consoleOut = _consoleHelper(console, _testOutputHelper);
        
        consoleOut
            .Should().Contain("add <title> [options]");
    }

    [Fact]
    public async Task Should_Accept_PostTitle()
    {
        var console = new TestConsole();
        var commandFactory = new CommandFactory();
        
        await commandFactory.RootCommand().InvokeAsync("add \"Some shiny new blog post\"", console);
        var consoleOut = _consoleHelper(console, _testOutputHelper);

        consoleOut.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_Accept_DraftFlag()
    {
        var console = new TestConsole();
        var commandFactory = new CommandFactory();

        await commandFactory.RootCommand().InvokeAsync("add \"New post in draft\" --draft", console);
        var consoleOut = _consoleHelper(console, _testOutputHelper);
        
        consoleOut.Should().NotBeNull();
    }
}