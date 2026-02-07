using System;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Xunit;

namespace Ubicomp.Utils.NET.Tests
{
    public class ReplayWindowTests
    {
        [Fact]
        public void Test_InOrderArrival()
        {
            var window = new ReplayWindow();
            Assert.True(window.CheckAndMark(1), "Should accept first packet");
            Assert.True(window.CheckAndMark(2), "Should accept next packet");
            Assert.True(window.CheckAndMark(100), "Should accept jump ahead");
        }

        [Fact]
        public void Test_DuplicateArrival()
        {
            var window = new ReplayWindow();
            Assert.True(window.CheckAndMark(10), "Should accept 10");
            Assert.False(window.CheckAndMark(10), "Should reject duplicate 10");
        }

        [Fact]
        public void Test_OutOfOrder_WithinWindow()
        {
            var window = new ReplayWindow();
            Assert.True(window.CheckAndMark(100), "Set high water mark to 100");

            // Window is [100-63, 100] = [37, 100]
            Assert.True(window.CheckAndMark(99), "Should accept 99");
            Assert.True(window.CheckAndMark(50), "Should accept 50");
            Assert.False(window.CheckAndMark(99), "Should reject duplicate 99");
        }

        [Fact]
        public void Test_TooOld()
        {
            var window = new ReplayWindow();
            Assert.True(window.CheckAndMark(100), "Set high water mark to 100");

            // Window is 64. So 100-64 = 36 is the boundary?
            // Highest = 100.
            // Offset = 100 - 36 = 64. If Offset >= 64 return false. So 36 is rejected.
            // 37: Offset = 63. Accepted.

            Assert.False(window.CheckAndMark(30), "Should reject 30 (too old)");
            Assert.False(window.CheckAndMark(36), "Should reject 36 (boundary)");
            Assert.True(window.CheckAndMark(37), "Should accept 37 (boundary)");
        }

        [Fact]
        public void Test_WindowShift()
        {
            var window = new ReplayWindow();
            Assert.True(window.CheckAndMark(10), "Start at 10");
            Assert.True(window.CheckAndMark(5), "Accept 5"); // Set bit for 5

            // Shift window way forward
            Assert.True(window.CheckAndMark(200), "Jump to 200");

            // Old 10 and 5 are now way out of window [200-63, 200] = [137, 200]
            Assert.False(window.CheckAndMark(10), "10 should be too old now");
            Assert.False(window.CheckAndMark(5), "5 should be too old now");

            // Allow new ones
            Assert.True(window.CheckAndMark(199), "Accept 199");
        }
    }
}
