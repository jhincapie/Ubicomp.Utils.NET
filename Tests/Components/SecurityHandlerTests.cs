using Microsoft.Extensions.Logging.Abstractions;
using Ubicomp.Utils.NET.MulticastTransportFramework.Components;
using Xunit;

namespace Ubicomp.Utils.NET.Tests.Components
{
    public class SecurityHandlerTests
    {
        [Fact]
        public void SecurityKey_UpdatesKeyManager()
        {
            var handler = new SecurityHandler(NullLogger.Instance);
            handler.SecurityKey = "password123";

            Assert.NotNull(handler.CurrentSession);
            Assert.NotNull(handler.CurrentSession.AesGcmInstance);
        }
    }
}
