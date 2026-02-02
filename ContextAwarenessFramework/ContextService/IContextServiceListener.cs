#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ubicomp.Utils.NET.ContextAwarenessFramework.ContextService
{
    /// <summary>
    /// Defines a listener that can receive notifications when a context service
    /// changes.
    /// </summary>
    public interface IContextServiceListener
    {
        /// <summary>
        /// Called when the service context has changed.
        /// </summary>
        /// <param name="sender">The service source.</param>
        /// <param name="e">The change arguments.</param>
        void ContextChanged(object sender,
                            NotifyContextServiceListenersEventArgs e);
    }

    /// <summary>
    /// Provides data for context service change events.
    /// </summary>
    public class NotifyContextServiceListenersEventArgs : EventArgs
    {
        /// <summary>Gets or sets the type of context data.</summary>
        public Type Type { get; set; }

        /// <summary>Gets or sets the context data object.</summary>
        public object NewObject { get; set; }

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="NotifyContextServiceListenersEventArgs"/> class.
        /// </summary>
        /// <param name="type">The type of context data.</param>
        /// <param name="newObject">The context data object.</param>
        public NotifyContextServiceListenersEventArgs(Type type,
                                                      object newObject)
        {
            Type = type;
            NewObject = newObject;
        }
    }

    /// <summary>
    /// Represents the method that handles context service change events.
    /// </summary>
    public delegate void NotifyContextServiceListeners(
        object sender, NotifyContextServiceListenersEventArgs e);

}
