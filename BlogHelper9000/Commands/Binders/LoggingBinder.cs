using System.CommandLine.Binding;

namespace BlogHelper9000.Commands.Binders;

public class LoggingBinder : BinderBase<ILogger>
    {
        protected override ILogger GetBoundValue(
            BindingContext bindingContext) => GetLogger(bindingContext);

        ILogger GetLogger(BindingContext bindingContext)
        {
            var logLevel = bindingContext.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption);
            
            using var loggerFactory = LoggerFactory.Create(
                builder =>
                {
                    builder.AddFilter("Microsoft", LogLevel.Warning);
                    builder.AddFilter("System", LogLevel.Warning);
                    builder.AddFilter("BlogHelper9000", logLevel);
                    builder.AddConsole();
                });
            var logger = loggerFactory.CreateLogger("BlogHelper9000");
            logger.LogTrace("Logger initialised");
            return logger;
        }
    }