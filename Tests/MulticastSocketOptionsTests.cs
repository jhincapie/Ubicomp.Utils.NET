using System;
using System.Net;
using Ubicomp.Utils.NET.Sockets;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    public class MulticastSocketOptionsTests
    {
        [Fact]
        public void MulticastSocketOptions_ShouldHaveSensibleDefaults()
        {
            var options = MulticastSocketOptions.LocalNetwork("239.0.0.1", 12345);

            Assert.Equal("239.0.0.1", options.GroupAddress);
            Assert.Equal(12345, options.Port);
            Assert.Equal(1, options.TimeToLive);
            Assert.Null(options.LocalIP);

            // Advanced options
            Assert.True(options.ReuseAddress);
            Assert.True(options.MulticastLoopback);
            Assert.True(options.NoDelay);
            Assert.False(options.DontFragment);

            // Buffers (0 usually means use OS default or unset)
            Assert.Equal(0, options.ReceiveBufferSize);
            Assert.Equal(0, options.SendBufferSize);

            // Filtering
            Assert.Null(options.InterfaceFilter);

            // Lifecycle
        }


        [Theory]
        [InlineData("", 5000, 1)] // Empty IP
        [InlineData("127.0.0.1", 5000, 1)] // Not multicast
        [InlineData("239.0.0.1", 0, 1)] // Invalid port
        [InlineData("239.0.0.1", 5000, -1)] // Invalid TTL
        public void Validate_ShouldThrow_OnInvalidOptions(string ip, int port,
                                                          int ttl)
        {
            // Factory methods now validate internally.
            Assert.Throws<ArgumentException>(() =>
            {
                var options = MulticastSocketOptions.LocalNetwork(ip, port);
                options.TimeToLive = ttl;
                options.Validate();
            });
        }

        [Fact]
        public void FactoryMethods_ShouldApplySensibleDefaults()
        {
            var local = MulticastSocketOptions.LocalNetwork("239.0.0.1", 5001);
            Assert.Equal("239.0.0.1", local.GroupAddress);
            Assert.Equal(5001, local.Port);
            Assert.Equal(1, local.TimeToLive);

            var wan = MulticastSocketOptions.WideAreaNetwork("239.1.1.1", 5002, 32);
            Assert.Equal("239.1.1.1", wan.GroupAddress);
            Assert.Equal(5002, wan.Port);
            Assert.Equal(32, wan.TimeToLive);
        }
    }
}
