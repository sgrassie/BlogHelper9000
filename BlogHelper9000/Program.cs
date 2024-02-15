using BlogHelper9000.Commands;

var rootCommand = new BlogHelperRootCommand(new FileSystem());

return await rootCommand.InvokeAsync(args);