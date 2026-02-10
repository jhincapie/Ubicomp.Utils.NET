using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    public class SecurityTests
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

        [Fact]
        public void TestLogInjectionVulnerability_Sanitized()
        {
            // Arrange
            var options = MulticastSocketOptions.LocalNetwork();
            var transport = new TransportComponent(options);
            var logger = new TestLogger();
            transport.Logger = logger;
            transport.EnforceOrdering = false; // Direct processing

            // Register dummy Ack handler to ensure it processes the Ack message logic where the log is
            // Actually, Ack processing is built-in in ProcessTransportMessage

            // We need an active session to trigger the log "Received Ack for message..."
            // So we need to fake an active session.
            // _activeSessions is private.
            // But we can just use reflection to add one, OR
            // transport.SendAsync with RequestAck creates a session.
            // But SendAsync requires a socket to send.

            // Let's rely on the fact that Ack processing happens if "tMessage.MessageType == AckMessageType"
            // and "tMessage.MessageData is AckMessageContent".
            // The log happens inside: if (_activeSessions.TryGetValue(...))

            // So we MUST have an active session.
            // We can use reflection to inject a session into _activeSessions.

            var originalMsgId = Guid.NewGuid();
            var session = new AckSession(originalMsgId);

            var activeSessionsField = typeof(TransportComponent).GetField("_activeSessions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var activeSessions = (System.Collections.Concurrent.ConcurrentDictionary<Guid, AckSession>)activeSessionsField.GetValue(transport);
            activeSessions.TryAdd(originalMsgId, session);

            // Construct malicious ACK message
            var maliciousSource = "Attacker\n[CRITICAL] System Failure";
            var ackContent = new AckMessageContent { OriginalMessageId = originalMsgId };

            var tMsg = new TransportMessage(
                new EventSource(Guid.NewGuid(), maliciousSource),
                TransportComponent.AckMessageType,
                ackContent
            );

            // Serialize
            var json = JsonSerializer.Serialize(tMsg, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true });
            var bytes = Encoding.UTF8.GetBytes(json);

            var socketMsg = new SocketMessage(bytes, 1);

            // Act
            // transport.HandleSocketMessage(socketMsg);
            // HandleSocketMessage writes to channel, requiring Start().
            // Instead, invoke private ProcessSingleMessage directly to avoid socket setup/async issues.
            var processMethod = typeof(TransportComponent).GetMethod("ProcessSingleMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            processMethod.Invoke(transport, new object[] { socketMsg });

            // Assert
            // Find the log entry
            bool foundLog = false;
            foreach (var log in logger.Logs)
            {
                if (log.Contains("Received Ack for message"))
                {
                    foundLog = true;
                    // Check if it contains the NEWLINE
                    Assert.DoesNotContain("\n", log);
                    // Check if it contains the sanitized version (we expect it to be sanitized eventually)
                    // For now, before fix, this assertion would fail if I asserted DoesNotContain("\n")
                }
            }

            Assert.True(foundLog, "Did not find the expected log message.");
        }

        [Fact]
        public void TestLogInjectionVulnerability_Sanitized_ControlChars()
        {
            // Arrange
            var options = MulticastSocketOptions.LocalNetwork();
            var transport = new TransportComponent(options);
            var logger = new TestLogger();
            transport.Logger = logger;
            transport.EnforceOrdering = false;

            var originalMsgId = Guid.NewGuid();
            var session = new AckSession(originalMsgId);

            var activeSessionsField = typeof(TransportComponent).GetField("_activeSessions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var activeSessions = (System.Collections.Concurrent.ConcurrentDictionary<Guid, AckSession>)activeSessionsField.GetValue(transport);
            activeSessions.TryAdd(originalMsgId, session);

            // Construct malicious ACK message with BACKSPACE
            var maliciousSource = "Normal\b\b\b\b\b\bEvil"; // Backspaces might hide "Normal"
            var ackContent = new AckMessageContent { OriginalMessageId = originalMsgId };

            var tMsg = new TransportMessage(
                new EventSource(Guid.NewGuid(), maliciousSource),
                TransportComponent.AckMessageType,
                ackContent
            );

            var json = JsonSerializer.Serialize(tMsg, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true });
            var bytes = Encoding.UTF8.GetBytes(json);
            var socketMsg = new SocketMessage(bytes, 1);

            var processMethod = typeof(TransportComponent).GetMethod("ProcessSingleMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            processMethod.Invoke(transport, new object[] { socketMsg });

            bool foundLog = false;
            foreach (var log in logger.Logs)
            {
                if (log.Contains("Received Ack for message"))
                {
                    foundLog = true;
                    // Check if it contains BACKSPACE
                    Assert.False(log.Contains("\b"), $"Log contained backspace: '{log}'");
                }
            }
            Assert.True(foundLog, "Did not find the expected log message.");
        }

    }
}
