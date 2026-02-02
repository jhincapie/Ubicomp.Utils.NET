#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ubicomp.Utils.NET.ContextAwarenessFramework.ContextAdapter;

namespace Ubicomp.Utils.NET.ContextAwarenessFramework.ContextService
{

    /// <summary>
    /// Specifies the persistence strategy for a context service.
    /// </summary>
    public enum ContextServicePersistenceType
    {
        /// <summary>No persistence.</summary>
        None,
        /// <summary>Persist at regular intervals.</summary>
        Periodic,
        /// <summary>Persist only when explicitly requested.</summary>
        OnRequest,
        /// <summary>Combine periodic and on-request persistence.</summary>
        Combined
    }

    /// <summary>
    /// Represents the method that handles monitor update events.
    /// </summary>
    public delegate void UpdateMonitorReadingDelegate(
        object sender, NotifyContextMonitorListenersEventArgs e);

    /// <summary>
    /// Represents the method that handles service change events.
    /// </summary>
    public delegate void ContextChangedDelegate(
        object sender, NotifyContextServiceListenersEventArgs e);

    /// <summary>
    /// Abstract base class for services that aggregate context data from
    /// monitors. Handles threading, persistence, and UI thread marshalling.
    /// </summary>
    public abstract class ContextService : IContextMonitorListener
    {
        /// <summary>Gets or sets the logger for this service.</summary>
        public ILogger Logger { get; set; } = NullLogger.Instance;

        /// <summary>Occurs when the service starts.</summary>
        public event EventHandler? OnStart;
        /// <summary>Occurs when the service stops.</summary>
        public event EventHandler? OnStop;
        /// <summary>Occurs when the service context changes.</summary>
        public event
            NotifyContextServiceListeners? OnNotifyContextServiceListeners;

        /// <summary>Gets or sets the persistence type.</summary>
        protected ContextServicePersistenceType PersistenceType
        {
            get; set;
        } = ContextServicePersistenceType.None;

        /// <summary>Gets or sets the persistence interval in
        /// milliseconds.</summary>
        public int PersistInterval { get; set; } = 60000;

        private bool _persistRequested = false;
        private DateTime _datePersistRequested = DateTime.MinValue;
        private bool _stopped = false;

        /// <summary>Template method to load initial entity data.</summary>
        protected virtual void LoadEntities()
        {
        }

        /// <summary>Template method called before persistence starts.</summary>
        protected virtual void PreparePersist()
        {
        }

        /// <summary>Template method to execute actual persistence
        /// logic.</summary>
        protected virtual void PersistEntities()
        {
        }

        /// <summary>Notifies listeners of a context change.</summary>
        protected void NotifyContextServiceListeners(
            object sender, NotifyContextServiceListenersEventArgs e)
        {
            OnNotifyContextServiceListeners?.Invoke(sender, e);
        }

        /// <summary>Starts the service.</summary>
        public void Start()
        {
            CustomStart();
            OnStart?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Template method for subclass-specific start
        /// logic.</summary>
        protected virtual void CustomStart()
        {
        }

        /// <summary>Stops the service and triggers final persistence.</summary>
        public void Stop()
        {
            _stopped = true;
            ExecutePersit();
            CustomStop();
            OnStop?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Template method for subclass-specific stop logic.</summary>
        protected virtual void CustomStop()
        {
        }

        /// <summary>The main execution loop for persistence.</summary>
        internal void Run()
        {
            if (PersistenceType == ContextServicePersistenceType.None)
                return;

            while (!_stopped)
            {
                try
                {
                    if (PersistenceType ==
                            ContextServicePersistenceType.Periodic ||
                        PersistenceType ==
                            ContextServicePersistenceType.Combined)
                        Thread.Sleep(PersistInterval);
                    if (PersistenceType ==
                        ContextServicePersistenceType.OnRequest)
                        Thread.Sleep(500);
                    if (_stopped)
                        break;

                    ExecutePersit();
                }
                catch (ThreadAbortException)
                {
                }
            }
        }

        private readonly object _lockPersist = new object();

        /// <summary>Executes the persistence logic if required.</summary>
        private void ExecutePersit()
        {
            lock (_lockPersist)
            {
                PreparePersist();

                bool shouldPersist = false;
                if (PersistenceType == ContextServicePersistenceType.Periodic)
                {
                    shouldPersist = true;
                }
                else if ((PersistenceType ==
                              ContextServicePersistenceType.OnRequest ||
                          PersistenceType ==
                              ContextServicePersistenceType.Combined) &&
                         _persistRequested)
                {
                    shouldPersist = true;
                    _persistRequested = false;
                }

                if (shouldPersist)
                {
                    PersistEntities();
                }
            }
        }

        /// <summary>Requests that persistence be executed on the next
        /// cycle.</summary>
        public void RequestPersist()
        {
            _persistRequested = true;
            _datePersistRequested = DateTime.Now;
        }

        #region IContextMonitorListener Members

        /// <summary>
        /// Handles a new reading from a monitor.
        /// </summary>
        /// <param name="sender">The monitor that provided the reading.</param>
        /// <param name="e">The reading data.</param>
        public void UpdateMonitorReading(
            object sender, NotifyContextMonitorListenersEventArgs e)
        {
            try
            {
                CustomUpdateMonitorReading(sender, e);
            }
            catch (Exception exception)
            {
                Logger.LogError(exception,
                                "An error occurred processing monitor reading");
            }
        }

        /// <summary>Template method for subclass-specific reading
        /// logic.</summary>
        protected virtual void CustomUpdateMonitorReading(
            object sender, NotifyContextMonitorListenersEventArgs e)
        {
        }

        #endregion
    }

}
