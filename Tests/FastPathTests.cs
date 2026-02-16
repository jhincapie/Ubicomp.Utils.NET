using System;
using System.Buffers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;
using Xunit;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Ubicomp.Utils.NET.Tests
{
    public class FastPathTests
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

        [MessageType("test.fastpath")]
        public class FastPathMessage
        {
            public string Data { get; set; }
        }

        [Fact]
        public void TestFastPath_ValidMessage_Processed()
        {
            var options = MulticastSocketOptions.LocalNetwork();
            var transport = new TransportComponent(options);
            var logger = new TestLogger();
            transport.Logger = logger;

            bool handled = false;
            transport.RegisterHandler<FastPathMessage>((msg, ctx) =>
            {
                handled = true;
            });

            var source = new EventSource(Guid.NewGuid(), "TestSender");
            var payload = new FastPathMessage { Data = "Valid" };
            var tMsg = new TransportMessage(source, "test.fastpath", payload);
            // Ensure valid timestamp
            tMsg.TimeStamp = DateTime.UtcNow.ToString(TransportMessage.DATE_FORMAT_NOW);

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
            var json = JsonSerializer.Serialize(tMsg, jsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);

            var socketMsg = new SocketMessage(bytes, 1);

            var processMethod = typeof(TransportComponent).GetMethod("ProcessSingleMessage", BindingFlags.NonPublic | BindingFlags.Instance);
            processMethod.Invoke(transport, new object[] { socketMsg });

            Assert.True(handled, "Message should be handled");
        }

        [Fact]
        public void TestFastPath_InvalidTimestamp_Dropped()
        {
            var options = MulticastSocketOptions.LocalNetwork();
            var transport = new TransportComponent(options);
            var logger = new TestLogger();
            transport.Logger = logger;

            bool handled = false;
            transport.RegisterHandler<FastPathMessage>((msg, ctx) =>
            {
                handled = true;
            });

            var source = new EventSource(Guid.NewGuid(), "TestSender");
            var payload = new FastPathMessage { Data = "InvalidTimestamp" };
            var tMsg = new TransportMessage(source, "test.fastpath", payload);
            // Set OLD timestamp
            tMsg.TimeStamp = DateTime.UtcNow.AddHours(-1).ToString(TransportMessage.DATE_FORMAT_NOW);

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
            var json = JsonSerializer.Serialize(tMsg, jsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);

            var socketMsg = new SocketMessage(bytes, 1);

            var processMethod = typeof(TransportComponent).GetMethod("ProcessSingleMessage", BindingFlags.NonPublic | BindingFlags.Instance);
            processMethod.Invoke(transport, new object[] { socketMsg });

            Assert.False(handled, "Message with old timestamp should be dropped");
            Assert.Contains(logger.Logs, l => l.Contains("timestamp out of bounds (FastPath)"));
        }

        [Fact]
        public void TestFastPath_MalformedHeader_Dropped()
        {
            var options = MulticastSocketOptions.LocalNetwork();
            var transport = new TransportComponent(options);
            var logger = new TestLogger();
            transport.Logger = logger;

            bool handled = false;
            transport.RegisterHandler<FastPathMessage>((msg, ctx) =>
            {
                handled = true;
            });

            // JSON missing MessageId
            var json = @"{ ""timeStamp"": ""2023-01-01 00:00:00"", ""messageType"": ""test.fastpath"", ""messageData"": { ""data"": ""Bad"" } }";
            var bytes = Encoding.UTF8.GetBytes(json);

            var socketMsg = new SocketMessage(bytes, 1);

            var processMethod = typeof(TransportComponent).GetMethod("ProcessSingleMessage", BindingFlags.NonPublic | BindingFlags.Instance);
            processMethod.Invoke(transport, new object[] { socketMsg });

            Assert.False(handled, "Message without MessageId should be dropped");
            // Expect "Dropped malformed message" or similar from FastPath failure
            Assert.Contains(logger.Logs, l => l.Contains("FastPath failed to read header"));
        }
    }
}
