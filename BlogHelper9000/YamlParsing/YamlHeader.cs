namespace BlogHelper9000.YamlParsing;

public class YamlHeader
{
    public string Layout { get; set; }
    public string Title { get; set; }
    public string? Description { get; set; }
    public List<string> Tags { get; set; }
    
    [YamlName("featured_image")]
    public string? FeaturedImage { get; set; }

    public string? Image { get; set; }
    
    [YamlName("featured_image_thumbnail")]
    public string? FeaturedImageThumbnail { get; set; }
    
    [YamlName("featured")]
    public bool? IsFeatured { get; set; }
    
    [YamlName("hidden")]
    public bool? IsHidden { get; set; }
    
    [YamlName("published")]
    public DateTime? PublishedOn { get; set; }
    
    public bool? IsPublished { get; set; }
    
    public string? Series { get; set; }

    [YamlIgnore]
    public bool IsSeries { get; set; }
    
    [YamlIgnore]
    public Dictionary<string, string> Extras { get; set; }
}