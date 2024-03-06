namespace BlogHelper9000.Commands;

public record AddOptions(
    string Title, 
    string[] Tags, 
    string? FeaturedImage, 
    bool IsDraft, 
    bool IsFeatured, 
    bool IsHidden);