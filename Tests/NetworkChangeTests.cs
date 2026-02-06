#nullable enable
using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using Ubicomp.Utils.NET.Sockets;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    public class NetworkChangeTests
    {
        [Fact]
        public void OnNetworkAddressChanged_ShouldNotThrow_And_MaintainJoinedAddresses()
        {
            // Arrange
            var options = MulticastSocketOptions.LocalNetwork("239.0.0.1", 5000);
            // We set LocalIP to null (default) to enable auto-join behavior
            options.LocalIP = null;

            using var socket = new MulticastSocketBuilder()
                .WithOptions(options)
                .Build();

            // Act
            // Invoke OnNetworkAddressChanged via reflection
            var methodInfo = typeof(MulticastSocket).GetMethod("OnNetworkAddressChanged", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(methodInfo);

            // Simulate the event firing multiple times
            for (int i = 0; i < 3; i++)
            {
                methodInfo.Invoke(socket, new object[] { this, EventArgs.Empty });
            }

            // Assert
            // JoinedAddresses should not be empty (assuming there's at least one interface)
            var joined = socket.JoinedAddresses.ToList();
            Assert.NotEmpty(joined);

            // Check for duplicates
            var distinctCount = joined.Distinct().Count();
            Assert.Equal(joined.Count, distinctCount);
        }

        [Fact]
        public void JoinAllInterfaces_ShouldBeThreadSafe()
        {
            // Arrange
            var options = MulticastSocketOptions.LocalNetwork("239.0.0.1", 5001);
            options.LocalIP = null;

            using var socket = new MulticastSocketBuilder()
                .WithOptions(options)
                .Build();

            var methodInfo = typeof(MulticastSocket).GetMethod("OnNetworkAddressChanged", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(methodInfo);

            // Act
            // Run multiple threads invoking the change
            var threads = new Thread[5];
            Exception? threadException = null;

            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(() =>
                {
                    try
                    {
                        for (int j = 0; j < 10; j++)
                        {
                            methodInfo.Invoke(socket, new object[] { this, EventArgs.Empty });
                            // Also try to read JoinedAddresses
                            var count = socket.JoinedAddresses.Count();
                        }
                    }
                    catch (Exception ex)
                    {
                        threadException = ex;
                    }
                });
                threads[i].Start();
            }

            foreach (var t in threads)
                t.Join();

            Assert.Null(threadException);

            var joined = socket.JoinedAddresses.ToList();
            Assert.Equal(joined.Count, joined.Distinct().Count());
        }
    }
}
