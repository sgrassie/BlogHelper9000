using System.Reflection;

var executor = CommandExecutor.For(_ =>{
    _.RegisterCommands(typeof(Program).GetTypeInfo().Assembly);
});

var result = executor.Execute(args);