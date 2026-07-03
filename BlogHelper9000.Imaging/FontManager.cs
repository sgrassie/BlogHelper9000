using System.Reflection;
using Microsoft.Extensions.Logging;
using SixLabors.Fonts;

namespace BlogHelper9000.Imaging;

public class FontManager
{
    private readonly ILogger _logger;
    private static readonly Assembly Assembly = typeof(FontManager).Assembly;
    private static readonly Lazy<FontCollection> LazyFontCollection = new(LoadFonts, LazyThreadSafetyMode.ExecutionAndPublication);

    public FontManager(ILogger logger)
    {
        _logger = logger;
    }

    private static FontCollection LoadFonts()
    {
        var collection = new FontCollection();
        foreach (var ttf in Assembly.GetManifestResourceNames().Where(x => x.EndsWith(".ttf")))
        {
            using var stream = Assembly.GetManifestResourceStream(ttf);
            if (stream != null) collection.Add(stream);
        }
        return collection;
    }

    public Font GetFont(string fontName, int fontSize = 125, FontStyle style = FontStyle.Bold)
    {
        if (LazyFontCollection.Value.TryGet(fontName, out var family))
        {
            _logger.LogDebug("Loading {FontName} with size {FontSize}", fontName, fontSize);

            return family.CreateFont(fontSize, style);
        }

        throw new ArgumentException($"Could not create {fontName} font.", nameof(fontName));
    }
}
