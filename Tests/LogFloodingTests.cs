using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    public class LogFloodingTests
    {
        public class TestLogger : ILogger, IDisposable
        {
            public List<string> Logs { get; } = new List<string>();

            public IDisposable BeginScope<TState>(TState state) => this;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                Logs.Add(formatter(state, exception));
            }

            public void Dispose()
            {
            }
        }

        [MessageType("test.logflood")]
        public class LogFloodMessage
        {
            public string Data { get; set; }
        }

        [Fact]
        public void TestReplayCacheFull_LogFlooding()
        {
            // Arrange
            var options = MulticastSocketOptions.LocalNetwork();
            var transport = new TransportComponent(options);
            var logger = new TestLogger();
            transport.Logger = logger;
            transport.EnforceOrdering = false;
            transport.MaxReplayCacheSize = 5; // Very small limit

            var source = new EventSource(Guid.NewGuid(), "TestSender");
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            var processMethod = typeof(TransportComponent).GetMethod("ProcessSingleMessage", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(processMethod);

            // Act - Fill cache and flood
            // Send 100 unique messages. First 5 fill cache. Next 95 are dropped and SHOULD NOT all be logged.
            // Currently they ARE logged.
            for (int i = 0; i < 100; i++)
            {
                var payload = new LogFloodMessage { Data = $"Message {i}" };
                var tMsg = new TransportMessage(source, "test.logflood", payload)
                {
                    MessageId = Guid.NewGuid(), // Unique ID
                    TimeStamp = DateTime.UtcNow.ToString(TransportMessage.DATE_FORMAT_NOW)
                };

                var json = JsonSerializer.Serialize(tMsg, jsonOptions);
                var bytes = Encoding.UTF8.GetBytes(json);
                var socketMsg = new SocketMessage(bytes, i + 1);

                processMethod.Invoke(transport, new object[] { socketMsg });
            }

            // Assert
            int logCount = logger.Logs.Count(l => l.Contains("Replay protection cache full"));
            // Before fix: logCount should be >= 95 (approx).
            // After fix: logCount should be small (e.g. 1).

            // Assert that flooding is suppressed
            Assert.True(logCount < 5, $"Expected suppressed logs, but found {logCount} logs.");
            Assert.True(logCount > 0, "Expected at least one log.");
        }

        [Fact]
        public void TestDuplicateMessage_LogFlooding()
        {
             // Arrange
            var options = MulticastSocketOptions.LocalNetwork();
            var transport = new TransportComponent(options);
            var logger = new TestLogger();
            transport.Logger = logger;
            transport.EnforceOrdering = false;
            transport.MaxReplayCacheSize = 1000;

            var source = new EventSource(Guid.NewGuid(), "TestSender");
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            var processMethod = typeof(TransportComponent).GetMethod("ProcessSingleMessage", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(processMethod);

            var payload = new LogFloodMessage { Data = "Duplicate" };
            var originalMsgId = Guid.NewGuid();
            var tMsg = new TransportMessage(source, "test.logflood", payload)
            {
                MessageId = originalMsgId,
                TimeStamp = DateTime.UtcNow.ToString(TransportMessage.DATE_FORMAT_NOW)
            };
            var json = JsonSerializer.Serialize(tMsg, jsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);

            // Act - Send 100 duplicate messages
            for (int i = 0; i < 100; i++)
            {
                var socketMsg = new SocketMessage(bytes, i + 1);
                processMethod.Invoke(transport, new object[] { socketMsg });
            }

            // Assert
            int logCount = logger.Logs.Count(l => l.Contains("Duplicate message detected"));
            // Before fix: logCount should be 99 (first one is processed, subsequent are dupes).

            Assert.True(logCount < 5, $"Expected suppressed logs for duplicates, but found {logCount} logs.");
            Assert.True(logCount > 0, "Expected at least one log.");
        }
    }
}
