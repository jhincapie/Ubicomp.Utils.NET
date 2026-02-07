#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ubicomp.Utils.NET.Generators.AutoDiscovery;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;

namespace Ubicomp.Utils.NET.SampleApp
{
    /// <summary>
    /// Sample application demonstrating the usage of the transport framework.
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Ubicomp.Utils.NET Sample App");

            Program app = new Program();
            await app.RunAsync(args);
        }

        /// <summary>
        /// Runs the sample application logic.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public async Task RunAsync(string[] args)
        {
            // Build Configuration
            var switchMappings = new Dictionary<string, string>()
            {
                { "--key", "Network:SecurityKey" },
                { "--address", "Network:GroupAddress" },
                { "--port", "Network:Port" },
                { "--ttl", "Network:TTL" },
                { "--interface", "Network:Interface" }
            };

            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddCommandLine(args, switchMappings);

            IConfiguration config = configBuilder.Build();

            // Parse flags not handled by config binding
            bool noWait = args.Contains("--no-wait");
            bool verbose = args.Contains("-v") || args.Contains("--verbose");
            bool allowLocal = args.Contains("--local");
            bool noEncryption = args.Contains("--no-encryption");

            // Read Config
            string groupAddressStr = config["Network:GroupAddress"] ?? "239.0.0.1";
            int port = config.GetValue<int>("Network:Port", 5000);
            int ttl = config.GetValue<int>("Network:TTL", 1);
            string? interfaceIp = config["Network:Interface"];
            string? securityKey = config["Network:SecurityKey"];

            bool encryptionEnabled = !string.IsNullOrEmpty(securityKey) && !noEncryption;

            if (verbose)
            {
                Console.WriteLine($"Configuration:");
                Console.WriteLine($"  Address: {groupAddressStr}");
                Console.WriteLine($"  Port: {port}");
                Console.WriteLine($"  TTL: {ttl}");
                if (!string.IsNullOrEmpty(interfaceIp))
                    Console.WriteLine($"  Interface: {interfaceIp}");
                Console.WriteLine($"  Security Key: {(string.IsNullOrEmpty(securityKey) ? "None" : "***")}");
                Console.WriteLine($"  Encryption: {(encryptionEnabled ? "Enabled" : "Disabled")}");
            }

            if (!IPAddress.TryParse(groupAddressStr, out var groupAddress))
            {
                Console.WriteLine($"Invalid IP Address: {groupAddressStr}. Defaulting to 239.0.0.1");
                groupAddress = IPAddress.Parse("239.0.0.1");
            }

            // 1. Create a logger factory
            using ILoggerFactory loggerFactory = LoggerFactory.Create(
                builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Trace);
                });

            var options = MulticastSocketOptions.WideAreaNetwork(groupAddress.ToString(), port, ttl);
            options.MulticastLoopback = allowLocal;

            if (!string.IsNullOrEmpty(interfaceIp) && IPAddress.TryParse(interfaceIp, out var nicIp))
            {
                options.InterfaceFilter = ip => ip.Equals(nicIp);
            }

            // 2. Setup TransportBuilder
            var builder = new TransportBuilder()
                .WithMulticastOptions(options)
                .WithLogging(loggerFactory)
                .WithLocalSource("SampleApp")
                .WithAutoSendAcks(false);

            if (!string.IsNullOrEmpty(securityKey))
            {
                builder.WithSecurityKey(securityKey);
                builder.WithEncryption(encryptionEnabled);
            }

            var transport = builder.Build();

            transport.RegisterHandler<SimpleContent>((content, context) =>
            {
                Console.WriteLine($"Received message: {content.Text} from {context.Source.ResourceName}");
                if (context.RequestAck)
                {
                    Console.WriteLine("Manually sending Ack...");
                    _ = transport.SendAckAsync(context);
                }
            });

            Console.WriteLine("Initializing Transport Component...");

            if (verbose)
            {
                Console.WriteLine("Multicast Options:");
                Console.WriteLine($"  Group Address: {options.GroupAddress}");
                Console.WriteLine($"  Port: {options.Port}");
                Console.WriteLine($"  TTL: {options.TimeToLive}");
            }

            transport.Start();

            // Run network diagnostics
            await transport.VerifyNetworkingAsync();

            // --- Feature: Reactive Extensions (Rx) ---
            // Subscribe to the message stream using Rx
            var rxSubscription = transport.MessageStream
                .Where(m => m.Length > 0) // Example filter
                .Subscribe(msg =>
                {
                    // This is a raw message tap
                    // Console.WriteLine($"[Rx Tap] Received {msg.Length} bytes.");
                });

            // --- Feature: Peer Discovery ---
            // Print active peers periodically
            var peerMonitor = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(5000);
                    if (verbose)
                    {
                        var peers = transport.ActivePeers.ToList();
                        Console.WriteLine($"\n--- Active Peers ({peers.Count}) ---");
                        foreach (var peer in peers)
                        {
                            Console.WriteLine($" - {peer.DeviceName} (ID: {peer.SourceId}) Metadata: {peer.Metadata}");
                        }
                        Console.WriteLine("---------------------------\n");
                    }
                }
            });

            if (args.Contains("--ack"))
            {
                var content = new SimpleContent { Text = "Ping with Ack request" };

                Console.WriteLine("Sending message with Ack request...");
                var session = await transport.SendAsync(content, new SendOptions { RequestAck = true });

                session.OnAckReceived += (s, source) =>
                {
                    Console.WriteLine($"> Received Ack from: {source.ResourceName} ({source.FriendlyName ?? "No friendly name"})");
                };

                // Wait for acks in a background task if not in noWait mode
                var waitTask = session.WaitAsync(transport.DefaultAckTimeout);
                if (noWait)
                {
                    await waitTask;
                    Console.WriteLine(session.IsAnyAckReceived ? "Ack session completed with success." : "Ack session timed out: no acks received.");
                }
                else
                {
                    _ = waitTask.ContinueWith(t =>
                    {
                        Console.WriteLine(t.Result ? "Ack session completed with success." : "Ack session timed out: no acks received.");
                    });
                }
            }

            if (noWait)
            {
                Console.WriteLine("Waiting 5 seconds for messages...");
                System.Threading.Thread.Sleep(5000);
            }
            else
            {
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
            }

            transport.Stop();
            rxSubscription.Dispose();
        }


    }

    /// <summary>
    /// Simple content model for transport messages.
    /// </summary>
    [MessageType("ubicomp.utils.net.sampleapp.SimpleContent")]
    public class SimpleContent
    {
        /// <summary>Gets or sets the text content.</summary>
        public string Text { get; set; } = string.Empty;
    }
}
