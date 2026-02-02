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
            TransportComponent.Instance.MulticastGroupAddress =
                IPAddress.Parse("239.0.0.1");
            TransportComponent.Instance.Port = 5000;
            TransportComponent.Instance.UDPTTL = 1;

            // Register this app as a listener for message type 2
            TransportComponent.Instance.TransportListeners.Add(SampleAppID,
                                                               this);

            // Register message type for deserialization
            TransportMessageConverter.KnownTypes.Add(SampleAppID,
                                                     typeof(SimpleContent));

            Console.WriteLine("Initializing Transport Component...");
            TransportComponent.Instance.Init();

            // Run network diagnostics
            TransportComponent.Instance.VerifyNetworking();

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
