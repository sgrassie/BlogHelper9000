using BlogHelper9000.Commands;

var rootCommand = new BlogHelperRootCommand();
return await rootCommand.InvokeAsync(args);