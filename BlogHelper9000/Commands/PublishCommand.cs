namespace BlogHelper9000.Commands;

public class PublishInput
{
    public string Post { get; set; }
}
public class PublishCommand : OaktonCommand<PublishInput>
{
    public override bool Execute(PublishInput input)
    {
        throw new System.NotImplementedException();
    }
}