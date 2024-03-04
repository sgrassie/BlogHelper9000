using System.CommandLine.Binding;

namespace BlogHelper9000.Commands.Binders;

public class BaseDirectoryBinder : BinderBase<string>
{
    protected override string GetBoundValue(BindingContext bindingContext) => GetBaseDirectory(bindingContext);

    private string GetBaseDirectory(BindingContext bindingContext)
    {
        var baseDirectory = bindingContext.ParseResult.GetValueForOption(GlobalOptions.BaseDirectoryOption);

        if (string.IsNullOrEmpty(baseDirectory))
        {
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        return baseDirectory;
    }
}