using BlogHelper9000.Commands.Inputs;

namespace BlogHelper9000.Commands;

public class PublishCommand : AsyncBaseCommand<PublishInput>
{
    public PublishCommand()
    {
        Usage("Publish a post!").Arguments(x => x.Post);
    }

    protected override Task<bool> Run(PublishInput input)
    {
        throw new NotImplementedException();
    }
}