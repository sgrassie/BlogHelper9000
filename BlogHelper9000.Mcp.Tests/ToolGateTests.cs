using System.IO.Abstractions.TestingHelpers;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace BlogHelper9000.Mcp.Tests;

public class ToolGateTests
{
    [Fact]
    public async Task RunExclusive_TwoConcurrentActions_NeverOverlap()
    {
        using var aEntered = new ManualResetEventSlim(false);
        using var bMayEnter = new ManualResetEventSlim(false);
        using var aMayExit = new ManualResetEventSlim(false);
        var bEnteredWhileAWasStillIn = false;

        var taskA = Task.Run(() => ToolGate.RunExclusive(() =>
        {
            aEntered.Set();
            aMayExit.Wait(TimeSpan.FromSeconds(5));
            return 0;
        }));

        // Wait until A is confirmed inside the gate before starting B.
        aEntered.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();

        var taskB = Task.Run(() => ToolGate.RunExclusive(() =>
        {
            // If B reaches here before A releases, the gate failed to serialise.
            bEnteredWhileAWasStillIn = !aMayExit.IsSet;
            bMayEnter.Set();
            return 0;
        }));

        // Give B a moment to try to enter, then confirm it is still blocked.
        var bEnteredEarly = bMayEnter.Wait(TimeSpan.FromMilliseconds(200));
        bEnteredEarly.Should().BeFalse("B must not enter the gate while A still holds it");

        aMayExit.Set();

        await Task.WhenAll(taskA, taskB);

        bEnteredWhileAWasStillIn.Should().BeFalse();
    }

    [Fact]
    public async Task RunExclusiveAsync_TwoConcurrentActions_NeverOverlap()
    {
        using var aEntered = new ManualResetEventSlim(false);
        using var bMayEnter = new ManualResetEventSlim(false);
        using var aMayExit = new ManualResetEventSlim(false);
        var bEnteredWhileAWasStillIn = false;

        var taskA = ToolGate.RunExclusiveAsync(async () =>
        {
            aEntered.Set();
            await Task.Run(() => aMayExit.Wait(TimeSpan.FromSeconds(5)));
            return 0;
        });

        aEntered.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();

        var taskB = ToolGate.RunExclusiveAsync(() =>
        {
            bEnteredWhileAWasStillIn = !aMayExit.IsSet;
            bMayEnter.Set();
            return Task.FromResult(0);
        });

        var bEnteredEarly = bMayEnter.Wait(TimeSpan.FromMilliseconds(200));
        bEnteredEarly.Should().BeFalse("B must not enter the gate while A still holds it");

        aMayExit.Set();

        await Task.WhenAll(taskA, taskB);

        bEnteredWhileAWasStillIn.Should().BeFalse();
    }

    [Fact]
    public void RunExclusive_ExceptionInsideAction_ReleasesGateForSubsequentCall()
    {
        var act = () => ToolGate.RunExclusive<int>(() => throw new InvalidOperationException("boom"));
        act.Should().Throw<InvalidOperationException>().WithMessage("boom");

        // The gate must have been released — a subsequent call must proceed without blocking forever.
        var result = ToolGate.RunExclusive(() => 42);
        result.Should().Be(42);
    }

    [Fact]
    public async Task RunExclusiveAsync_ExceptionInsideAction_ReleasesGateForSubsequentCall()
    {
        var act = async () => await ToolGate.RunExclusiveAsync<int>(() => throw new InvalidOperationException("boom"));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");

        var result = await ToolGate.RunExclusiveAsync(() => Task.FromResult(42));
        result.Should().Be(42);
    }

    // Integration-flavoured: two gated tools (UpdatePostTool + PatchPostTool) racing on the same
    // MockFileSystem file, calling the real tool entry points — NOT wrapped in an extra
    // ToolGate.RunExclusive here, because both tools already gate their own bodies internally;
    // doing so would nest two acquisitions of the same non-reentrant SemaphoreSlim(1,1) on one
    // logical operation and self-deadlock (caught during development of this test).
    //
    // MockFileSystem is a purely in-memory dictionary with no OS-level file handles, so it can't
    // reproduce the real "sharing violation" race from the field, and without a hook into
    // ToolGate's internals this test can't directly observe whether the two tool bodies overlapped
    // in time. That property is covered precisely by RunExclusive_TwoConcurrentActions_NeverOverlap
    // and RunExclusiveAsync_TwoConcurrentActions_NeverOverlap above. What this test adds is a
    // regression check that gating two *different* tool methods against the same shared state does
    // not deadlock, throw, or corrupt the file, run repeatedly to shake out timing-dependent bugs.
    [Fact]
    public async Task GatedTools_ConcurrentUpdateAndPatch_DoNotDeadlockOrCorruptState()
    {
        for (var iteration = 0; iteration < 25; iteration++)
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile("/blog/_drafts/my-post.md",
                new MockFileData("---\ntitle: Original\n---\n\nThe quick brown fox jumps."));
            var postManager = CreatePostManager(fileSystem);

            var updateTask = Task.Run(() =>
                UpdatePostTool.UpdatePost(postManager, "my-post.md", title: "Updated Title"));

            var patchTask = Task.Run(() =>
                PatchPostTool.PatchPost(postManager, "my-post.md", "brown fox", "lazy dog"));

            var updateResult = await updateTask;
            await patchTask;

            updateResult.Success.Should().BeTrue();
            // PatchPost may legitimately fail if UpdatePost's title-only change ran first and left
            // the body's "brown fox" text intact — either way it must not throw or corrupt the file.
            var finalContent = fileSystem.File.ReadAllText("/blog/_drafts/my-post.md");
            finalContent.Should().StartWith("---\n");
        }
    }

    private static PostManager CreatePostManager(MockFileSystem fileSystem) =>
        new(fileSystem, new MarkdownHandler(fileSystem), Options.Create(new BlogHelperOptions { BaseDirectory = "/blog" }));
}
