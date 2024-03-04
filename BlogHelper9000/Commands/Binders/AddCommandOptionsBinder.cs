using System.CommandLine.Binding;

namespace BlogHelper9000.Commands.Binders;

internal class AddCommandOptionsBinder(
    Argument<string> titleArgument,
    Argument<string[]> tagsArgument,
    Option<bool> draftOption,
    Option<string> featuredImageOption,
    Option<bool> isFeatured,
    Option<bool> isHidden)
    : BinderBase<AddOptions>
{
    protected override AddOptions GetBoundValue(BindingContext bindingContext)
    {
        return new AddOptions(
            bindingContext.ParseResult.GetValueForArgument(titleArgument),
            bindingContext.ParseResult.GetValueForArgument(tagsArgument),
            bindingContext.ParseResult.GetValueForOption(featuredImageOption),
            bindingContext.ParseResult.GetValueForOption(draftOption),
            bindingContext.ParseResult.GetValueForOption(isFeatured),
            bindingContext.ParseResult.GetValueForOption(isHidden));
    }
    
}