using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    public class ReplayAttackTests
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

        [MessageType("test.replay")]
        public class ReplayTestMessage
        {
            public string Data
            {
                get; set;
            }
        }

        [Fact]
        public void TestReplayAttack_BlocksDuplicateMessages()
        {
            // Arrange
            var options = MulticastSocketOptions.LocalNetwork();
            var transport = new TransportComponent(options);
            var logger = new TestLogger();
            transport.Logger = logger;
            transport.EnforceOrdering = false; // Direct processing

            int handlerCallCount = 0;
            transport.RegisterHandler<ReplayTestMessage>((msg, ctx) =>
            {
                handlerCallCount++;
            });

            var source = new EventSource(Guid.NewGuid(), "TestSender");
            var originalMsgId = Guid.NewGuid();
            var payload = new ReplayTestMessage { Data = "Sensitive Action" };

            var tMsg = new TransportMessage(source, "test.replay", payload)
            {
                MessageId = originalMsgId,
                TimeStamp = DateTime.Now.ToString(TransportMessage.DATE_FORMAT_NOW)
            };

            // Serialize message
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
            var json = JsonSerializer.Serialize(tMsg, jsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);

            // Simulate receiving the message via SocketMessage
            // SequenceId 1
            var socketMsg1 = new SocketMessage(bytes, 1);

            // Use reflection to call private ProcessSingleMessage
            var processMethod = typeof(TransportComponent).GetMethod("ProcessSingleMessage", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(processMethod);

            // Act - First Receipt
            processMethod.Invoke(transport, new object[] { socketMsg1 });

            // Assert - First Receipt
            Assert.Equal(1, handlerCallCount);

            // Act - Replay Attack (Same MessageId, New SequenceId)
            var socketMsg2 = new SocketMessage(bytes, 2);
            processMethod.Invoke(transport, new object[] { socketMsg2 });

            // Assert - Should be blocked (Count stays 1)
            Assert.Equal(1, handlerCallCount);

            // Verify Log Warning
            bool foundWarning = false;
            foreach (var log in logger.Logs)
            {
                if (log.Contains("Duplicate message detected"))
                {
                    foundWarning = true;
                    break;
                }
            }
            Assert.True(foundWarning, "Expected warning about duplicate message not found.");
        }
    }
}
