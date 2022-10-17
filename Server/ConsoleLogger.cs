using System.Collections.Concurrent;

namespace Sharenima.Server;

public class ConsoleLoggerConfiguration {
    public LogLevel LogLevel { get; set; } = LogLevel.Information;
    public int EventId { get; set; } = 0;
    public ConsoleColor Colour { get; set; } = ConsoleColor.Green;
}

public class ConsoleLoggerProvider : ILoggerProvider {
    private readonly ConsoleLoggerConfiguration _config;
    private readonly ConcurrentDictionary<string, ConsoleLogger> _loggers = new ConcurrentDictionary<string, ConsoleLogger>();

    public ConsoleLoggerProvider(ConsoleLoggerConfiguration config) {
        _config = config;
    }

    public ILogger CreateLogger(string categoryName) {
        return _loggers.GetOrAdd(categoryName, name => new ConsoleLogger(name, _config));
    }

    public void Dispose() {
        _loggers.Clear();
    }
}

public class ConsoleLogger : ILogger {
    private static readonly object Lock = new Object();
    private readonly string _name;
    private readonly ConsoleLoggerConfiguration _config;

    public ConsoleLogger(string name, ConsoleLoggerConfiguration config) {
        _name = name;
        _config = config;
    }

    public IDisposable BeginScope<TState>(TState state) => default!;

    public bool IsEnabled(LogLevel logLevel) {
        return logLevel == _config.LogLevel;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception exception, Func<TState, Exception, string> formatter) {
        if (!IsEnabled(logLevel)) {
            return;
        }

        lock (Lock) {
            if (_config.EventId == 0 || _config.EventId == eventId.Id) {
                var colour = Console.ForegroundColor;
                Console.ForegroundColor = _config.Colour;
                Console.WriteLine($"{logLevel.ToString()} - {eventId.Id} - {_name} - {formatter(state, exception)}");
                Console.ForegroundColor = colour;
            }
        }
    }
}