using System.CommandLine.Binding;
using Microsoft.Extensions.Logging;

namespace BlogHelper9000.Commands.Binders;

public class LoggingBinder : BinderBase<ILogger>
    {
        protected override ILogger GetBoundValue(
            BindingContext bindingContext) => GetLogger(bindingContext);

        ILogger GetLogger(BindingContext bindingContext)
        {
            using ILoggerFactory loggerFactory = LoggerFactory.Create(
                builder => builder.AddConsole());
            ILogger logger = loggerFactory.CreateLogger("BlogHelper9000");
            return logger;
        }
    }