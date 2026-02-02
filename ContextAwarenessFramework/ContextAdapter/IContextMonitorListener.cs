#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ubicomp.Utils.NET.ContextAwarenessFramework.ContextAdapter
{

    /// <summary>
    /// Defines a listener that can receive updates from a context monitor.
    /// </summary>
    public interface IContextMonitorListener
    {
        /// <summary>
        /// Called when a monitor has a new reading.
        /// </summary>
        /// <param name="sender">The monitor source.</param>
        /// <param name="e">The update arguments.</param>
        void UpdateMonitorReading(object sender,
                                  NotifyContextMonitorListenersEventArgs e);
    }

    /// <summary>
    /// Provides data for context monitor notification events.
    /// </summary>
    public class NotifyContextMonitorListenersEventArgs : EventArgs
    {
        /// <summary>Gets or sets the type of context data.</summary>
        public Type Type { get; set; }

        /// <summary>Gets or sets the context data object.</summary>
        public object NewObject { get; set; }

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="NotifyContextMonitorListenersEventArgs"/> class.
        /// </summary>
        /// <param name="type">The type of context data.</param>
        /// <param name="newObject">The context data object.</param>
        public NotifyContextMonitorListenersEventArgs(Type type,
                                                      object newObject)
        {
            Type = type;
            NewObject = newObject;
        }
    }

    /// <summary>
    /// Represents the method that handles context monitor update events.
    /// </summary>
    public delegate void NotifyContextMonitorListeners(
        object sender, NotifyContextMonitorListenersEventArgs e);

}
