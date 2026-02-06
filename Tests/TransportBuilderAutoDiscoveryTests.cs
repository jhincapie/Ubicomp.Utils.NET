#nullable enable
using System;
using System.Reflection;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;
using Xunit;

namespace Ubicomp.Utils.NET.Generators.AutoDiscovery
{
    // Mock the generated class
    public static class TransportExtensions
    {
        public static bool WasCalled = false;
        public static TransportComponent? Component = null;

        public static void RegisterDiscoveredMessages(this TransportComponent component)
        {
            WasCalled = true;
            Component = component;
        }

        public static void Reset()
        {
            WasCalled = false;
            Component = null;
        }
    }
}

namespace Ubicomp.Utils.NET.Tests
{
    [Collection("TransportBuilderTests")]
    public class TransportBuilderAutoDiscoveryTests
    {
        [Fact]
        public void Build_ShouldCallRegisterDiscoveredMessages()
        {
            // Arrange
            Ubicomp.Utils.NET.Generators.AutoDiscovery.TransportExtensions.Reset();
            var options = MulticastSocketOptions.LocalNetwork();
            var builder = new TransportBuilder()
                .WithMulticastOptions(options);

            // Act
            var component = builder.Build();

            // Assert
            Assert.True(Ubicomp.Utils.NET.Generators.AutoDiscovery.TransportExtensions.WasCalled, "RegisterDiscoveredMessages should be called during Build()");
            Assert.Same(component, Ubicomp.Utils.NET.Generators.AutoDiscovery.TransportExtensions.Component);
        }
    }
}
