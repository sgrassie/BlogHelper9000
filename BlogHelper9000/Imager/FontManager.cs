using System.Reflection;
using SixLabors.Fonts;

namespace BlogHelper9000.Imager;

public class FontManager
{
    private readonly ILogger _logger;
    private static readonly Assembly Assembly = typeof(FontManager).Assembly;
    private static readonly FontCollection FontCollection = new();

        public FontManager(ILogger logger)
        {
            _logger = logger;
            _logger.LogInformation("Loading fonts");

            foreach (var ttf in Assembly.GetManifestResourceNames().Where(x => x.EndsWith(".ttf")))
            {
                using var stream = Assembly.GetManifestResourceStream(ttf);
                if (stream != null) FontCollection.Add(stream);
            }
        }
        
        public Font GetFont(string fontName, int fontSize = 125, FontStyle style = FontStyle.Bold)
        {
            if (FontCollection.TryGet(fontName, out var family))
            {
                _logger.LogDebug("Loading {FontName} with size {FontSize}", fontName, fontSize);
                
                return family.CreateFont(fontSize, style);
            }

            throw new ArgumentException($"Could not create {fontName} font.", nameof(fontName));
        }
}
