using BlogHelper9000.Nvim;
using FluentAssertions;

namespace BlogHelper9000.Nvim.Tests;

public class NvimClientEditFileTests
{
    [Theory]
    [InlineData("plain.md", "'plain.md'")]
    [InlineData("with space.md", "'with space.md'")]
    [InlineData("with'quote.md", "'with''quote.md'")]
    [InlineData("with|pipe.md", "'with|pipe.md'")]
    [InlineData("with\\backslash.md", "'with\\backslash.md'")]
    [InlineData("with\nnewline.md", "'with\nnewline.md'")]
    [InlineData("with\"doublequote.md", "'with\"doublequote.md'")]
    public void ToVimSingleQuotedString_Wraps_Path_As_Safe_Vim_Literal(string path, string expected)
    {
        var result = NvimClient.ToVimSingleQuotedString(path);

        result.Should().Be(expected);
    }

    [Fact]
    public void ToVimSingleQuotedString_Round_Trips_A_Path_Containing_Ex_Command_Metacharacters()
    {
        const string maliciousPath = "innocuous.md' | :! rm -rf / #";

        var result = NvimClient.ToVimSingleQuotedString(maliciousPath);

        result.Should().StartWith("'").And.EndWith("'");

        // Applying Vim's own single-quote decoding rule (only '' -> ' is special)
        // to the body must reproduce the exact original path.
        var body = result[1..^1];
        var decoded = body.Replace("''", "'");
        decoded.Should().Be(maliciousPath);
    }
}
