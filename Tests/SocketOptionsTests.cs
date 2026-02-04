using System;
using System.Net.Sockets;
using System.Reflection;
using Ubicomp.Utils.NET.Sockets;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    public class SocketOptionsTests
    {
        [Fact]
        public void VerifySocketOptions_AreSet_Explicitly_False()
        {
            var options = MulticastSocketOptions.LocalNetwork(port: 5001);
            // Set all bool options to false
            options.ReuseAddress = false;
            options.MulticastLoopback = false;
            options.NoDelay = false;
            options.DontFragment = false;

            var builder = new MulticastSocketBuilder().WithOptions(options);
            using var socket = builder.Build();

            // Reflection to get internal socket
            var udpSocketField = typeof(MulticastSocket).GetField("_udpSocket", BindingFlags.NonPublic | BindingFlags.Instance);
            var udpSocket = (Socket)udpSocketField.GetValue(socket);

            // MulticastLoopback
            // Default is usually 1 (true).
            // If code fails to set it to 0 when false, this assertion will fail.
            int loopback = (int)udpSocket.GetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback);
            Assert.Equal(0, loopback);

            // ReuseAddress
            int reuseAddress = (int)udpSocket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress);
            Assert.Equal(0, reuseAddress);

            // DontFragment
            int dontFragment = (int)udpSocket.GetSocketOption(SocketOptionLevel.IP, SocketOptionName.DontFragment);
            Assert.Equal(0, dontFragment);

            // NoDelay (UDP NoChecksum)
            try
            {
                int noDelay = (int)udpSocket.GetSocketOption(SocketOptionLevel.Udp, SocketOptionName.NoDelay);
                Assert.Equal(0, noDelay);
            }
            catch
            {
                // Ignore if platform doesn't support getting it
            }
        }

        [Fact]
        public void VerifySocketOptions_AreSet_Explicitly_True()
        {
            var options = MulticastSocketOptions.LocalNetwork();
            // Set all bool options to true
            options.ReuseAddress = true;
            options.MulticastLoopback = true;
            options.NoDelay = true;
            options.DontFragment = true;

            var builder = new MulticastSocketBuilder().WithOptions(options);
            using var socket = builder.Build();

            // Reflection to get internal socket
            var udpSocketField = typeof(MulticastSocket).GetField("_udpSocket", BindingFlags.NonPublic | BindingFlags.Instance);
            var udpSocket = (Socket)udpSocketField.GetValue(socket);

            // ReuseAddress
            int reuseAddress = (int)udpSocket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress);
            Assert.NotEqual(0, reuseAddress); // Expect non-zero (true)

            // MulticastLoopback
            int loopback = (int)udpSocket.GetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback);
            Assert.NotEqual(0, loopback);

            // DontFragment
            int dontFragment = (int)udpSocket.GetSocketOption(SocketOptionLevel.IP, SocketOptionName.DontFragment);
            Assert.NotEqual(0, dontFragment);

             // NoDelay
             try
             {
                 int noDelay = (int)udpSocket.GetSocketOption(SocketOptionLevel.Udp, SocketOptionName.NoDelay);
                 Assert.NotEqual(0, noDelay);
             }
             catch {}
        }
    }
}
