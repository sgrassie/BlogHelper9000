using BlogHelper9000.Commands.Inputs;

namespace BlogHelper9000.Commands;

public class ImageCommand : AsyncBaseCommand<ImageInput>
{
    private string _imagesRoot = string.Empty;
    
    public ImageCommand()
    {
        Usage("Default");
        Usage("Update images across all posts").Arguments(a => a.ApplyAllFlag, a => a.is, a => a.BaseDirectoryFlag);
    }
    protected override Task<bool> Run(ImageInput input)
    {
        throw new NotImplementedException();
    }

    private class ImageProcessor
    {
        
    }
}