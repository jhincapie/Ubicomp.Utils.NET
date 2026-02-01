using System;
using System.Net;
using Ubicomp.Utils.NET.Socket;

namespace Ubicomp.Utils.NET.SampleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Ubicomp.Utils.NET Sample App");
            
            var socket = new MulticastSocket("239.0.0.1", 5000, 1);
            socket.OnMessageReceived += (sender, message) => 
            {
                Console.WriteLine($"Received message: {message}");
            };

            Console.WriteLine("Starting multicast socket...");
            socket.Start();

            Console.WriteLine("Sending a test message...");
            socket.Send("Hello from Sample App!");

            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();

            socket.Stop();
        }
    }
}
