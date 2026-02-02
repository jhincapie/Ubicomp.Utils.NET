#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

namespace Ubicomp.Utils.NET.Sockets
{
    /// <summary>
    /// Defines a listener for multicast socket events.
    /// </summary>
    public interface IMulticastSocketListener
    {
        /// <summary>
        /// Called when the multicast socket has a message or status update.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        void SocketMessage(object sender,
                           NotifyMulticastSocketListenerEventArgs e);
    }

    /// <summary>
    /// Specifies the type of notification from a multicast socket.
    /// </summary>
    public enum MulticastSocketMessageType
    {
        /// <summary>The socket has successfully started.</summary>
        SocketStarted,
        /// <summary>A message has been received.</summary>
        MessageReceived,
        /// <summary>An error occurred while receiving.</summary>
        ReceiveException,
        /// <summary>A message has been sent.</summary>
        MessageSent,
        /// <summary>An error occurred while sending.</summary>
        SendException
    }

    /// <summary>
    /// Provides data for multicast socket notification events.
    /// </summary>
    public class NotifyMulticastSocketListenerEventArgs : EventArgs
    {
        /// <summary>Gets the type of notification.</summary>
        public MulticastSocketMessageType Type
        {
            get;
        }

        /// <summary>Gets the object associated with the notification.</summary>
        public object? NewObject
        {
            get;
        }

        /// <summary>Gets the sequence number of the message.</summary>
        public int Consecutive
        {
            get;
        }

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="NotifyMulticastSocketListenerEventArgs"/> class.
        /// </summary>
        /// <param name="type">The type of notification.</param>
        /// <param name="newObject">The object associated with the
        /// event.</param> <param name="consecutive">The sequence
        /// number.</param>
        public NotifyMulticastSocketListenerEventArgs(
            MulticastSocketMessageType type, object? newObject,
            int consecutive = 0)
        {
            Type = type;
            NewObject = newObject;
            Consecutive = consecutive;
        }
    }

    /// <summary>
    /// Represents the method that will handle multicast socket events.
    /// </summary>
    public delegate void NotifyMulticastSocketListener(
        object sender, NotifyMulticastSocketListenerEventArgs e);
}
