using System;
using Microsoft.Extensions.Logging;

namespace SVFileMapper.Models
{
    internal class FakeLogger : ILogger, IDisposable
    {
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter)
        {
        }

        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable BeginScope<TState>(TState state) => new FakeLogger();

        public void Dispose()
        {
        }
    }
}