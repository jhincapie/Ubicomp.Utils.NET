using System;
using System.Net;
using Ubicomp.Utils.NET.Sockets;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Ubicomp.Utils.NET.SampleApp
{
    class Program : ITransportListener
    {
        public const int SampleAppID = 2;

        static void Main(string[] args)
        {
            Console.WriteLine("Ubicomp.Utils.NET Sample App");
            
            Program app = new Program();
            app.Run(args);
        }

        public void Run(string[] args)
        {
            bool noWait = false;
            foreach (var arg in args)
            {
                if (arg == "--no-wait") noWait = true;
            }

            // 1. Create a logger factory
            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Trace);
            });

            // 2. Create a logger for the TransportComponent
            ILogger<TransportComponent> logger = loggerFactory.CreateLogger<TransportComponent>();
            
            // 3. Inject the logger
            TransportComponent.Instance.Logger = logger;

            // Configure Transport Component
            TransportComponent.Instance.MulticastGroupAddress = IPAddress.Parse("239.0.0.1");
            TransportComponent.Instance.Port = 5000;
            TransportComponent.Instance.UDPTTL = 1;
            
            // Register this app as a listener for message type 2
            TransportComponent.Instance.TransportListeners.Add(SampleAppID, this);
            
            // Register message type for deserialization
            TransportMessageConverter.KnownTypes.Add(SampleAppID, typeof(SimpleContent));
            
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

        public void MessageReceived(TransportMessage message, string rawMessage)
        {
            if (message.MessageData is SimpleContent content)
            {
                Console.WriteLine($"Received message: {content.Text} from {message.MessageSource.ResourceName}");
            }
            else
            {
                Console.WriteLine($"Received raw message of type {message.MessageType}: {rawMessage}");
            }
        }
    }

    public class SimpleContent : ITransportMessageContent
    {
        public string Text { get; set; }
    }
}
