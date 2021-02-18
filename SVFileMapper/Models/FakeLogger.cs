using System;
using Microsoft.Extensions.Logging;

namespace SVFileMapper.Models
{
    internal class FakeLogger : ILogger, IDisposable
    {
        public void Dispose()
        {
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter)
        {
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return new FakeLogger();
        }
    }
}