#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Ubicomp.Utils.NET.ContextAwarenessFramework.ContextAdapter
{

    /// <summary>
    /// Manages a collection of <see cref="ContextMonitor"/> instances and their
    /// lifecycles.
    /// </summary>
    public abstract class ContextMonitorContainer
    {
        private static bool _monitorsStarted = false;
        private static readonly List<ContextMonitor> _monitors =
            new List<ContextMonitor>();
        private static readonly Dictionary<ContextMonitor, Thread> _threads =
            new Dictionary<ContextMonitor, Thread>();

        /// <summary>
        /// Registers a new monitor and prepares its background thread.
        /// </summary>
        /// <param name="monitor">The monitor to add.</param>
        public static void AddMonitor(ContextMonitor monitor)
        {
            Thread monitorThread =
                new Thread(monitor.Run) { IsBackground = true };

            _monitors.Add(monitor);
            _threads.Add(monitor, monitorThread);

            if (_monitorsStarted)
                StartMonitor(monitor, monitorThread);
        }

        /// <summary>
        /// Stops and removes a monitor.
        /// </summary>
        /// <param name="monitor">The monitor to remove.</param>
        public static void RemoveMonitor(ContextMonitor monitor)
        {
            monitor.Stop();

            _monitors.Remove(monitor);
            _threads.Remove(monitor);

            if (_monitors.Count == 0)
                _monitorsStarted = false;
        }

        /// <summary>
        /// Starts all registered monitors.
        /// </summary>
        public static void StartMonitors()
        {
            foreach (ContextMonitor monitor in _monitors)
            {
                Thread thread = _threads[monitor];
                StartMonitor(monitor, thread);
            }
            _monitorsStarted = true;
        }

        private static void StartMonitor(ContextMonitor monitor, Thread thread)
        {
            monitor.Start();
            thread.Start();
        }

        /// <summary>
        /// Stops all registered monitors.
        /// </summary>
        public static void StopMonitors()
        {
            foreach (ContextMonitor monitor in _monitors)
                monitor.Stop();
            _monitorsStarted = false;
        }

        /// <summary>
        /// Retrieves a registered monitor of the specified type.
        /// </summary>
        /// <param name="monitorType">The type of the monitor.</param>
        /// <returns>The monitor instance, or null if not found.</returns>
        public static ContextMonitor? GetContextMonitor(Type monitorType)
        {
            return _monitors.FirstOrDefault(m => m.GetType() == monitorType);
        }
    }

}
