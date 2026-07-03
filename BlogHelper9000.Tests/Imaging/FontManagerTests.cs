using BlogHelper9000.Imaging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlogHelper9000.Tests.Imaging;

public class FontManagerTests
{
    [Fact]
    public void Repeated_Construction_Should_Not_Throw_Or_Duplicate_Fonts()
    {
        var first = new FontManager(NullLogger.Instance);
        var second = new FontManager(NullLogger.Instance);

        var act = () =>
        {
            first.GetFont("Ubuntu", 90);
            second.GetFont("Ubuntu", 90);
        };

        act.Should().NotThrow();
    }
}
