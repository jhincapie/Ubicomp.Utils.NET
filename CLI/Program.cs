using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;

namespace Ubicomp.Utils.NET.CLI
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintHelp();
                return;
            }

            string command = args[0].ToLowerInvariant();

            switch (command)
            {
                case "check":
                    await RunCheck();
                    break;
                case "sniff":
                    await RunSniff();
                    break;
                case "dashboard":
                    await RunDashboard();
                    break;
                default:
                    Console.WriteLine($"Unknown command: {command}");
                    PrintHelp();
                    break;
            }
        }

        static void PrintHelp()
        {
            Console.WriteLine("Ubicomp.Utils.NET CLI Tool");
            Console.WriteLine("Usage:");
            Console.WriteLine("  check   - Runs network diagnostics (firewall, loopback)");
            Console.WriteLine("  sniff   - Listens for multicast packets and dumps them");
            Console.WriteLine("  dashboard- Show live dashboard");
        }

        static async Task RunCheck()
        {
            Console.WriteLine("Running Network Diagnostics...");

            // Setup simple logger
            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });
            ILogger logger = loggerFactory.CreateLogger("CLI");

            var options = new MulticastSocketOptions(); // Default

            using var transport = new TransportComponent(options);
            transport.Logger = logger;
            transport.Start();

            bool result = await transport.VerifyNetworkingAsync();

            if (result)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("SUCCESS: Multicast Loopback functionality verified.");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAILURE: Multicast Loopback failed. Check firewall.");
            }
            Console.ResetColor();
        }

        static async Task RunSniff()
        {
            Console.WriteLine("Starting Packet Sniffer (Press Ctrl+C to stop)...");

            var options = new MulticastSocketOptions(); // Default
             // Setup simple logger
            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Warning); // Only warnings/errors
            });
            ILogger logger = loggerFactory.CreateLogger("CLI");

            using var transport = new TransportComponent(options);
            transport.Logger = logger;
            transport.Start();

            Console.WriteLine($"Listening on {options.GroupAddress}:{options.Port}...");

            transport.MessageStream.Subscribe(msg =>
            {
                string dump = TransportDiagnostics.DumpPacket(msg.Data);
                Console.WriteLine("--------------------------------------------------");
                Console.WriteLine($"Received Packet from {msg.RemoteEndpoint} (Seq: {msg.ArrivalSequenceId})");
                Console.WriteLine(dump);
            });

            await Task.Delay(-1); // Block forever
        }

        static async Task RunDashboard()
        {
             var options = new MulticastSocketOptions(); // Default or parse args
             // For simplicity, dashboard uses defaults or could share arg parsing logic.
             // But DashboardCommand.RunAsync takes options.
             await DashboardCommand.RunAsync(options);
        }
    }
}
