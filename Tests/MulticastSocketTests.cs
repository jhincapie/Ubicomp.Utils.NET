using System;
using Xunit;
using Ubicomp.Utils.NET.Sockets;
using System.Net;

namespace Ubicomp.Utils.NET.Tests
{
    public class MulticastSocketTests
    {
        [Fact]
        public void Constructor_ShouldInitializeCorrectly()
        {
            string groupAddress = "239.0.0.1";
            int port = 5000;
            int ttl = 1;

            var socket = new MulticastSocket(groupAddress, port, ttl);

            Assert.NotNull(socket);
        }

        [Fact]
        public void StartReceiving_ShouldThrowIfNoListener()
        {
            var socket = new MulticastSocket("239.0.0.2", 5001, 1);
            
            Assert.Throws<ApplicationException>(() => socket.StartReceiving());
        }
    }
}
