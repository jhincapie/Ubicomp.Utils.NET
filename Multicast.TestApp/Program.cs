#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;

namespace Ubicomp.Utils.NET.Muticast.TestApp
{
    /// <summary>
    /// Test application for validating multicast transport functionality.
    /// </summary>
    class Program
    {
        /// <summary>The unique ID for the test application.</summary>
        public const int ProgramID = 1;

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

        private void SendMessages()
        {
            _transport.Send(new MockMessage() { Message = "Hello World 1" });
            _transport.Send(new MockMessage() { Message = "Hello World 2" });
            _transport.Send(new MockMessage() { Message = "Hello World 3" });
            _transport.Send(new MockMessage() { Message = "Hello World 4" });
            _transport.Send(new MockMessage() { Message = "Hello World 5" });
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Press any key to finish.");
            Program testObj = new Program("225.4.5.6", 5000, 10);
            testObj.Start();
            testObj.SendMessages();

            Console.Read();
        }
    }
}