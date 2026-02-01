using System;
using System.Threading;
using Ubicomp.Utils.NET.ContextAwarenessFramework.ContextAdapter;
using Ubicomp.Utils.NET.ContextAwarenessFramework.ContextService;
using Ubicomp.Utils.NET.ContextAwarenessFramework.DataModel;
using System.ComponentModel;

namespace ContextAwarenessFramework.TestApp
{
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

            Console.WriteLine("Press any key to stop...");
            Thread.Sleep(3500); // Wait for 3-4 ticks
            
            if (Environment.UserInteractive)
            {
                 Console.ReadKey();
            }

            ContextMonitorContainer.StopMonitors();
        }
    }

    public class TestEntity : IEntity
    {
        public Guid EntityGuid { get; set; } = Guid.NewGuid();
        
        private string _value;
        public string Value
        {
            get => _value;
            set
            {
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class TestMonitor : ContextMonitor
    {
        private Random _rnd = new Random();

        protected override void CustomRun()
        {
            string data = $"Random Value: {_rnd.Next(1, 100)}";
            Console.WriteLine($"[Monitor] Generated: {data}");
            NotifyContextServices(this, new NotifyContextMonitorListenersEventArgs(typeof(string), data));
        }
    }

    public class TestService : ContextService
    {
        protected override void CustomUpdateMonitorReading(object sender, NotifyContextMonitorListenersEventArgs e)
        {
            string data = e.NewObject as string;
            Console.WriteLine($"[Service] Received: {data}");
        }
    }
}