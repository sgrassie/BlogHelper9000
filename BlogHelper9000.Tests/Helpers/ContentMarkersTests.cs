using BlogHelper9000.Core.Helpers;

namespace BlogHelper9000.Tests.Helpers;

public class ContentMarkersTests
{
    [Theory]
    [InlineData("Still need to write this. TODO: add examples.", "TODO")]
    [InlineData("FIXME: this paragraph doesn't make sense.", "FIXME")]
    [InlineData("XXX check this claim before publishing.", "XXX")]
    [InlineData("Publish date TBD.", "TBD")]
    public void FindMarkers_DetectsEachMarker(string content, string expectedMarker)
    {
        ContentMarkers.FindMarkers(content).Should().Contain(expectedMarker);
    }

    [Fact]
    public void FindMarkers_DetectsPlaceholderCaseInsensitively()
    {
        ContentMarkers.FindMarkers("Intro paragraph. [Placeholder for the diagram]")
            .Should().Contain("[placeholder");
    }

    [Fact]
    public void FindMarkers_MatchesPlaceholderRegardlessOfCase()
    {
        ContentMarkers.FindMarkers("[PLACEHOLDER: fill this in]")
            .Should().Contain("[placeholder");
    }

    [Fact]
    public void FindMarkers_IsCaseSensitiveForAllCapsMarkers_AvoidingProseFalsePositives()
    {
        ContentMarkers.FindMarkers("I still need to todo this and fixme later.")
            .Should().BeEmpty();
    }

    [Fact]
    public void FindMarkers_WordBoundaryMatch_DoesNotFireOnSubstringWithinAWord()
    {
        ContentMarkers.FindMarkers("See the TODOS.md file in the repo for the URL slug todos-list.")
            .Should().BeEmpty();
    }

    [Fact]
    public void FindMarkers_ReturnsDistinctMarkers_WhenRepeated()
    {
        ContentMarkers.FindMarkers("TODO: one thing. TODO: another thing.")
            .Should().Equal("TODO");
    }

    [Fact]
    public void FindMarkers_ReturnsEmpty_WhenContentHasNoMarkers()
    {
        ContentMarkers.FindMarkers("This post is finished and ready to publish.")
            .Should().BeEmpty();
    }

    [Fact]
    public void FindMarkers_ReturnsEmpty_ForEmptyContent()
    {
        ContentMarkers.FindMarkers(string.Empty).Should().BeEmpty();
    }

    [Fact]
    public void FindMarkers_ReturnsMultipleMarkers_WhenSeveralArePresent()
    {
        ContentMarkers.FindMarkers("TODO: finish intro. FIXME: broken link. Ending TBD. [placeholder image]")
            .Should().BeEquivalentTo("TODO", "FIXME", "TBD", "[placeholder");
    }
}
