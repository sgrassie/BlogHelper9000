namespace BlogHelper9000.Commands.Inputs;

public class ImageInput : BaseInput
{
    public string Post { get; set; } = string.Empty;

    public string AuthorBrandingFlag { get; set; } = string.Empty;
    
    [FlagAlias("apply-to-drafts", true)]
    public bool ApplyToDrafts { get; set; }
    
    [FlagAlias("apply-to-posts", true)]
    public bool ApplyToPosts { get; set; }
    
    [FlagAlias("apply-all", true)]
    public bool ApplyAllFlag { get; set; }
}