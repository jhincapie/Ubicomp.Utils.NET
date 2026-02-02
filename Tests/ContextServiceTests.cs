#nullable enable
using System;
using System.Threading;
using Ubicomp.Utils.NET.ContextAwarenessFramework.ContextService;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    public class ContextServiceTests
    {
        private class MockService : ContextService
        {
            public bool WasStopped
            {
                get; private set;
            }
            protected override void CustomStop()
            {
                WasStopped = true;
            }
        }

        [Fact]
        public void StopServices_ShouldStopAllRegisteredServices()
        {
            // Arrange
            var service = new MockService();
            ContextServiceContainer.AddContextService(service);
            ContextServiceContainer.StartServices();

            // Act
            ContextServiceContainer.StopServices();

            // Assert
            Assert.True(service.WasStopped);
        }
    }
}
