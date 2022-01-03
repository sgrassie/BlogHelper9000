using System.Reflection;

var executor = CommandExecutor.For(_ =>{
    _.RegisterCommands(typeof(Program).GetTypeInfo().Assembly);
});

var result = await executor.ExecuteAsync(args);
return result;