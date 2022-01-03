namespace BlogHelper9000.Commands;

public abstract class AsyncBaseCommand<TInput> : OaktonAsyncCommand<TInput> where TInput : BaseInput
{
    private BaseHelper<TInput> _baseHelper;

    protected string? DraftsPath => _baseHelper.DraftsPath;

    protected string? PostsPath => _baseHelper.PostsPath;
    
    public override Task<bool> Execute(TInput input)
    {
        _baseHelper = BaseHelper<TInput>.Initialise(input);
        return !ValidateInput(input) ? Task.FromResult(false) : Run(input);
    }
    
    protected abstract Task<bool> Run(TInput input);
    
    protected virtual bool ValidateInput(TInput input)
    {
        return _baseHelper.ValidateInput(input);
    }
}