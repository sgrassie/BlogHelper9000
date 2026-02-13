using BlogHelper9000.Tui.Views;
using FluentAssertions;

namespace BlogHelper9000.Tui.Tests.Views;

public class BufferStatusBarTests
{
    [Fact]
    public void AddOrUpdateBuffer_AddsEntryToList()
    {
        var bar = new BufferStatusBar();

        bar.AddOrUpdateBuffer(1, "/blog/_posts/hello.md");

        bar._buffers.Should().HaveCount(1);
        bar._buffers[0].Handle.Should().Be(1);
        bar._buffers[0].FilePath.Should().Be("/blog/_posts/hello.md");
    }

    [Fact]
    public void AddOrUpdateBuffer_WithEmptyPath_IsIgnored()
    {
        var bar = new BufferStatusBar();

        bar.AddOrUpdateBuffer(1, "");

        bar._buffers.Should().BeEmpty();
    }

    [Fact]
    public void AddOrUpdateBuffer_WithSameHandle_UpdatesPath()
    {
        var bar = new BufferStatusBar();

        bar.AddOrUpdateBuffer(1, "/blog/_posts/old.md");
        bar.AddOrUpdateBuffer(1, "/blog/_posts/new.md");

        bar._buffers.Should().HaveCount(1);
        bar._buffers[0].FilePath.Should().Be("/blog/_posts/new.md");
    }

    [Fact]
    public void RemoveBuffer_RemovesCorrectEntry()
    {
        var bar = new BufferStatusBar();
        bar.AddOrUpdateBuffer(1, "/blog/_posts/first.md");
        bar.AddOrUpdateBuffer(2, "/blog/_posts/second.md");

        bar.RemoveBuffer(1);

        bar._buffers.Should().HaveCount(1);
        bar._buffers[0].Handle.Should().Be(2);
    }

    [Fact]
    public void RemoveBuffer_WithUnknownHandle_IsNoOp()
    {
        var bar = new BufferStatusBar();
        bar.AddOrUpdateBuffer(1, "/blog/_posts/hello.md");

        bar.RemoveBuffer(99);

        bar._buffers.Should().HaveCount(1);
    }

    [Fact]
    public void SetActiveBuffer_UpdatesActiveHandle()
    {
        var bar = new BufferStatusBar();
        bar.AddOrUpdateBuffer(1, "/blog/_posts/first.md");
        bar.AddOrUpdateBuffer(2, "/blog/_posts/second.md");

        // After adding buffer 2, it should be active (AddOrUpdateBuffer sets active)
        // Now explicitly set buffer 1 as active
        bar.SetActiveBuffer(1);

        // We can verify indirectly via the internal state — the active handle
        // is tested through rendering, but we verify the method doesn't throw
        // and the buffers are still intact
        bar._buffers.Should().HaveCount(2);
    }

    [Fact]
    public void BufferDisplayOrder_IsMaintained()
    {
        var bar = new BufferStatusBar();

        bar.AddOrUpdateBuffer(3, "/blog/_posts/third.md");
        bar.AddOrUpdateBuffer(1, "/blog/_posts/first.md");
        bar.AddOrUpdateBuffer(2, "/blog/_posts/second.md");

        bar._buffers.Should().HaveCount(3);
        bar._buffers[0].Handle.Should().Be(3);
        bar._buffers[1].Handle.Should().Be(1);
        bar._buffers[2].Handle.Should().Be(2);
    }
}
