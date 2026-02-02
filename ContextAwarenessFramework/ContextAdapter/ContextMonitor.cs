#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Ubicomp.Utils.NET.ContextAwarenessFramework.ContextAdapter
{

    /// <summary>
    /// Specifies how a context monitor should update its readings.
    /// </summary>
    public enum ContextAdapterUpdateType
    {
        /// <summary>Continuously poll for updates.</summary>
        Continuous,
        /// <summary>Poll at regular intervals.</summary>
        Interval,
        /// <summary>Only poll when explicitly requested.</summary>
        OnRequest
    }

    /// <summary>
    /// Abstract base class for monitoring context data sources.
    /// Handles threading and update lifecycle logic.
    /// </summary>
    public abstract class ContextMonitor : IDisposable
    {
        /// <summary>Occurs when the monitor starts.</summary>
        public event EventHandler? OnStart;
        /// <summary>Occurs when the monitor stops.</summary>
        public event EventHandler? OnStop;
        /// <summary>Occurs when new context data is available.</summary>
        public event NotifyContextMonitorListeners? OnNotifyContextServices;

        /// <summary>Gets or sets the update strategy.</summary>
        public ContextAdapterUpdateType UpdateType {
            get; set;
        } = ContextAdapterUpdateType.Continuous;

        /// <summary>Gets or sets the update interval in milliseconds.</summary>
        public int UpdateInterval { get; set; } = 3000;

        private bool _stopped = false;
        private readonly ManualResetEvent _stopWaitHandle =
            new ManualResetEvent(false);

        /// <summary>
        /// Starts the monitor.
        /// </summary>
        public void Start()
        {
            _stopWaitHandle.Reset();
            _stopped = false;
            CustomStart();
            OnStart?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Template method for subclass-specific start
        /// logic.</summary>
        protected virtual void CustomStart()
        {
        }

        /// <summary>
        /// Stops the monitor and signals the background thread.
        /// </summary>
        public void Stop()
        {
            _stopped = true;
            _stopWaitHandle.Set();
            CustomStop();
            OnStop?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Template method for subclass-specific stop logic.</summary>
        protected virtual void CustomStop()
        {
        }

        /// <summary>
        /// The main execution loop for the monitor.
        /// </summary>
        internal void Run()
        {
            if (UpdateType == ContextAdapterUpdateType.OnRequest)
                return;

            while (!_stopped)
            {
                if (UpdateType == ContextAdapterUpdateType.Interval &&
                    _stopWaitHandle.WaitOne(UpdateInterval, false))
                    break;

                if (_stopped)
                    break;

                CustomRun();
            }
        }

        /// <summary>Template method for the main monitoring logic.</summary>
        protected virtual void CustomRun()
        {
        }

        /// <summary>
        /// Notifies listeners about a new context update.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event arguments.</param>
        protected void NotifyContextServices(
            object sender, NotifyContextMonitorListenersEventArgs e)
        {
            OnNotifyContextServices?.Invoke(sender, e);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases resources used by the monitor.
        /// </summary>
        /// <param name="disposing">True to release managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            _stopWaitHandle?.Dispose();
        }
    }

}
