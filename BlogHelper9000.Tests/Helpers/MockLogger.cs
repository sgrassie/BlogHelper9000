using Microsoft.Extensions.Logging;

namespace BlogHelper9000.Tests.Helpers;

//See: https://github.com/nsubstitute/NSubstitute/issues/597
//Specifically: https://github.com/nsubstitute/NSubstitute/issues/597#issuecomment-1081422618
public abstract class MockLogger<T> : ILogger<T>
{
    void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var unboxed = (IReadOnlyList<KeyValuePair<string, object>>)state!;
        string message = formatter(state, exception);

        this.Log();
        this.Log(logLevel, message, exception);
        this.Log(logLevel, unboxed.ToDictionary(k => k.Key, v => v.Value), exception);
    }

    public abstract void Log();

    public abstract void Log(LogLevel logLevel, string message, Exception? exception = null);

    public abstract void Log(LogLevel logLevel, IDictionary<string, object> state, Exception? exception = null);

    public virtual bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

#pragma warning disable CS8633 // Nullability in constraints for type parameter doesn't match the constraints for type parameter in implicitly implemented interface method'.
    public abstract IDisposable BeginScope<TState>(TState state);
#pragma warning restore CS8633 // Nullability in constraints for type parameter doesn't match the constraints for type parameter in implicitly implemented interface method'.
}