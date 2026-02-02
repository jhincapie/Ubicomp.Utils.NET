#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Ubicomp.Utils.NET.MulticastTransportFramework;

namespace Ubicomp.Utils.NET.Muticast.TestApp
{
    /// <summary>
    /// Test application for validating multicast transport functionality.
    /// </summary>
    class Program : ITransportListener
    {
        /// <summary>The unique ID for the test application.</summary>
        public const int ProgramID = 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="Program"/> class.
        /// </summary>
        /// <param name="groupIP">The multicast group IP.</param>
        /// <param name="port">The multicast port.</param>
        /// <param name="timeToLive">The multicast TTL.</param>
        public Program(string groupIP, int port, int timeToLive)
        {
            TransportComponent.Instance.MulticastGroupAddress =
                IPAddress.Parse(groupIP);
            TransportComponent.Instance.Port = port;
            TransportComponent.Instance.UDPTTL = timeToLive;
        }

        /// <summary>
        /// Configures the transport component for testing.
        /// </summary>
        public void Config()
        {
            TransportComponent.Instance.TransportListeners.Add(
                Program.ProgramID, this);

            // Register MockMessage type for polymorphic deserialization
            TransportMessageConverter.KnownTypes.Add(Program.ProgramID,
                                                     typeof(MockMessage));

            TransportComponent.Instance.Init();
        }

        private void SendMessages()
        {
            Guid localHostGuid = Guid.NewGuid();
            EventSource eventSource =
                new EventSource(localHostGuid, Environment.MachineName,
                                Environment.MachineName);

            MockMessage mMessage1 =
                new MockMessage() { Message = "Hello World 1" };
            MockMessage mMessage2 =
                new MockMessage() { Message = "Hello World 2" };
            MockMessage mMessage3 =
                new MockMessage() { Message = "Hello World 3" };
            MockMessage mMessage4 =
                new MockMessage() { Message = "Hello World 4" };
            MockMessage mMessage5 =
                new MockMessage() { Message = "Hello World 5" };

            TransportMessage tMessage1 =
                new TransportMessage(eventSource, Program.ProgramID, mMessage1);
            TransportMessage tMessage2 =
                new TransportMessage(eventSource, Program.ProgramID, mMessage2);
            TransportMessage tMessage3 =
                new TransportMessage(eventSource, Program.ProgramID, mMessage3);
            TransportMessage tMessage4 =
                new TransportMessage(eventSource, Program.ProgramID, mMessage4);
            TransportMessage tMessage5 =
                new TransportMessage(eventSource, Program.ProgramID, mMessage5);

            TransportComponent.Instance.Send(tMessage1);
            TransportComponent.Instance.Send(tMessage2);
            TransportComponent.Instance.Send(tMessage3);
            TransportComponent.Instance.Send(tMessage4);
            TransportComponent.Instance.Send(tMessage5);
        }

        /// <inheritdoc />
        public void MessageReceived(TransportMessage message, string rawMessage)
        {
            if (message.MessageData is MockMessage mock)
            {
                Console.WriteLine("MessageReceived: {0}", mock.Message);
            }
            else
            {
                Console.WriteLine("MessageReceived: (Raw or Unknown) " +
                                  rawMessage);
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Press any key to finish.");
            Program testObj = new Program("225.4.5.6", 5000, 10);
            testObj.Config();
            testObj.SendMessages();

            Console.Read();
        }
    }
}
