#nullable enable
using System;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Logging;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;

namespace Ubicomp.Utils.NET.SampleApp
{
    /// <summary>
    /// Sample application demonstrating the usage of the transport framework.
    /// </summary>
    class Program
    {
        /// <summary>The ID for this sample application.</summary>
        public const int SampleAppID = 2;

        static void Main(string[] args)
        {
            Console.WriteLine("Ubicomp.Utils.NET Sample App");

            Program app = new Program();
            app.Run(args);
        }

        /// <summary>
        /// Runs the sample application logic.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public void Run(string[] args)
        {
            bool noWait = args.Contains("--no-wait");
            bool verbose = args.Contains("-v") || args.Contains("--verbose");
            bool allowLocal = args.Contains("--local");

            // Parse optional parameters or use defaults
            string groupAddressStr = GetArgValue(args, "--address") ?? "239.0.0.1";
            string portStr = GetArgValue(args, "--port") ?? "5000";
            string ttlStr = GetArgValue(args, "--ttl") ?? "1";

            if (!IPAddress.TryParse(groupAddressStr, out var groupAddress))
            {
                groupAddress = IPAddress.Parse("239.0.0.1");
            }

            if (!int.TryParse(portStr, out int port))
            {
                port = 5000;
            }

            if (!int.TryParse(ttlStr, out int ttl))
            {
                ttl = 1;
            }

            // 1. Create a logger factory
            using ILoggerFactory loggerFactory = LoggerFactory.Create(
                builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Trace);
                });

            var options = MulticastSocketOptions.WideAreaNetwork(groupAddress.ToString(), port, ttl);

            // 2. Use the new TransportBuilder
            var transport = new TransportBuilder()
                .WithMulticastOptions(options)
                .WithLogging(loggerFactory)
                .WithLocalSource("SampleApp")
                .IgnoreLocalMessages(!allowLocal)
                .WithAutoSendAcks(false)
                .Build();

            transport.RegisterHandler<SimpleContent>(SampleAppID, (content, context) =>
            {
                Console.WriteLine($"Received message: {content.Text} from {context.Source.ResourceName}");
                if (context.RequestAck)
                {
                    Console.WriteLine("Manually sending Ack...");
                    transport.SendAck(context);
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
            transport.VerifyNetworking();

            if (args.Contains("--ack"))
            {
                var content = new SimpleContent { Text = "Ping with Ack request" };

                Console.WriteLine("Sending message with Ack request...");
                var session = transport.Send(content, new SendOptions { RequestAck = true });

                session.OnAckReceived += (s, source) =>
                {
                    Console.WriteLine($"> Received Ack from: {source.ResourceName} ({source.FriendlyName ?? "No friendly name"})");
                };

                // Wait for acks in a background task if not in noWait mode
                var waitTask = session.WaitAsync(transport.DefaultAckTimeout);
                if (noWait)
                {
                    waitTask.Wait();
                    Console.WriteLine(session.IsAnyAckReceived ? "Ack session completed with success." : "Ack session timed out: no acks received.");
                }
                else
                {
                    waitTask.ContinueWith(t => {
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
        }

        private string? GetArgValue(string[] args, string flag)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals(flag, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Simple content model for transport messages.
    /// </summary>
    public class SimpleContent
    {
        /// <summary>Gets or sets the text content.</summary>
        public string Text { get; set; } = string.Empty;
    }
}
