#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Ubicomp.Utils.NET.Sockets;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    /// <summary>
    /// The central component for managing multicast transport communication.
    /// Handles message serialization, sequential processing via a gatekeeper,
    /// and routing messages to registered handlers.
    /// </summary>
    public class TransportComponent
    {
        /// <summary>Gets or sets the logger for this component.</summary>
        public ILogger Logger { get; set; } = NullLogger.Instance;

        /// <summary>The unique ID for the transport component itself.</summary>
        public const int TransportComponentID = 0;

        /// <summary>
        /// The message type ID used for acknowledgements.
        /// </summary>
        public const int AckMessageType = 99;

        private static readonly object importLock = new object();
        private static readonly object exportLock = new object();

        private MulticastSocket? _socket;
        private readonly MulticastSocketOptions _socketOptions;

        private readonly ConcurrentDictionary<int, Delegate> _genericHandlers = new ConcurrentDictionary<int, Delegate>();
        private readonly ConcurrentDictionary<Type, int> _typeToIdMap = new ConcurrentDictionary<Type, int>();

        private readonly ConcurrentDictionary<Guid, AckSession> _activeSessions = new ConcurrentDictionary<Guid, AckSession>();

        private readonly JsonSerializerSettings _jsonSettings;

        private int _currentMessageCons = 1;
        private readonly object gate = new object();
        private bool _isStopping = false;
        private readonly SortedDictionary<int, SocketMessage> _waitingMessages = new SortedDictionary<int, SocketMessage>();

        /// <summary>Gets or sets the default timeout for acknowledgements.</summary>
        public TimeSpan DefaultAckTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets the timeout for the GateKeeper to wait for a specific sequence number.
        /// Defaults to 500ms.
        /// </summary>
        public TimeSpan GateKeeperTimeout { get; set; } = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Gets or sets the local source identification for this transport component.
        /// </summary>
        public EventSource LocalSource { get; set; } = new EventSource(Guid.NewGuid(), Environment.MachineName);

        /// <summary>
        /// Gets or sets a value indicating whether messages received from the local source
        /// should be ignored by this transport component.
        /// </summary>
        public bool IgnoreLocalMessages { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether acknowledgements should be sent automatically
        /// when a message requesting one is received and handled.
        /// </summary>
        public bool AutoSendAcks { get; set; } = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="TransportComponent"/> class.
        /// </summary>
        /// <param name="options">The multicast socket options to use.</param>
        public TransportComponent(MulticastSocketOptions options)
        {
            _socketOptions = options;
            _jsonSettings = new JsonSerializerSettings();
            _jsonSettings.Converters.Add(new TransportMessageConverter());

            RegisterInternalTypes();
        }

        private void RegisterInternalTypes()
        {
            if (!TransportMessageConverter.KnownTypes.ContainsKey(AckMessageType))
            {
                TransportMessageConverter.KnownTypes.Add(AckMessageType, typeof(AckMessageContent));
            }
        }

        /// <summary>
        /// Registers a handler for a specific message type.
        /// </summary>
        public void RegisterHandler<T>(int id, Action<T, MessageContext> handler) where T : class
        {
            _genericHandlers[id] = handler;
            _typeToIdMap[typeof(T)] = id;
            if (!TransportMessageConverter.KnownTypes.ContainsKey(id))
            {
                TransportMessageConverter.KnownTypes.Add(id, typeof(T));
            }
        }

        /// <summary>
        /// Starts the transport component and starts listening for traffic.
        /// </summary>
        public void Start()
        {
            Stop();

            _socket = new MulticastSocketBuilder()
                .WithOptions(_socketOptions)
                .OnMessageReceived(msg => HandleSocketMessage(msg))
                .OnError(err => Logger.LogError("Socket Error: {0}. Exception: {1}", err.Message, err.Exception?.Message))
                .Build();

            lock (gate)
            {
                _currentMessageCons = 1;
                _isStopping = false;
                _waitingMessages.Clear();
                Monitor.PulseAll(gate);
            }
            _socket.StartReceiving();

            string interfaces = string.Join(", ", _socket.JoinedAddresses.Select(a => a.ToString()));
            Logger.LogInformation("Multicast Socket Started to Listen for Traffic on {0}:{1} (TTL: {2}, Interfaces: {3})",
                                  _socketOptions.GroupAddress, _socketOptions.Port, _socketOptions.TimeToLive, interfaces);
            Logger.LogInformation("TransportComponent Initialized.");
        }

        /// <summary>
        /// Stops the transport component and closes the underlying socket.
        /// </summary>
        public void Stop()
        {
            lock (gate)
            {
                _isStopping = true;
                Monitor.PulseAll(gate);
            }

            if (_socket != null)
            {
                _socket.Close();
                _socket.Dispose();
                _socket = null;
            }
        }

        /// <summary>
        /// Verifies networking configuration by performing firewall checks and a loopback test.
        /// </summary>
        /// <returns>True if diagnostics pass, otherwise false.</returns>
        public bool VerifyNetworking()
        {
            Logger.LogInformation("Performing Network Diagnostics...");
            NetworkDiagnostics.LogFirewallStatus(_socketOptions.Port, Logger);

            bool success = NetworkDiagnostics.PerformLoopbackTest(this);
            if (success)
            {
                Logger.LogInformation("Network Diagnostics Passed: Multicast Loopback Successful.");
            }
            else
            {
                Logger.LogWarning("Network Diagnostics Failed: Multicast Loopback NOT received. Check firewall settings and interface configuration.");
            }
            return success;
        }

        /// <summary>
        /// Sends a message of type T over the multicast socket.
        /// </summary>
        public AckSession Send<T>(T content, SendOptions? options = null) where T : class
        {
            int messageType;
            if (options?.MessageType != null)
            {
                messageType = options.MessageType.Value;
            }
            else if (!_typeToIdMap.TryGetValue(typeof(T), out messageType))
            {
                throw new ArgumentException($"Type {typeof(T).Name} is not registered and no MessageType was provided in SendOptions.");
            }

            var message = new TransportMessage(LocalSource, messageType, content)
            {
                RequestAck = options?.RequestAck ?? false
            };

            return SendInternal(message, options?.AckTimeout);
        }

        private AckSession SendInternal(TransportMessage message, TimeSpan? ackTimeout)
        {
            var session = new AckSession(message.MessageId);

            if (message.RequestAck)
            {
                _activeSessions.TryAdd(message.MessageId, session);
                _ = session.WaitAsync(ackTimeout ?? DefaultAckTimeout).ContinueWith(_ =>
                {
                    _activeSessions.TryRemove(message.MessageId, out var _);
                });
            }

            string json;
            lock (exportLock)
            {
                json = JsonConvert.SerializeObject(message, _jsonSettings);
            }

            _socket?.Send(json);

            if (!message.RequestAck)
            {
                session.ReportAck(LocalSource);
            }

            return session;
        }

        /// <summary>
        /// Sends an acknowledgement for the message associated with the given context.
        /// </summary>
        public void SendAck(MessageContext context)
        {
            SendAck(context.MessageId);
        }

        private void SendAck(Guid originalMessageId)
        {
            var ackContent = new AckMessageContent
            {
                OriginalMessageId = originalMessageId
            };

            var ackMessage = new TransportMessage(LocalSource, AckMessageType, ackContent)
            {
                RequestAck = false
            };

            SendInternal(ackMessage, null);
        }

        internal void HandleSocketMessage(SocketMessage msg)
        {
            bool shouldStartTimeout;
            bool shouldProcessNow;

            lock (gate)
            {
                if (_isStopping) return;

                if (msg.SequenceId > _currentMessageCons)
                {
                    _waitingMessages[msg.SequenceId] = msg;
                    shouldStartTimeout = true;
                    shouldProcessNow = false;
                }
                else if (msg.SequenceId == _currentMessageCons)
                {
                    shouldStartTimeout = false;
                    shouldProcessNow = true;
                }
                else
                {
                    Logger.LogWarning("Received late message {0} (current is {1}). Ignoring.", msg.SequenceId, _currentMessageCons);
                    return;
                }
            }

            if (shouldProcessNow)
            {
                ProcessMessageAndSequence(msg);
            }
            else if (shouldStartTimeout)
            {
                // Start a task to wait and then jump if necessary
                _ = Task.Run(async () =>
                {
                    await Task.Delay(GateKeeperTimeout);
                    SocketMessage? nextToProcess = null;
                    lock (gate)
                    {
                        if (_isStopping) return;
                        
                        // If the message we were waiting for still hasn't arrived
                        if (msg.SequenceId > _currentMessageCons)
                        {
                            Logger.LogWarning("Sequence gap detected. Timed out waiting for message {0}. Jumping to {1}.", _currentMessageCons, msg.SequenceId);
                            _currentMessageCons = msg.SequenceId;
                            if (_waitingMessages.TryGetValue(_currentMessageCons, out nextToProcess))
                            {
                                _waitingMessages.Remove(_currentMessageCons);
                            }
                        }
                    }
                    if (nextToProcess != null)
                    {
                        ProcessMessageAndSequence(nextToProcess);
                    }
                });
            }
        }

        private void ProcessMessageAndSequence(SocketMessage initialMsg)
        {
            SocketMessage? currentMsg = initialMsg;

            while (currentMsg != null)
            {
                try
                {
                    string sMessage = Encoding.UTF8.GetString(currentMsg.Data);
                    TransportMessage? tMessage;

                    lock (importLock)
                    {
                        Logger.LogTrace("Importing message {0}", currentMsg.SequenceId);
                        tMessage = JsonConvert.DeserializeObject<TransportMessage>(sMessage, _jsonSettings);
                    }

                    if (tMessage != null)
                    {
                        ProcessTransportMessage(tMessage, currentMsg.SequenceId);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error Processing Received Message {0}: {1}", currentMsg.SequenceId, ex.Message);
                }
                finally
                {
                    currentMsg = NudgeGateAndGetNext();
                }
            }
        }

        private void ProcessTransportMessage(TransportMessage tMessage, int sequenceId)
        {
            if (IgnoreLocalMessages && tMessage.MessageSource.ResourceId == LocalSource.ResourceId)
            {
                Logger.LogTrace("Ignoring local message {0}", sequenceId);
                return;
            }

            if (tMessage.MessageType == AckMessageType && tMessage.MessageData is AckMessageContent ackContent)
            {
                if (_activeSessions.TryGetValue(ackContent.OriginalMessageId, out var session))
                {
                    Logger.LogTrace("Received Ack for message {0} from {1}", ackContent.OriginalMessageId, tMessage.MessageSource.ResourceName);
                    session.ReportAck(tMessage.MessageSource);
                }
            }

            Logger.LogTrace("Processing message {0}", sequenceId);

            bool handled = false;

            if (_genericHandlers.TryGetValue(tMessage.MessageType, out var handler))
            {
                var context = new MessageContext(tMessage.MessageId, tMessage.MessageSource, tMessage.TimeStamp, tMessage.RequestAck);
                handler.DynamicInvoke(tMessage.MessageData, context);
                handled = true;
            }

            if (handled && AutoSendAcks && tMessage.RequestAck && tMessage.MessageType != AckMessageType)
            {
                Logger.LogTrace("Automatically sending Ack for message {0}", tMessage.MessageId);
                SendAck(tMessage.MessageId);
            }
        }

        private SocketMessage? NudgeGateAndGetNext()
        {
            lock (gate)
            {
                _currentMessageCons++;
                
                if (_waitingMessages.TryGetValue(_currentMessageCons, out var nextMsg))
                {
                    _waitingMessages.Remove(_currentMessageCons);
                    return nextMsg;
                }

                Monitor.PulseAll(gate);
                return null;
            }
        }
    }
}