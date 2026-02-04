#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;

namespace Ubicomp.Utils.NET.Muticast.TestApp
{
    /// <summary>
    /// Test application for validating multicast transport functionality.
    /// </summary>
    class Program
    {
        /// <summary>The message ID used for testing.</summary>
        public const string ProgramID = "1";

        private readonly TransportComponent _transport;

        /// <summary>
        /// Initializes a new instance of the <see cref="Program"/> class.
        /// </summary>
        /// <param name="groupIP">The multicast group IP.</param>
        /// <param name="port">The multicast port.</param>
        /// <param name="timeToLive">The multicast TTL.</param>
        public Program(string groupIP, int port, int timeToLive)
        {
            var options = MulticastSocketOptions.WideAreaNetwork(groupIP, port, timeToLive);

            _transport = new TransportBuilder()
                .WithMulticastOptions(options)
                .WithLocalSource(Environment.MachineName)
                .RegisterHandler<MockMessage>(ProgramID, (message, context) =>
                {
                    Console.WriteLine("MessageReceived: {0}", message.Message);
                })
                .Build();
        }

        /// <summary>
        /// Configures the transport component for testing.
        /// </summary>
        public void Start()
        {
            _transport.Start();
        }

        private async Task SendMessagesAsync()
        {
            await _transport.SendAsync(new MockMessage() { Message = "Hello World 1" });
            await _transport.SendAsync(new MockMessage() { Message = "Hello World 2" });
            await _transport.SendAsync(new MockMessage() { Message = "Hello World 3" });
            await _transport.SendAsync(new MockMessage() { Message = "Hello World 4" });
            await _transport.SendAsync(new MockMessage() { Message = "Hello World 5" });
        }

        static async Task Main(string[] args)
        {
            Console.WriteLine("Press any key to finish.");
            Program testObj = new Program("225.4.5.6", 5000, 10);
            testObj.Start();
            await testObj.SendMessagesAsync();

            Console.Read();
        }
    }
}
