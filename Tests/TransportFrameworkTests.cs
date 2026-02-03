#nullable enable
using System;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    /// <summary>
    /// Unit tests for the basic components of the transport framework.
    /// </summary>
    public class TransportFrameworkTests
    {
        /// <summary>Mock content for transport framework tests.</summary>
        public class MockContent
        {
            /// <summary>Gets or sets the content text.</summary>
            public string Content { get; set; } = string.Empty;
        }

        /// <summary>
        /// Validates that an EventSource initializes its properties correctly.
        /// </summary>
        [Fact]
        public void EventSource_ShouldInitializeCorrectly()
        {
            Guid id = Guid.NewGuid();
            string name = "TestHost";
            string description = "TestDescription";

            var source = new EventSource(id, name, description);

            Assert.Equal(id, source.ResourceId);
            Assert.Equal(name, source.ResourceName);
            Assert.Equal(description, source.FriendlyName);
        }

        /// <summary>
        /// Validates that a TransportMessage initializes its properties
        /// correctly.
        /// </summary>
        [Fact]
        public void TransportMessage_ShouldInitializeCorrectly()
        {
            var source = new EventSource(Guid.NewGuid(), "Host", "Desc");
            int typeId = 123;
            var content = new MockContent { Content = "TestData" };

            var message = new TransportMessage(source, typeId, content);

            Assert.Equal(source, message.MessageSource);
            Assert.Equal(typeId, message.MessageType);
            Assert.Equal(content, message.MessageData);
            Assert.NotNull(message.TimeStamp);
        }
    }
}
