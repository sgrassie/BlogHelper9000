using System.CommandLine.Binding;

namespace BlogHelper9000.Commands.Binders;

public class FileSystemBinder : BinderBase<IFileSystem>
{
    protected override IFileSystem GetBoundValue(BindingContext bindingContext) => GetFileSystem();

    private static IFileSystem GetFileSystem()
    {
        return new FileSystem();
    }
}