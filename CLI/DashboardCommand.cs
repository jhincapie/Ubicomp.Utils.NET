using System;
using System.Linq;
using System.Threading.Tasks;
using Spectre.Console;
using Ubicomp.Utils.NET.MulticastTransportFramework;
using Ubicomp.Utils.NET.Sockets;

namespace Ubicomp.Utils.NET.CLI
{
    public class DashboardCommand
    {
        public static async Task RunAsync(MulticastSocketOptions options)
        {
            var transport = new TransportBuilder()
                .WithMulticastOptions(options)
                .WithLocalSource("CLI-Dashboard")
                //.WithLogging() // We might want to capture logs for the dashboard?
                .Build();

            transport.Start();

            // Run verification to populate peers quickly?
            _ = transport.VerifyNetworkingAsync();

            await Console.Out.WriteLineAsync("Starting Dashboard...");

            // Spectre Console Live Display
            await AnsiConsole.Live(GetLayout(transport))
                .AutoClear(false)
                .Overflow(VerticalOverflow.Ellipsis)
                .Cropping(VerticalOverflowCropping.Bottom)
                .StartAsync(async ctx =>
                {
                    while (true)
                    {
                        ctx.UpdateTarget(GetLayout(transport));
                        await Task.Delay(500);

                        // Exit condition?
                        if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q)
                        {
                            break;
                        }
                    }
                });

            transport.Stop();
        }

        private static Layout GetLayout(TransportComponent transport)
        {
            var layout = new Layout("Root")
                .SplitColumns(
                    new Layout("Left")
                        .SplitRows(
                            new Layout("Status"),
                            new Layout("Peers")
                        ),
                    new Layout("Right")
                        .SplitRows(
                            new Layout("Metrics"),
                            new Layout("Logs")
                        )
                );

            // Status Panel
            var statusTable = new Table()
                .AddColumn("Property")
                .AddColumn("Value")
                .AddRow("State", transport.IsRunning ? "[green]Running[/]" : "[red]Stopped[/]")
                .AddRow("Address", transport.Options.GroupAddress)
                .AddRow("Port", transport.Options.Port.ToString())
                .AddRow("Source ID", transport.LocalSource.ResourceId.ToString());

            layout["Status"].Update(
                new Panel(statusTable)
                    .Header("Transport Status")
                    .Border(BoxBorder.Rounded));

            // Peers Panel
            var peersTable = new Table()
                .AddColumn("ID")
                .AddColumn("Friendly Name")
                .AddColumn("Last Seen");

            foreach (var peer in transport.ActivePeers)
            {
                peersTable.AddRow(
                    peer.SourceId.ToString(),
                    peer.DeviceName ?? "-",
                    DateTime.Now.ToString("HH:mm:ss"));
            }

            layout["Peers"].Update(
                 new Panel(peersTable)
                    .Header($"Active Peers ({transport.ActivePeers.Count()})")
                    .Border(BoxBorder.Rounded)
                    .Expand());

            // Metrics (Placeholder)
            layout["Metrics"].Update(
                new Panel(new Markup("[yellow]Metrics not implemented[/]"))
                    .Header("Metrics")
                    .Border(BoxBorder.Rounded));

            // Logs (Placeholder / Capture)
            layout["Logs"].Update(
                new Panel(new Markup("[grey]Logs will appear here[/]"))
                    .Header("Recent Logs")
                    .Border(BoxBorder.Rounded));

            return layout;
        }
    }
}
