using System;
using Xunit;
using Ubicomp.Utils.NET.MulticastTransportFramework;

namespace Ubicomp.Utils.NET.Tests
{
    public class TransportFrameworkTests
    {
        public class MockContent : ITransportMessageContent
        {
            public string Content { get; set; }
        }

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
