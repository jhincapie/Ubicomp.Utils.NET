#nullable enable
using System;
using System.ComponentModel;
using System.Threading;
using Ubicomp.Utils.NET.ContextAwarenessFramework.ContextAdapter;
using Ubicomp.Utils.NET.ContextAwarenessFramework.ContextService;
using Ubicomp.Utils.NET.ContextAwarenessFramework.DataModel;

namespace ContextAwarenessFramework.TestApp
{
    /// <summary>
    /// Test application for the Context Awareness Framework.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("ContextAwarenessFramework Test App");

            var monitor = new TestMonitor();
            monitor.UpdateInterval = 1000;
            monitor.UpdateType = ContextAdapterUpdateType.Interval;

            var service = new TestService();
            monitor.OnNotifyContextServices += service.UpdateMonitorReading;

            Console.WriteLine("Adding Monitor...");
            ContextMonitorContainer.AddMonitor(monitor);

            Console.WriteLine("Starting Monitors...");
            ContextMonitorContainer.StartMonitors();

            Console.WriteLine("Waiting for updates...");
            Thread.Sleep(3500); // Wait for 3-4 ticks

            if (Environment.UserInteractive)
            {
                Console.WriteLine("Press any key to stop...");
                Console.ReadKey();
            }

            ContextMonitorContainer.StopMonitors();
        }
    }

    /// <summary>
    /// Test entity implementation for context monitoring.
    /// </summary>
    public class TestEntity : IEntity
    {
        /// <inheritdoc />
        public Guid EntityGuid { get; set; } = Guid.NewGuid();

        private string _value = string.Empty;

        /// <summary>Gets or sets a test value.</summary>
        public string Value
        {
            get => _value;
            set {
                _value = value;
                PropertyChanged?.Invoke(
                    this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }

        /// <inheritdoc />
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>
    /// Test implementation of a context monitor.
    /// </summary>
    public class TestMonitor : ContextMonitor
    {
        private readonly Random _rnd = new Random();

        /// <inheritdoc />
        protected override void CustomRun()
        {
            string data = $"Random Value: {_rnd.Next(1, 100)}";
            Console.WriteLine($"[Monitor] Generated: {data}");
            NotifyContextServices(
                this, new NotifyContextMonitorListenersEventArgs(typeof(string),
                                                                 data));
        }
    }

    /// <summary>
    /// Test implementation of a context service.
    /// </summary>
    public class TestService : ContextService
    {
        /// <inheritdoc />
        protected override void CustomUpdateMonitorReading(
            object sender, NotifyContextMonitorListenersEventArgs e)
        {
            string? data = e.NewObject as string;
            Console.WriteLine($"[Service] Received: {data}");
        }
    }
}
