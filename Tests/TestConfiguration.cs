#nullable enable
using System;
using Ubicomp.Utils.NET.Sockets;

namespace Ubicomp.Utils.NET.Tests
{
    public static class TestConfiguration
    {
        // Default to true (In-Memory) to avoid network flakiness
        // Set env var "USE_REAL_SOCKETS=true" to override.
        public static bool UseInMemorySockets
        {
            get
            {
                string? env = Environment.GetEnvironmentVariable("USE_REAL_SOCKETS");
                if (bool.TryParse(env, out bool useReal) && useReal)
                {
                    return false;
                }
                return true;
            }
        }

        public static void ConfigureOptions(MulticastSocketOptions options)
        {
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                options.LocalIP = "127.0.0.1";
            }
            // Clear filter for tests to ensure connectivity (loopback etc.)
            options.InterfaceFilter = null;
        }

        public static IMulticastSocket CreateSocket(MulticastSocketOptions options)
        {
            ConfigureOptions(options);

            if (UseInMemorySockets)
            {
                return new InMemoryMulticastSocket(options.GroupAddress, options.Port);
            }
            else
            {
                return new MulticastSocketBuilder()
                    .WithOptions(options)
                    .Build();
            }
        }
    }
}
