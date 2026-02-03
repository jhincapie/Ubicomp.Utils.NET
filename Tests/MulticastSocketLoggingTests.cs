#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Ubicomp.Utils.NET.Sockets;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    public class MulticastSocketLoggingTests
    {
        private class TestLogger : ILogger
        {
            public List<string> LoggedMessages { get; } = new List<string>();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                LoggedMessages.Add(formatter(state, exception));
            }
        }

        [Fact]
        public void Build_WithLogger_ShouldLogInitialization()
        {
            // Arrange
            var logger = new TestLogger();
            var options = MulticastSocketOptions.LocalNetwork("239.0.0.60", 5200);
            
            // Act
            using var socket = new MulticastSocketBuilder()
                .WithOptions(options)
                .WithLogger(logger)
                .Build();

            // Assert
            Assert.Contains(logger.LoggedMessages, m => m.Contains("Setting up MulticastSocket on port 5200"));
            Assert.Contains(logger.LoggedMessages, m => m.Contains("Socket bound to"));
            Assert.Contains(logger.LoggedMessages, m => m.Contains("Joined multicast group on"));
        }

        [Fact]
        public void StartReceiving_ShouldLogStart()
        {
            // Arrange
            var logger = new TestLogger();
            var options = MulticastSocketOptions.LocalNetwork("239.0.0.61", 5201);
            using var socket = new MulticastSocketBuilder()
                .WithOptions(options)
                .WithLogger(logger)
                .Build();

            // Act
            socket.StartReceiving();

            // Assert
            Assert.Contains(logger.LoggedMessages, m => m.Contains("Starting receive loop"));
        }
    }
}
