using BlogHelper9000.Commands.Inputs;

namespace BlogHelper9000.Commands;

public abstract class BaseCommand<TInput> : OaktonCommand<TInput>
    where TInput : BaseInput
{
    private BaseHelper<TInput> _baseHelper = null!;
    
    protected string DraftsPath => _baseHelper.DraftsPath;

    protected string PostsPath => _baseHelper.PostsPath;

    public override bool Execute(TInput input)
    {
        _baseHelper = BaseHelper<TInput>.Initialise(input);
        return ValidateInput(input) && Run(input);
    }

    protected abstract bool Run(TInput input);

    protected virtual bool ValidateInput(TInput input)
    {
        return _baseHelper.ValidateInput(input);
    }
}