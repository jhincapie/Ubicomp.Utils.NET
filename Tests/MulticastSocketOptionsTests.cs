using Xunit;
using System;
using System.Net;
using Ubicomp.Utils.NET.Sockets;

namespace Ubicomp.Utils.NET.Tests
{
    public class MulticastSocketOptionsTests
    {
        [Fact]
        public void MulticastSocketOptions_ShouldHaveSensibleDefaults()
        {
            var options = new MulticastSocketOptions("239.0.0.1", 12345);

            Assert.Equal("239.0.0.1", options.TargetIP);
            Assert.Equal(12345, options.TargetPort);
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
            
                        Assert.True(options.AutoJoin);
            
                    }
            
            
            
                    [Theory]
            
                    [InlineData("", 5000, 1)]         // Empty IP
            
                    [InlineData("127.0.0.1", 5000, 1)] // Not multicast
            
                    [InlineData("239.0.0.1", 0, 1)]    // Invalid port
            
                    [InlineData("239.0.0.1", 5000, -1)] // Invalid TTL
            
                    public void Validate_ShouldThrow_OnInvalidOptions(string ip, int port, int ttl)
            
                    {
            
                        var options = new MulticastSocketOptions(ip, port) { TimeToLive = ttl };
            
                        Assert.Throws<ArgumentException>(() => options.Validate());
            
                    }
            
                }
            
            }
            
            