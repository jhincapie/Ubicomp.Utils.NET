#nullable enable
using System;
using System.Linq;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;

namespace Ubicomp.Utils.NET.SampleApp
{
    /// <summary>
    /// Sample application demonstrating the usage of the transport framework.
    /// </summary>
    class Program : ITransportListener
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

            // Parse optional parameters or use defaults
            string groupAddressStr = GetArgValue(args, "--address") ?? "239.0.0.1";
            string portStr = GetArgValue(args, "--port") ?? "5000";
            string ttlStr = GetArgValue(args, "--ttl") ?? "1";

            if (!IPAddress.TryParse(groupAddressStr, out var groupAddress))
            {
                Console.WriteLine($"Invalid group address: {groupAddressStr}. Using default: 239.0.0.1");
                groupAddress = IPAddress.Parse("239.0.0.1");
            }

            if (!int.TryParse(portStr, out int port))
            {
                Console.WriteLine($"Invalid port: {portStr}. Using default: 5000");
                port = 5000;
            }

            if (!int.TryParse(ttlStr, out int ttl))
            {
                Console.WriteLine($"Invalid TTL: {ttlStr}. Using default: 1");
                ttl = 1;
            }

            // 1. Create a logger factory
            using ILoggerFactory loggerFactory = LoggerFactory.Create(
                builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Trace);
                });

            // 2. Create a logger for the TransportComponent
            ILogger<TransportComponent> logger =
                loggerFactory.CreateLogger<TransportComponent>();

            // 3. Inject the logger
            TransportComponent.Instance.Logger = logger;

            // Configure Transport Component
            TransportComponent.Instance.MulticastGroupAddress = groupAddress;
            TransportComponent.Instance.Port = port;
            TransportComponent.Instance.UDPTTL = ttl;

            // Register this app as a listener for message type 2
            TransportComponent.Instance.TransportListeners.Add(SampleAppID,
                                                               this);

            // Register message type for deserialization
            TransportMessageConverter.KnownTypes.Add(SampleAppID,
                                                     typeof(SimpleContent));

            Console.WriteLine("Initializing Transport Component...");

            if (verbose)
            {
                Console.WriteLine("Multicast Options:");
                Console.WriteLine($"  Group Address: {TransportComponent.Instance.MulticastGroupAddress}");
                Console.WriteLine($"  Port: {TransportComponent.Instance.Port}");
                Console.WriteLine($"  Local IP: {TransportComponent.Instance.LocalIPAddress?.ToString() ?? "Any"}");
                Console.WriteLine($"  TTL: {TransportComponent.Instance.UDPTTL}");
                // These are defaults used in TransportComponent.Init() via MulticastSocketOptions.WideAreaNetwork
                Console.WriteLine("  Reuse Address: True");
                Console.WriteLine("  Multicast Loopback: True");
                Console.WriteLine("  No Delay: True");
                Console.WriteLine("  Don't Fragment: False");
                Console.WriteLine("  Auto Join: True");
            }

            TransportComponent.Instance.Init();

            // Run network diagnostics
            TransportComponent.Instance.VerifyNetworking();

            if (args.Contains("--ack"))
            {
                var content = new SimpleContent { Text = "Ping with Ack request" };
                var msg = new TransportMessage(TransportComponent.Instance.LocalSource, SampleAppID, content)
                {
                    RequestAck = true
                };

                Console.WriteLine("Sending message with Ack request...");
                var session = TransportComponent.Instance.Send(msg);

                session.OnAckReceived += (s, source) =>
                {
                    Console.WriteLine($"> Received Ack from: {source.ResourceName} ({source.FriendlyName ?? "No friendly name"})");
                };

                // Wait for acks in a background task if not in noWait mode
                var waitTask = session.WaitAsync(TransportComponent.Instance.DefaultAckTimeout);
                if (noWait)
                {
                    waitTask.Wait();
                    Console.WriteLine(session.IsAnyAckReceived ? "Ack session completed with success." : "Ack session timed out.");
                }
                else
                {
                    waitTask.ContinueWith(t => {
                        Console.WriteLine(t.Result ? "Ack session completed with success." : "Ack session timed out.");
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

        /// <inheritdoc />
        public void MessageReceived(TransportMessage message, string rawMessage)
        {
            if (message.MessageData is SimpleContent content)
            {
                Console.WriteLine($"Received message: {content.Text} from " +
                                  $"{message.MessageSource.ResourceName}");
            }
            else
            {
                Console.WriteLine(
                    $"Received raw message of type {message.MessageType}: " +
                    $"{rawMessage}");
            }

            if (message.RequestAck)
            {
                Console.WriteLine($"Sending Ack for message {message.MessageId}...");
                TransportComponent.Instance.SendAck(message);
            }
        }
    }

    /// <summary>
    /// Simple content model for transport messages.
    /// </summary>
    public class SimpleContent : ITransportMessageContent
    {
        /// <summary>Gets or sets the text content.</summary>
        public string Text { get; set; } = string.Empty;
    }
}