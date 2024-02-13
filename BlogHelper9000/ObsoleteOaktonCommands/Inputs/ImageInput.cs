namespace BlogHelper9000.Commands.Inputs;

public class ImageInput : BaseInput
{
    public string Post { get; set; } = string.Empty;

    [FlagAlias("image-query", 'q')] 
    public string ImageQueryFlag { get; set; } = "tech";

    [FlagAlias("logo", true)]
    public string AuthorBrandingFlag { get; set; } = string.Empty;
    
    [FlagAlias("apply-to-drafts", 'd')]
    public bool ApplyToDraftsFlag { get; set; }
    
    [FlagAlias("apply-to-posts", true)]
    public bool ApplyToPostsFlag { get; set; }
    
    [FlagAlias("apply-all", true)]
    public bool ApplyAllFlag { get; set; }
}