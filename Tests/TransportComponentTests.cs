using Xunit;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using System.Net;

namespace Ubicomp.Utils.NET.Tests
{
    public class TransportComponentTests
    {
        [Fact]
        public void Singleton_IsNotNull()
        {
            Assert.NotNull(TransportComponent.Instance);
        }

        [Fact]
        public void Properties_CanBeSet()
        {
            var component = TransportComponent.Instance;
            component.MulticastGroupAddress = IPAddress.Parse("224.0.0.1");
            component.Port = 5000;
            component.UDPTTL = 1;

            Assert.Equal(IPAddress.Parse("224.0.0.1"), component.MulticastGroupAddress);
            Assert.Equal(5000, component.Port);
            Assert.Equal(1, component.UDPTTL);
        }
    }
}
