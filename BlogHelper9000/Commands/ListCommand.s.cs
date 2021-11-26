using System.IO;

namespace BlogHelper9000.Commands;

public class ListInput
{
    public string Filter { get; set; }
}

public class ListCommand : OaktonCommand<ListInput>
{
    private const string _postsFolder = "_posts";
    
    public override bool Execute(ListInput input)
    {
        var files = Directory.EnumerateFiles()
    }
}
