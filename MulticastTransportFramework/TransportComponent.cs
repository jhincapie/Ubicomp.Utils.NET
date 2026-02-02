#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Ubicomp.Utils.NET.Sockets;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{

    /// <summary>
    /// The central component for managing multicast transport communication.
    /// Handles message serialization, sequential processing via a gatekeeper,
    /// and routing messages to registered listeners.
    /// </summary>
    public class TransportComponent : ITransportListener
    {
        /// <summary>Gets or sets the logger for this component.</summary>
        public ILogger Logger { get; set; } = NullLogger.Instance;

        /// <summary>The unique ID for the transport component itself.</summary>
        public const int TransportComponentID = 0;

        /// <summary>
        /// The message type ID used for acknowledgements.
        /// </summary>
        public const int AckMessageType = 99;

        /// <summary>The singleton instance of the transport
        /// component.</summary>
        public static TransportComponent Instance = new TransportComponent();

        private static object importLock = new object();
        private static object exportLock = new object();

        private MulticastSocket _socket = null!;
        private IPAddress _multicastGroupAddress = null!;
        private IPAddress? _localAddress;
        private int _port;
        private int _udpTTL;

        /// <summary>
        /// A mapping of message types to their respective listeners.
        /// </summary>
        public Dictionary<int, ITransportListener> TransportListeners
        {
            get;
        } = new Dictionary<int, ITransportListener>();

        private readonly ConcurrentDictionary<Guid, AckSession> _activeSessions = new ConcurrentDictionary<Guid, AckSession>();

        private readonly JsonSerializerSettings _jsonSettings;

        private int _currentMessageCons = 0;
        private readonly object gate = new object();

        /// <summary>Gets or sets the default timeout for acknowledgements.</summary>
        public TimeSpan DefaultAckTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets the local source identification for this transport component.
        /// </summary>
        public EventSource LocalSource { get; set; } = new EventSource(Guid.NewGuid(), Environment.MachineName);

        /// <summary>
        /// Gets or sets a value indicating whether messages received from the local source
        /// should be ignored by this transport component.
        /// </summary>
        public bool IgnoreLocalMessages { get; set; } = true;

        /// <summary>Gets or sets the multicast group IP address.</summary>
        public IPAddress MulticastGroupAddress
        {
            get => _multicastGroupAddress;
            set => _multicastGroupAddress = value;
        }

        /// <summary>Gets or sets the local IP address to bind to.</summary>
        public IPAddress? LocalIPAddress
        {
            get => _localAddress;
            set => _localAddress = value;
        }

        /// <summary>Gets or sets the multicast port.</summary>
        public int Port
        {
            get => _port;
            set => _port = value;
        }

        /// <summary>Gets or sets the multicast Time-to-Live (TTL).</summary>
        public int UDPTTL
        {
            get => _udpTTL;
            set => _udpTTL = value;
        }

        private TransportComponent()
        {
            _jsonSettings = new JsonSerializerSettings();
            _jsonSettings.Converters.Add(new TransportMessageConverter());

            if (!TransportMessageConverter.KnownTypes.ContainsKey(AckMessageType))
            {
                TransportMessageConverter.KnownTypes.Add(AckMessageType, typeof(AckMessageContent));
            }

            TransportListeners.Add(TransportComponent.TransportComponentID,
                                   this);
            TransportListeners.Add(AckMessageType, this);
        }

        /// <summary>
        /// Initializes the transport component and starts listening for
        /// traffic.
        /// </summary>
        /// <exception cref="ApplicationException">
        /// Thrown if address or port are not specified.
        /// </exception>
        public void Init()
        {
            if (_multicastGroupAddress == null)
                throw new ApplicationException(
                    "Multicast group address not specified.");
            if (_port == 0)
                throw new ApplicationException(
                    "Multicast group port not specified.");

            Stop();

            var options = MulticastSocketOptions.WideAreaNetwork(_multicastGroupAddress.ToString(), _port, _udpTTL);

            options.LocalIP = _localAddress?.ToString();

            _socket = new MulticastSocket(options);
            _socket.OnNotifyMulticastSocketListener +=
                socket_OnNotifyMulticastSocketListener;

            lock (gate)
            {
                _currentMessageCons = 1;
                Monitor.PulseAll(gate);
            }
            _socket.StartReceiving();

            string interfaces = string.Join(
                ", ", _socket.JoinedAddresses.Select(a => a.ToString()));
            Logger.LogInformation("Multicast Socket Started to Listen for " +
                                      "Traffic on {0}:{1} (TTL: {2}, Interfaces: {3})",
                                  _multicastGroupAddress, _port, _udpTTL, interfaces);
            Logger.LogInformation("TransportComponent Initialized.");
        }

        /// <summary>
        /// Stops the transport component and closes the underlying socket.
        /// </summary>
        public void Stop()
        {
            if (_socket != null)
            {
                _socket.OnNotifyMulticastSocketListener -=
                    socket_OnNotifyMulticastSocketListener;
                _socket.Close();
                _socket.Dispose();
                _socket = null!;
            }
        }

        /// <summary>
        /// Verifies networking configuration by performing firewall checks and
        /// a loopback test.
        /// </summary>
        /// <returns>True if diagnostics pass, otherwise false.</returns>
        public bool VerifyNetworking()
        {
            Logger.LogInformation("Performing Network Diagnostics...");
            NetworkDiagnostics.LogFirewallStatus(_port, Logger);

            bool success = NetworkDiagnostics.PerformLoopbackTest(this);
            if (success)
            {
                Logger.LogInformation("Network Diagnostics Passed: Multicast " +
                                      "Loopback Successful.");
            }
            else
            {
                Logger.LogWarning("Network Diagnostics Failed: Multicast " +
                                  "Loopback NOT received. Check firewall " +
                                  "settings and interface configuration.");
            }
            return success;
        }

        /// <summary>
        /// Sends a transport message over the multicast socket.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <returns>An <see cref="AckSession"/> to track acknowledgements if requested.</returns>
        public AckSession Send(TransportMessage message)
        {
            var session = new AckSession(message.MessageId);

            if (message.RequestAck)
            {
                _activeSessions.TryAdd(message.MessageId, session);
                // Ensure cleanup after timeout
                _ = session.WaitAsync(DefaultAckTimeout).ContinueWith(_ =>
                {
                    _activeSessions.TryRemove(message.MessageId, out var _);
                });
            }

            string json;
            lock (exportLock)
                json =
                JsonConvert.SerializeObject(message, _jsonSettings);

            _socket?.Send(json);

            if (!message.RequestAck)
            {
                // Report "success" immediately if no ack requested
                session.ReportAck(LocalSource);
            }

            return session;
        }

        /// <summary>
        /// Sends an acknowledgement for the specified message.
        /// </summary>
        /// <param name="originalMessage">The message to acknowledge.</param>
        public void SendAck(TransportMessage originalMessage)
        {
            var ackContent = new AckMessageContent
            {
                OriginalMessageId = originalMessage.MessageId
            };

            var ackMessage = new TransportMessage(LocalSource, AckMessageType, ackContent)
            {
                RequestAck = false
            };

            Send(ackMessage);
        }

        private void socket_OnNotifyMulticastSocketListener(
            object sender, NotifyMulticastSocketListenerEventArgs e)
        {
            if (sender != _socket)
                return;

            if (e.Type == MulticastSocketMessageType.SendException)
            {
                Logger.LogError("Error Sending Message: {0}", e.NewObject);
                return;
            }

            if (e.Type == MulticastSocketMessageType.ReceiveException)
            {
                Logger.LogError("Error Receiving Message: {0}", e.NewObject);
                return;
            }

            if (e.Type != MulticastSocketMessageType.MessageReceived)
                return;

            if (e.NewObject == null)
                return;

            bool enteredGate = false;
            try
            {
                // Enter the gate first based on socket sequence ID to ensure
                // order, but we MUST exit it (nudge) even if deserialization
                // fails.
                GateKeeperMethod(e.Consecutive);
                enteredGate = true;

                string sMessage = GetMessageAsString((byte[])e.NewObject);
                TransportMessage? tMessage = null;

                lock (importLock)
                {
                    Logger.LogTrace("Importing message {0}", e.Consecutive);
                    tMessage = JsonConvert.DeserializeObject<TransportMessage>(
                        sMessage, _jsonSettings);
                }

                if (tMessage == null)
                {
                    Logger.LogWarning("Deserialization failed for message {0}",
                                      e.Consecutive);
                    return;
                }

                if (IgnoreLocalMessages && tMessage.MessageSource.ResourceId == LocalSource.ResourceId)
                {
                    Logger.LogTrace("Ignoring local message {0}", e.Consecutive);
                    return;
                }

                if (tMessage.MessageType == AckMessageType && tMessage.MessageData is AckMessageContent ackContent)
                {
                    if (_activeSessions.TryGetValue(ackContent.OriginalMessageId, out var session))
                    {
                        Logger.LogTrace("Received Ack for message {0} from {1}",
                                        ackContent.OriginalMessageId, tMessage.MessageSource.ResourceName);
                        session.ReportAck(tMessage.MessageSource);
                    }
                }

                Logger.LogTrace("Processing message {0}", e.Consecutive);
                if (!TransportListeners.TryGetValue(tMessage.MessageType,
                                                    out var listener))
                {
                    Logger.LogWarning(
                        "No listener registered for message type {0}",
                        tMessage.MessageType);
                    return;
                }

                listener.MessageReceived(tMessage, sMessage);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error Processing Received Message {0}: {1}",
                                e.Consecutive, ex.Message);
            }
            finally
            {
                if (enteredGate)
                    NudgeGate();
            }
        }

        private void GateKeeperMethod(int consecutive)
        {
            lock (gate)
            {
                while (_currentMessageCons != consecutive)
                {
                    Monitor.Wait(gate);
                }
            }
        }



        private void NudgeGate()
        {
            lock (gate)
            {
                _currentMessageCons++;
                Monitor.PulseAll(gate);
            }
        }

        #region ITransportListener Members

        /// <summary>
        /// Handles received messages for the transport component itself.
        /// </summary>
        /// <param name="message">The deserialized message.</param>
        /// <param name="rawMessage">The raw string representation.</param>
        public void MessageReceived(TransportMessage message, string rawMessage)
        {
            if (message.MessageType == AckMessageType)
            {
                Logger.LogTrace("Acknowledgement message {0} handled by internal session manager.", message.MessageId);
                return;
            }

            Logger.LogInformation(
                "Received Message for Transport Component - " +
                "Not Implemented Feature.");
        }

        #endregion

        private static string GetMessageAsString(byte[] messageBytes)
        {
            int length = Array.IndexOf<byte>(messageBytes, (byte)'\0');
            if (length == -1)
                length = messageBytes.Length;
            var encoding = new UTF8Encoding();
            return encoding.GetString(messageBytes, 0, length);
        }
    }

}
